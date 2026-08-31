using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ScoreClient : MonoBehaviour
{
    public int BestScore { get; private set; }
    public bool IsSaving { get; private set; }
    public bool HasPendingScore => pendingScore > BestScore;
    public bool IsLoadingLeaderboard { get; private set; }
    public string SaveStatus { get; private set; } = "Not signed in";
    public event Action ScoreSaved;
    public event Action<LeaderboardEntry[]> LeaderboardLoaded;
    public event Action<string> LeaderboardFailed;

    private GameAuthClient auth;
    private PlayerDataClient playerData;
    private string userId;
    private int pendingScore;
    private float nextAttempt;
    private float retryDelay = 2f;
    private bool needsLogin;

    public void Initialize(GameAuthClient authClient, PlayerDataClient dataClient)
    {
        auth = authClient;
        playerData = dataClient;
        auth.SignedIn += HandleSignedIn;
        playerData.DataLoaded += HandleDataLoaded;
        if (auth.IsSignedIn) HandleSignedIn(auth.CurrentUser);
    }

    private void OnDestroy()
    {
        if (auth != null) auth.SignedIn -= HandleSignedIn;
        if (playerData != null) playerData.DataLoaded -= HandleDataLoaded;
    }

    private void HandleSignedIn(GameAuthClient.AuthUser user)
    {
        userId = user.id;
        BestScore = 0;
        pendingScore = PlayerPrefs.GetInt(PendingKey(userId), 0);
        needsLogin = false;
        retryDelay = 2f;
        nextAttempt = 0f;
        SaveStatus = HasPendingScore ? "Save pending" : "Saved";
    }

    private void HandleDataLoaded(PlayerDataClient.PlayerData data)
    {
        if (data.userId != userId) return;
        BestScore = Mathf.Max(BestScore, data.bestScore);
        data.bestScore = BestScore;
        ClearSavedPending();
    }

    private static string PendingKey(string id) => "PendingBestScore_" + id;

    public void QueueScore(int score)
    {
        if (auth == null || !auth.IsSignedIn || string.IsNullOrEmpty(userId)) return;
        if (score <= BestScore || score <= pendingScore) return;
        pendingScore = score;
        // Save locally before sending so a failed request survives closing the game.
        PlayerPrefs.SetInt(PendingKey(userId), pendingScore);
        PlayerPrefs.Save();
        if (!IsSaving && !needsLogin) SaveStatus = "Save pending";
    }

    private void Update()
    {
        if (auth == null || !auth.IsSignedIn || IsSaving || needsLogin || !HasPendingScore) return;
        if (Time.realtimeSinceStartup >= nextAttempt) StartCoroutine(SavePendingScore());
    }

    private IEnumerator SavePendingScore()
    {
        IsSaving = true;
        SaveStatus = "Saving...";
        string requestUser = userId;
        string requestToken = auth.Token;
        int submittedScore = pendingScore;
        using UnityWebRequest request = new UnityWebRequest(auth.ServerBaseUrl + "/scores", "POST");
        request.timeout = 90;
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ScoreRequest { score = submittedScore })));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");
        request.SetRequestHeader("Authorization", "Bearer " + requestToken);
        yield return request.SendWebRequest();

        IsSaving = false;
        if (requestUser != userId || requestToken != auth.Token) yield break;
        ScoreResponse response = null;
        if (request.result == UnityWebRequest.Result.Success)
        {
            try { response = JsonUtility.FromJson<ScoreResponse>(request.downloadHandler.text); }
            catch (ArgumentException) { }
        }
        if (response?.playerData != null && response.playerData.userId == userId && response.playerData.bestScore >= submittedScore)
        {
            BestScore = Mathf.Max(BestScore, response.playerData.bestScore);
            if (playerData.CurrentData != null && playerData.CurrentData.userId == userId)
                playerData.CurrentData.bestScore = BestScore;
            ClearSavedPending();
            retryDelay = 2f;
            nextAttempt = 0f;
            SaveStatus = HasPendingScore ? "Save pending" : "Saved";
            ScoreSaved?.Invoke();
        }
        else
        {
            needsLogin = request.responseCode == 401;
            SaveStatus = needsLogin ? "Sign in again to save" : "Save pending - retrying";
            nextAttempt = Time.realtimeSinceStartup + retryDelay;
            retryDelay = Mathf.Min(retryDelay * 2f, 30f);
            Debug.LogWarning($"[ScoreClient] Score not confirmed ({request.responseCode}). Pending score retained.", this);
        }
    }

    private void ClearSavedPending()
    {
        if (HasPendingScore) return;
        PlayerPrefs.DeleteKey(PendingKey(userId));
        PlayerPrefs.Save();
        if (!IsSaving) SaveStatus = "Saved";
    }

    public void LoadLeaderboard()
    {
        if (!IsLoadingLeaderboard) StartCoroutine(LoadLeaderboardRoutine());
    }

    private IEnumerator LoadLeaderboardRoutine()
    {
        IsLoadingLeaderboard = true;
        using UnityWebRequest request = UnityWebRequest.Get(auth.ServerBaseUrl + "/leaderboard");
        request.timeout = 90;
        yield return request.SendWebRequest();
        IsLoadingLeaderboard = false;
        LeaderboardResponse response = null;
        if (request.result == UnityWebRequest.Result.Success)
        {
            try { response = JsonUtility.FromJson<LeaderboardResponse>(request.downloadHandler.text); }
            catch (ArgumentException) { }
        }
        if (response?.entries != null) LeaderboardLoaded?.Invoke(response.entries);
        else LeaderboardFailed?.Invoke("Ranking unavailable. Please retry.");
    }

    [Serializable] private class ScoreRequest { public int score; }
    [Serializable] private class ScoreResponse { public PlayerDataClient.PlayerData playerData; }
    [Serializable] private class LeaderboardResponse { public LeaderboardEntry[] entries; }
    [Serializable] public class LeaderboardEntry
    {
        public int rank;
        public string displayName;
        public int bestScore;
        public bool isGuest;
    }
}
