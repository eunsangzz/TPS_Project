using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LoginScreenUI : MonoBehaviour
{
    [SerializeField] private string mainSceneName = "MainScene";

    private GameSession session;
    private InputField usernameInput;
    private InputField passwordInput;
    private InputField guestNameInput;
    private Text statusText;
    private Button loginButton;
    private Button guestButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInLoginScene()
    {
        if (SceneManager.GetActiveScene().name != "Login") return;
        if (FindFirstObjectByType<LoginScreenUI>() != null) return;

        GameObject loginObject = new GameObject("LoginScreenUI");
        loginObject.AddComponent<LoginScreenUI>();
    }

    private void Awake()
    {
        session = GameSession.GetOrCreate();
        BuildUI();
    }

    private void OnEnable()
    {
        session.AuthClient.SignedIn += HandleSignedIn;
        session.AuthClient.SignInFailed += HandleFailed;
        session.PlayerDataClient.DataLoaded += HandlePlayerDataLoaded;
        session.PlayerDataClient.RequestFailed += HandleFailed;
    }

    private void OnDisable()
    {
        if (session == null) return;

        session.AuthClient.SignedIn -= HandleSignedIn;
        session.AuthClient.SignInFailed -= HandleFailed;
        session.PlayerDataClient.DataLoaded -= HandlePlayerDataLoaded;
        session.PlayerDataClient.RequestFailed -= HandleFailed;
    }

    private void BuildUI()
    {
        EnsureEventSystem();

        Canvas canvas = CreateCanvas();
        Image background = CreateImage("Background", canvas.transform, new Color(0.05f, 0.07f, 0.09f, 1f));
        Stretch(background.rectTransform);

        Image accent = CreateImage("Accent", canvas.transform, new Color(0.14f, 0.58f, 0.64f, 0.18f));
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.offsetMin = new Vector2(0f, 0f);
        accentRect.offsetMax = new Vector2(0f, 0f);

        GameObject panelObject = new GameObject("LoginPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(430f, 0f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.1f, 0.12f, 0.15f, 0.94f);

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 26);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateText("Title", panelObject.transform, "TPS PROJECT", 32, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.96f, 1f, 1f), 42f);
        CreateText("Subtitle", panelObject.transform, "Sign in to load your player data.", 15, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.72f, 0.78f, 0.84f, 1f), 28f);

        usernameInput = CreateInput(panelObject.transform, "UsernameInput", "Username", false, "player");
        passwordInput = CreateInput(panelObject.transform, "PasswordInput", "Password", true, "1234");
        loginButton = CreateButton(panelObject.transform, "LoginButton", "LOGIN", HandleLoginClicked);

        CreateSeparator(panelObject.transform);

        guestNameInput = CreateInput(panelObject.transform, "GuestNameInput", "Guest name", false, "Guest");
        guestButton = CreateButton(panelObject.transform, "GuestButton", "START AS GUEST", HandleGuestClicked);

        statusText = CreateText("Status", panelObject.transform, "Ready", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.68f, 0.76f, 0.82f, 1f), 34f);
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("LoginCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    private InputField CreateInput(Transform parent, string objectName, string placeholder, bool password, string value)
    {
        GameObject inputObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(InputField));
        inputObject.transform.SetParent(parent, false);
        SetLayoutHeight(inputObject, 44f);

        Image image = inputObject.GetComponent<Image>();
        image.color = new Color(0.16f, 0.18f, 0.22f, 1f);

        InputField input = inputObject.GetComponent<InputField>();
        input.text = value;
        input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;

        Text text = CreateText("Text", inputObject.transform, "", 16, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, 44f);
        Text placeholderText = CreateText("Placeholder", inputObject.transform, placeholder, 16, FontStyle.Italic, TextAnchor.MiddleLeft, new Color(0.55f, 0.6f, 0.65f, 1f), 44f);
        Inset(text.rectTransform, 14f, 8f);
        Inset(placeholderText.rectTransform, 14f, 8f);

        input.textComponent = text;
        input.placeholder = placeholderText;
        return input;
    }

    private Button CreateButton(Transform parent, string objectName, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        SetLayoutHeight(buttonObject, 46f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.1f, 0.55f, 0.62f, 1f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        CreateText("Label", buttonObject.transform, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 46f);
        return button;
    }

    private Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyle style, TextAnchor alignment, Color color, float height)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        SetLayoutHeight(textObject, height);

        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = color;

        return label;
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private void CreateSeparator(Transform parent)
    {
        Image separator = CreateImage("Separator", parent, new Color(1f, 1f, 1f, 0.12f));
        SetLayoutHeight(separator.gameObject, 1f);
    }

    private void HandleLoginClicked()
    {
        SetBusy(true, "Logging in...");
        session.AuthClient.LoginWithCredentials(usernameInput.text, passwordInput.text);
    }

    private void HandleGuestClicked()
    {
        SetBusy(true, "Creating guest session...");
        session.AuthClient.LoginAsGuest(guestNameInput.text);
    }

    private void HandleSignedIn(GameAuthClient.AuthUser user)
    {
        SetBusy(true, $"Welcome, {user.displayName}. Loading player data...");
        session.PlayerDataClient.LoadPlayerData();
    }

    private void HandlePlayerDataLoaded(PlayerDataClient.PlayerData data)
    {
        SetBusy(false, $"Loaded Lv.{data.level} data. Entering game...");
        SceneManager.LoadScene(mainSceneName);
    }

    private void HandleFailed(string error)
    {
        SetBusy(false, error);
    }

    private void SetBusy(bool busy, string status)
    {
        if (statusText != null)
            statusText.text = status;

        if (loginButton != null)
            loginButton.interactable = !busy;

        if (guestButton != null)
            guestButton.interactable = !busy;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystem.transform.SetParent(transform, false);
    }

    private void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void Inset(RectTransform rect, float horizontal, float vertical)
    {
        Stretch(rect);
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    private void SetLayoutHeight(GameObject gameObject, float height)
    {
        LayoutElement element = gameObject.GetComponent<LayoutElement>();
        if (element == null)
            element = gameObject.AddComponent<LayoutElement>();

        element.preferredHeight = height;
        element.minHeight = height;
    }
}
