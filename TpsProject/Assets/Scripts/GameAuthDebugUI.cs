using UnityEngine;

public class GameAuthDebugUI : MonoBehaviour
{
    [SerializeField] private GameAuthClient authClient;
    [SerializeField] private string username = "player";
    [SerializeField] private string password = "1234";
    [SerializeField] private string guestDisplayName = "Guest";

    private string status = "Ready";
    private Rect windowRect = new Rect(24f, 24f, 360f, 260f);

    private void Awake()
    {
        if (authClient == null)
            authClient = GetComponent<GameAuthClient>();

        if (authClient == null)
            authClient = FindFirstObjectByType<GameAuthClient>();

        if (authClient == null)
            authClient = gameObject.AddComponent<GameAuthClient>();
    }

    private void OnEnable()
    {
        if (authClient == null) return;

        authClient.SignedIn += HandleSignedIn;
        authClient.SignInFailed += HandleSignInFailed;
    }

    private void OnDisable()
    {
        if (authClient == null) return;

        authClient.SignedIn -= HandleSignedIn;
        authClient.SignInFailed -= HandleSignInFailed;
    }

    private void OnGUI()
    {
        if (FindFirstObjectByType<LoginScreenUI>() != null) return;
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, "Server Login");
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.Label($"Status: {status}");

        GUILayout.Space(8f);
        GUILayout.Label("Demo Login");
        GUILayout.Label("Username");
        username = GUILayout.TextField(username);
        GUILayout.Label("Password");
        password = GUILayout.PasswordField(password, '*');

        GUI.enabled = authClient != null && !authClient.IsBusy;
        if (GUILayout.Button("Login"))
        {
            status = "Logging in...";
            authClient.LoginWithCredentials(username, password);
        }

        GUILayout.Space(8f);
        GUILayout.Label("Guest Login");
        GUILayout.Label("Guest Name");
        guestDisplayName = GUILayout.TextField(guestDisplayName);

        if (GUILayout.Button("Start As Guest"))
        {
            status = "Creating guest session...";
            authClient.LoginAsGuest(guestDisplayName);
        }

        GUI.enabled = true;

        if (authClient != null && authClient.IsSignedIn)
        {
            GUILayout.Space(8f);
            GUILayout.Label($"Signed in: {authClient.CurrentUser.displayName}");
            GUILayout.Label($"Token: {ShortToken(authClient.Token)}");
        }

        GUI.DragWindow();
    }

    private void HandleSignedIn(GameAuthClient.AuthUser user)
    {
        status = $"Login success: {user.displayName}";
    }

    private void HandleSignInFailed(string error)
    {
        status = error;
    }

    private string ShortToken(string token)
    {
        if (string.IsNullOrEmpty(token) || token.Length <= 8)
            return token;

        return $"{token.Substring(0, 8)}...";
    }
}
