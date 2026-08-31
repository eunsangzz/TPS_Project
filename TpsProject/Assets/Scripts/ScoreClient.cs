using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class ScoreClient : MonoBehaviour
{
    public int BestScore { get; private set; }
    public ScoreSkillRecord[] BestSkills { get; private set; } = Array.Empty<ScoreSkillRecord>();
    private string bestRunId;
    public bool IsSaving { get; private set; }
    public bool HasPendingScore => pending != null && IsNewer(pending.score, pending.runId,
        ScoreSkillRecord.Revision(pending.skills), BestScore, bestRunId, ScoreSkillRecord.Revision(BestSkills));
    public bool IsLoadingLeaderboard { get; private set; }
    public string SaveStatus { get; private set; } = "Not signed in";
    public event Action ScoreSaved;
    public event Action<LeaderboardEntry[]> LeaderboardLoaded;
    public event Action<string> LeaderboardFailed;

    private GameAuthClient auth;
    private PlayerDataClient playerData;
    private string userId;
    private ScoreRequest pending;
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
        BestSkills = Array.Empty<ScoreSkillRecord>();
        bestRunId = null;
        pending = null;
        try { pending = JsonUtility.FromJson<ScoreRequest>(PlayerPrefs.GetString(RecordKey(userId), "")); }
        catch (ArgumentException) { }
        int legacyScore = PlayerPrefs.GetInt(PendingKey(userId), 0);
        if (legacyScore > (pending?.score ?? 0)) pending = new ScoreRequest { score = legacyScore, skills = Array.Empty<ScoreSkillRecord>() };
        needsLogin = false;
        retryDelay = 2f;
        nextAttempt = 0f;
        SaveStatus = HasPendingScore ? "Save pending" : "Saved";
    }

    private void HandleDataLoaded(PlayerDataClient.PlayerData data)
    {
        if (data.userId != userId) return;
        ApplyRecord(data);
        ClearSavedPending();
    }

    private static string PendingKey(string id) => "PendingBestScore_" + id;
    private static string RecordKey(string id) => "PendingScoreRecord_" + id;

    private static bool IsNewer(int score, string runId, int revision, int otherScore, string otherRunId, int otherRevision)
    {
        return score > otherScore || (score == otherScore && !string.IsNullOrEmpty(runId) && runId == otherRunId && revision > otherRevision);
    }

    public void QueueScore(int score, PlayerSkills skills = null)
    {
        if (auth == null || !auth.IsSignedIn || string.IsNullOrEmpty(userId)) return;
        string runId = skills != null ? skills.RunId : null;
        int revision = skills != null ? skills.SelectionCount : 0;
        if (score <= 0 || !IsNewer(score, runId, revision, BestScore, bestRunId, ScoreSkillRecord.Revision(BestSkills))) return;
        if (pending != null && !IsNewer(score, runId, revision, pending.score, pending.runId, ScoreSkillRecord.Revision(pending.skills))) return;
        pending = new ScoreRequest { score = score, runId = runId, skills = skills != null ? skills.CreateScoreSnapshot() : Array.Empty<ScoreSkillRecord>() };
        // Persist the immutable score/build pair together, before starting the request.
        PlayerPrefs.SetString(RecordKey(userId), JsonUtility.ToJson(pending));
        PlayerPrefs.DeleteKey(PendingKey(userId));
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
        ScoreRequest submitted = pending;
        using UnityWebRequest request = new UnityWebRequest(auth.ServerBaseUrl + "/scores", "POST");
        request.timeout = 90;
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(submitted)));
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
        if (response?.playerData != null && response.playerData.userId == userId && response.playerData.bestScore >= submitted.score)
        {
            ApplyRecord(response.playerData);
            if (playerData.CurrentData != null && playerData.CurrentData.userId == userId)
            {
                playerData.CurrentData.bestScore = BestScore;
                playerData.CurrentData.bestSkills = BestSkills;
                playerData.CurrentData.bestRunId = bestRunId;
            }
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
        pending = null;
        PlayerPrefs.DeleteKey(PendingKey(userId));
        PlayerPrefs.DeleteKey(RecordKey(userId));
        PlayerPrefs.Save();
        if (!IsSaving) SaveStatus = "Saved";
    }

    private void ApplyRecord(PlayerDataClient.PlayerData data)
    {
        bool stale = data.bestScore < BestScore || (data.bestScore == BestScore && data.bestRunId == bestRunId &&
            ScoreSkillRecord.Revision(data.bestSkills) < ScoreSkillRecord.Revision(BestSkills));
        if (!stale)
        {
            BestScore = data.bestScore;
            BestSkills = data.bestSkills ?? Array.Empty<ScoreSkillRecord>();
            bestRunId = data.bestRunId;
        }
        data.bestScore = BestScore;
        data.bestSkills = BestSkills;
        data.bestRunId = bestRunId;
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

    [Serializable] private class ScoreRequest { public int score; public string runId; public ScoreSkillRecord[] skills; }
    [Serializable] private class ScoreResponse { public PlayerDataClient.PlayerData playerData; }
    [Serializable] private class LeaderboardResponse { public LeaderboardEntry[] entries; }
    [Serializable] public class LeaderboardEntry
    {
        public int rank;
        public string displayName;
        public int bestScore;
        public ScoreSkillRecord[] bestSkills;
        public bool isGuest;
    }
}
