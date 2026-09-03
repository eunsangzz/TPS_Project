using UnityEngine;
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
    private Button newGuestButton;
    private Button refreshButton;
    private Text leaderboardStatus;
    private Text personalBest;
    private Text personalSkills;
    private readonly Text[] ranks = new Text[10];
    private readonly Text[] names = new Text[10];
    private readonly Text[] scores = new Text[10];
    private readonly Text[] skillRecords = new Text[10];
    private RectTransform contentRect;
    private Canvas loginCanvas;
    private bool enteringGame;
    private bool refreshAfterLoad;
    private GameObject settingsPanel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateInLoginScene()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        EnsureLoginUI();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => EnsureLoginUI();

    private static void EnsureLoginUI()
    {
        if (SceneManager.GetActiveScene().name != "Login") return;
        if (FindFirstObjectByType<LoginScreenUI>() != null) return;

        GameObject loginObject = new GameObject("LoginScreenUI");
        loginObject.AddComponent<LoginScreenUI>();
    }

    private void Awake()
    {
        session = GameSession.GetOrCreate();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        BuildUI();
    }

    private void Start() => RefreshLeaderboard();

    private void Update()
    {
        float width = Mathf.Min(540f, ((RectTransform)loginCanvas.transform).rect.width - 48f);
        contentRect.sizeDelta = new Vector2(Mathf.Max(240f, width), contentRect.sizeDelta.y);
        if (session.AuthClient.IsSignedIn)
            personalBest.text = $"{session.AuthClient.CurrentUser.displayName}   BEST {session.ScoreClient.BestScore:N0}   {session.ScoreClient.SaveStatus}";
        else
            personalBest.text = "";
        personalSkills.gameObject.SetActive(session.AuthClient.IsSignedIn);
        if (session.AuthClient.IsSignedIn)
        {
            personalSkills.text = "BEST BUILD: " + ScoreSkillRecord.Format(session.ScoreClient.BestSkills);
            FitSkillText(personalSkills);
        }
        foreach (Text record in skillRecords)
            if (record.gameObject.activeSelf) FitSkillText(record);
    }

    private void OnEnable()
    {
        session.AuthClient.SignedIn += HandleSignedIn;
        session.AuthClient.SignInFailed += HandleFailed;
        session.PlayerDataClient.DataLoaded += HandlePlayerDataLoaded;
        session.PlayerDataClient.RequestFailed += HandleFailed;
        session.ScoreClient.LeaderboardLoaded += HandleLeaderboardLoaded;
        session.ScoreClient.LeaderboardFailed += HandleLeaderboardFailed;
        session.ScoreClient.ScoreSaved += RefreshLeaderboard;
    }

    private void OnDisable()
    {
        GameSettings.Save();
        if (session == null) return;

        session.AuthClient.SignedIn -= HandleSignedIn;
        session.AuthClient.SignInFailed -= HandleFailed;
        session.PlayerDataClient.DataLoaded -= HandlePlayerDataLoaded;
        session.PlayerDataClient.RequestFailed -= HandleFailed;
        session.ScoreClient.LeaderboardLoaded -= HandleLeaderboardLoaded;
        session.ScoreClient.LeaderboardFailed -= HandleLeaderboardFailed;
        session.ScoreClient.ScoreSaved -= RefreshLeaderboard;
    }

    private void BuildUI()
    {
        GameUIInput.EnsureEventSystem(transform);

        Canvas canvas = CreateCanvas();
        loginCanvas = canvas;
        Image background = CreateImage("Background", canvas.transform, new Color(0.05f, 0.07f, 0.09f, 1f));
        Stretch(background.rectTransform);

        Image accent = CreateImage("Accent", canvas.transform, new Color(0.14f, 0.58f, 0.64f, 0.18f));
        RectTransform accentRect = accent.rectTransform;
        accentRect.anchorMin = new Vector2(0f, 0f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.offsetMin = new Vector2(0f, 0f);
        accentRect.offsetMax = new Vector2(0f, 0f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        viewport.transform.SetParent(canvas.transform, false);
        Inset(viewport.GetComponent<RectTransform>(), 16f, 16f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject panelObject = new GameObject("LoginContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panelObject.transform.SetParent(viewport.transform, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = new Vector2(540f, 0f);
        contentRect = panelRect;
        ScrollRect scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = panelRect;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;

        VerticalLayoutGroup layout = panelObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 24, 26);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panelObject.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        CreateText("Title", panelObject.transform, "TPS PROJECT", 32, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.92f, 0.96f, 1f, 1f), 42f);
        CreateButton(panelObject.transform, "SettingsButton", "SETTINGS", ToggleSettings);
        BuildSettings(panelObject.transform);
        personalBest = CreateText("PersonalBest", panelObject.transform, "", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.35f), 36f);
        personalSkills = CreateText("PersonalSkills", panelObject.transform, "", 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.68f, 0.85f, 0.8f), 24f);

        usernameInput = CreateInput(panelObject.transform, "UsernameInput", "Username", false, "player");
        passwordInput = CreateInput(panelObject.transform, "PasswordInput", "Password", true, "1234");
        loginButton = CreateButton(panelObject.transform, "LoginButton", "LOGIN", HandleLoginClicked);

        CreateSeparator(panelObject.transform);

        guestNameInput = CreateInput(panelObject.transform, "GuestNameInput", "Guest name", false, "Guest");
        guestNameInput.characterLimit = 32;
        guestButton = CreateButton(panelObject.transform, "GuestButton", session.AuthClient.HasSavedGuest ? "CONTINUE AS GUEST" : "START AS GUEST", HandleGuestClicked);
        newGuestButton = CreateButton(panelObject.transform, "NewGuestButton", "NEW GUEST", HandleNewGuestClicked);
        newGuestButton.gameObject.SetActive(session.AuthClient.HasSavedGuest);

        statusText = CreateText("Status", panelObject.transform, "Ready", 14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.68f, 0.76f, 0.82f, 1f), 34f);
        CreateSeparator(panelObject.transform);
        CreateText("RankingTitle", panelObject.transform, "TOP 10", 24, FontStyle.Bold, TextAnchor.MiddleLeft, new Color(1f, 0.8f, 0.35f), 34f);
        leaderboardStatus = CreateText("RankingStatus", panelObject.transform, "Loading...", 14, FontStyle.Normal, TextAnchor.MiddleLeft, Color.white, 24f);
        for (int i = 0; i < 10; i++)
        {
            GameObject row = new GameObject("RankRow" + i, typeof(RectTransform));
            row.transform.SetParent(panelObject.transform, false);
            SetLayoutHeight(row, 28f);
            ranks[i] = CreateRowCell(row.transform, "Rank", 0f, 0.12f, TextAnchor.MiddleLeft);
            names[i] = CreateRowCell(row.transform, "Name", 0.12f, 0.75f, TextAnchor.MiddleLeft);
            scores[i] = CreateRowCell(row.transform, "Score", 0.75f, 1f, TextAnchor.MiddleRight);
            skillRecords[i] = CreateText("RankSkills" + i, panelObject.transform, "", 13, FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.68f, 0.78f, 0.8f), 24f);
        }
        refreshButton = CreateButton(panelObject.transform, "RefreshRanking", "REFRESH RANKING", RefreshLeaderboard);
    }

    private void ToggleSettings()
    {
        bool show = !settingsPanel.activeSelf;
        settingsPanel.SetActive(show);
        if (!show) GameSettings.Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) GameSettings.Save();
    }

    private void OnApplicationQuit() => GameSettings.Save();

    private void BuildSettings(Transform parent)
    {
        settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        settingsPanel.transform.SetParent(parent, false);
        settingsPanel.GetComponent<Image>().color = new Color(0.09f, 0.13f, 0.17f, 1f);
        VerticalLayoutGroup layout = settingsPanel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 12, 12);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        settingsPanel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Text sensitivity = CreateText("SensitivityLabel", settingsPanel.transform,
            $"MOUSE SENSITIVITY   {GameSettings.MouseSensitivity:0.00}x", 15, FontStyle.Bold,
            TextAnchor.MiddleLeft, Color.white, 28f);
        CreateSettingsSlider("MouseSensitivity", GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
            GameSettings.MouseSensitivity, value =>
            {
                GameSettings.SetMouseSensitivity(value);
                sensitivity.text = $"MOUSE SENSITIVITY   {GameSettings.MouseSensitivity:0.00}x";
            });
        Text volume = CreateText("VolumeLabel", settingsPanel.transform,
            $"MASTER VOLUME   {GameSettings.MasterVolume * 100f:0}%", 15, FontStyle.Bold,
            TextAnchor.MiddleLeft, Color.white, 28f);
        CreateSettingsSlider("MasterVolume", 0f, 1f, GameSettings.MasterVolume, value =>
        {
            GameSettings.SetMasterVolume(value);
            volume.text = $"MASTER VOLUME   {GameSettings.MasterVolume * 100f:0}%";
        });
        CreateText("SettingsHint", settingsPanel.transform, "Applies immediately. Saved when closed.", 12,
            FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.68f, 0.76f, 0.82f), 22f);
        CreateButton(settingsPanel.transform, "CloseSettings", "DONE", ToggleSettings);
        settingsPanel.SetActive(false);
    }

    private void CreateSettingsSlider(string objectName, float min, float max, float value,
        UnityEngine.Events.UnityAction<float> changed)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Slider));
        root.transform.SetParent(settingsPanel.transform, false);
        SetLayoutHeight(root, 32f);
        root.GetComponent<Image>().color = new Color(0.13f, 0.19f, 0.24f);
        Slider slider = root.GetComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;

        Image track = CreateImage("Track", root.transform, new Color(0.25f, 0.33f, 0.39f));
        Inset(track.rectTransform, 12f, 12f);
        track.raycastTarget = false;
        Image fill = CreateImage("Fill", track.transform, new Color(0.12f, 0.7f, 0.76f));
        Stretch(fill.rectTransform);
        fill.raycastTarget = false;
        slider.fillRect = fill.rectTransform;

        GameObject handleArea = new GameObject("HandleArea", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        Inset(handleArea.GetComponent<RectTransform>(), 12f, 0f);
        Image handle = CreateImage("Handle", handleArea.transform, Color.white);
        handle.rectTransform.sizeDelta = new Vector2(18f, 28f);
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(changed);
    }

    private Text CreateRowCell(Transform parent, string name, float left, float right, TextAnchor alignment)
    {
        Text text = CreateText(name, parent, "", 16, FontStyle.Normal, alignment, Color.white, 28f);
        text.rectTransform.anchorMin = new Vector2(left, 0f);
        text.rectTransform.anchorMax = new Vector2(right, 1f);
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 11;
        text.resizeTextMaxSize = 16;
        return text;
    }

    private void RefreshLeaderboard()
    {
        if (session.ScoreClient.IsLoadingLeaderboard)
        {
            refreshAfterLoad = true;
            return;
        }
        refreshAfterLoad = false;
        leaderboardStatus.text = "Loading...";
        refreshButton.interactable = false;
        session.ScoreClient.LoadLeaderboard();
    }

    private void HandleLeaderboardLoaded(ScoreClient.LeaderboardEntry[] entries)
    {
        leaderboardStatus.text = entries.Length == 0 ? "No scores yet" : "Highest scores";
        refreshButton.interactable = true;
        for (int i = 0; i < ranks.Length; i++)
        {
            bool present = i < entries.Length;
            ranks[i].text = present ? entries[i].rank.ToString("00") : "";
            names[i].text = present ? entries[i].displayName : "";
            scores[i].text = present ? entries[i].bestScore.ToString("N0") : "";
            ranks[i].transform.parent.gameObject.SetActive(present);
            skillRecords[i].gameObject.SetActive(present);
            skillRecords[i].text = present ? ScoreSkillRecord.Format(entries[i].bestSkills) : "";
        }
        if (refreshAfterLoad) RefreshLeaderboard();
    }

    private void FitSkillText(Text label)
    {
        float height = Mathf.Max(22f, label.preferredHeight + 6f);
        if (!Mathf.Approximately(label.rectTransform.sizeDelta.y, height)) SetLayoutHeight(label.gameObject, height);
    }

    private void HandleLeaderboardFailed(string error)
    {
        leaderboardStatus.text = error;
        refreshButton.interactable = true;
        if (refreshAfterLoad) RefreshLeaderboard();
    }

    private Canvas CreateCanvas()
    {
        GameObject canvasObject = new GameObject("LoginCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 1000f);
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

        Text buttonLabel = CreateText("Label", buttonObject.transform, label, 15, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, 46f);
        Stretch(buttonLabel.rectTransform);
        return button;
    }

    private Text CreateText(string objectName, Transform parent, string text, int fontSize, FontStyle style, TextAnchor alignment, Color color, float height)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        SetLayoutHeight(textObject, height);

        Text label = textObject.GetComponent<Text>();
        label.text = text;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.supportRichText = false;
        label.raycastTarget = false;
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
        enteringGame = true;
        SetBusy(true, "Logging in...");
        session.AuthClient.LoginWithCredentials(usernameInput.text, passwordInput.text);
    }

    private void HandleGuestClicked()
    {
        enteringGame = true;
        SetBusy(true, "Creating guest session...");
        session.AuthClient.LoginAsGuest(guestNameInput.text);
    }

    private void HandleNewGuestClicked()
    {
        enteringGame = true;
        SetBusy(true, "Creating new guest...");
        session.AuthClient.LoginAsNewGuest(guestNameInput.text);
    }

    private void HandleSignedIn(GameAuthClient.AuthUser user)
    {
        SetBusy(true, $"Welcome, {user.displayName}. Loading player data...");
        session.PlayerDataClient.LoadPlayerData();
    }

    private void HandlePlayerDataLoaded(PlayerDataClient.PlayerData data)
    {
        if (!enteringGame) return;
        SetBusy(false, $"Loaded Lv.{data.level} data. Entering game...");
        SceneManager.LoadScene(mainSceneName);
    }

    private void HandleFailed(string error)
    {
        enteringGame = false;
        SetBusy(false, "Sign-in failed. Check connection or account.");
    }

    private void SetBusy(bool busy, string status)
    {
        if (statusText != null)
            statusText.text = status;

        if (loginButton != null)
            loginButton.interactable = !busy;

        if (guestButton != null)
            guestButton.interactable = !busy;
        if (newGuestButton != null)
            newGuestButton.interactable = !busy;
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
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
    }
}
