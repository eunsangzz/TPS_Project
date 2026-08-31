using UnityEngine;

public class GameSession : MonoBehaviour
{
    public static GameSession Instance { get; private set; }

    public GameAuthClient AuthClient { get; private set; }
    public PlayerDataClient PlayerDataClient { get; private set; }
    public ScoreClient ScoreClient { get; private set; }

    public PlayerDataClient.PlayerData PlayerData => PlayerDataClient != null ? PlayerDataClient.CurrentData : null;

    public static GameSession GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        GameObject sessionObject = new GameObject("GameSession");
        return sessionObject.AddComponent<GameSession>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        AuthClient = GetComponent<GameAuthClient>();
        if (AuthClient == null)
            AuthClient = gameObject.AddComponent<GameAuthClient>();

        PlayerDataClient = GetComponent<PlayerDataClient>();
        if (PlayerDataClient == null)
            PlayerDataClient = gameObject.AddComponent<PlayerDataClient>();

        PlayerDataClient.SetAuthClient(AuthClient);
        ScoreClient = GetComponent<ScoreClient>();
        if (ScoreClient == null)
            ScoreClient = gameObject.AddComponent<ScoreClient>();
        ScoreClient.Initialize(AuthClient, PlayerDataClient);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
