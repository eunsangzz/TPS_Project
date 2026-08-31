using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class GameAuthClient : MonoBehaviour
{
    [Header("Server")]
    [SerializeField] private string serverBaseUrl = "https://tps-project.onrender.com";

    [Header("Test Login")]
    [SerializeField] private string username = "player";
    [SerializeField] private string password = "1234";
    [SerializeField] private string guestDisplayName = "Guest";

    public string Token { get; private set; }
    public AuthUser CurrentUser { get; private set; }
    public string LastError { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsSignedIn => !string.IsNullOrEmpty(Token) && CurrentUser != null;

    public event Action<AuthUser> SignedIn;
    public event Action<string> SignInFailed;

    [ContextMenu("Login As Demo User")]
    public void LoginAsDemoUser()
    {
        LoginWithCredentials(username, password);
    }

    [ContextMenu("Login As Guest")]
    public void LoginAsGuest()
    {
        LoginAsGuest(guestDisplayName);
    }

    public void LoginWithCredentials(string loginUsername, string loginPassword)
    {
        if (IsBusy) return;

        StartCoroutine(Login(loginUsername, loginPassword));
    }

    public void LoginAsGuest(string displayName)
    {
        if (IsBusy) return;

        StartCoroutine(GuestLogin(displayName));
    }

    public IEnumerator GuestLogin(string displayName)
    {
        GuestLoginRequest requestBody = new GuestLoginRequest
        {
            displayName = displayName
        };

        yield return SendAuthRequest("/auth/guest", JsonUtility.ToJson(requestBody));
    }

    public IEnumerator Login(string loginUsername, string loginPassword)
    {
        LoginRequest requestBody = new LoginRequest
        {
            username = loginUsername,
            password = loginPassword
        };

        yield return SendAuthRequest("/auth/login", JsonUtility.ToJson(requestBody));
    }

    private IEnumerator SendAuthRequest(string path, string json)
    {
        IsBusy = true;
        LastError = string.Empty;

        string url = $"{serverBaseUrl.TrimEnd('/')}{path}";
        using UnityWebRequest request = new UnityWebRequest(url, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            LastError = $"Auth failed: {request.responseCode} {request.error} {request.downloadHandler.text}";
            Debug.LogError($"[GameAuthClient] {LastError}", this);
            SignInFailed?.Invoke(LastError);
            IsBusy = false;
            yield break;
        }

        AuthResponse response = JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);
        Token = response.token;
        CurrentUser = response.user;
        PlayerPrefs.SetString("AuthToken", Token);
        PlayerPrefs.SetString("AuthUserId", CurrentUser.id);
        PlayerPrefs.SetString("AuthDisplayName", CurrentUser.displayName);
        PlayerPrefs.Save();

        Debug.Log($"[GameAuthClient] Login success: {CurrentUser.displayName} ({CurrentUser.id}), guest={CurrentUser.isGuest}", this);
        SignedIn?.Invoke(CurrentUser);
        IsBusy = false;
    }

    [Serializable]
    private class GuestLoginRequest
    {
        public string displayName;
    }

    [Serializable]
    private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class AuthResponse
    {
        public string token;
        public AuthUser user;
    }

    [Serializable]
    public class AuthUser
    {
        public string id;
        public string username;
        public string displayName;
        public bool isGuest;
    }
}
