using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerDataClient : MonoBehaviour
{
    [SerializeField] private GameAuthClient authClient;
    [SerializeField] private string serverBaseUrl = "https://tps-project.onrender.com";

    public PlayerData CurrentData { get; private set; }
    public string LastError { get; private set; }
    public bool IsBusy { get; private set; }

    public event Action<PlayerData> DataLoaded;
    public event Action<PlayerData> DataSaved;
    public event Action<string> RequestFailed;

    private void Awake()
    {
        if (authClient == null)
            authClient = GetComponent<GameAuthClient>();

        if (authClient == null)
            authClient = FindFirstObjectByType<GameAuthClient>();
    }

    public void SetAuthClient(GameAuthClient client)
    {
        authClient = client;
    }

    public void LoadPlayerData()
    {
        if (IsBusy) return;

        StartCoroutine(LoadPlayerDataRoutine());
    }

    public void SavePlayerData(PlayerData data)
    {
        if (IsBusy) return;

        StartCoroutine(SavePlayerDataRoutine(data));
    }

    public IEnumerator LoadPlayerDataRoutine()
    {
        yield return SendPlayerDataRequest("GET", null, DataLoaded);
    }

    public IEnumerator SavePlayerDataRoutine(PlayerData data)
    {
        string json = JsonUtility.ToJson(data);
        yield return SendPlayerDataRequest("PUT", json, DataSaved);
    }

    private IEnumerator SendPlayerDataRequest(string method, string json, Action<PlayerData> success)
    {
        if (authClient == null || string.IsNullOrEmpty(authClient.Token))
        {
            Fail("Login token is missing.");
            yield break;
        }

        IsBusy = true;
        LastError = string.Empty;

        string url = $"{serverBaseUrl.TrimEnd('/')}/player-data";
        using UnityWebRequest request = new UnityWebRequest(url, method);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Authorization", $"Bearer {authClient.Token}");

        if (!string.IsNullOrEmpty(json))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.SetRequestHeader("Content-Type", "application/json");
        }

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Fail($"Player data request failed: {request.responseCode} {request.error} {request.downloadHandler.text}");
            IsBusy = false;
            yield break;
        }

        PlayerDataResponse response = JsonUtility.FromJson<PlayerDataResponse>(request.downloadHandler.text);
        CurrentData = response.playerData;

        Debug.Log($"[PlayerDataClient] Data received: Lv.{CurrentData.level}, coins={CurrentData.coins}, weapon={CurrentData.selectedWeapon}", this);
        success?.Invoke(CurrentData);
        IsBusy = false;
    }

    private void Fail(string error)
    {
        LastError = error;
        Debug.LogError($"[PlayerDataClient] {LastError}", this);
        RequestFailed?.Invoke(LastError);
    }

    [Serializable]
    private class PlayerDataResponse
    {
        public PlayerData playerData;
    }

    [Serializable]
    public class PlayerData
    {
        public string userId;
        public string displayName;
        public int level;
        public int xp;
        public int coins;
        public string selectedWeapon;
        public string lastLoginAt;
        public string updatedAt;
    }
}
