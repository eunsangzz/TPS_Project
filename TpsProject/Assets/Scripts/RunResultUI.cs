using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RunResultUI : MonoBehaviour
{
    private Text saveState;

    public void Show(int score)
    {
        ThirdPersonCamera cameraController = FindFirstObjectByType<ThirdPersonCamera>();
        if (cameraController != null) cameraController.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 0f;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        gameObject.AddComponent<GraphicRaycaster>();
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform);

        GameObject shade = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
        shade.transform.SetParent(transform, false);
        RectTransform shadeRect = shade.GetComponent<RectTransform>();
        shadeRect.anchorMin = Vector2.zero;
        shadeRect.anchorMax = Vector2.one;
        shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
        shade.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        MakeText("RUN COMPLETE", 36, 100f);
        MakeText($"SCORE {score:N0}", 28, 40f);
        saveState = MakeText("", 18, -10f);
        GameObject buttonObject = new GameObject("ReturnToLogin", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -90f);
        rect.sizeDelta = new Vector2(340f, 52f);
        buttonObject.GetComponent<Image>().color = new Color(0.1f, 0.55f, 0.62f);
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = buttonObject.GetComponent<Image>();
        button.onClick.AddListener(() => { Time.timeScale = 1f; SceneManager.LoadScene("Login"); });
        MakeText("RETURN TO LOGIN", 20, -90f);
    }

    private void Update()
    {
        if (saveState == null) return;
        ScoreClient client = GameSession.Instance != null ? GameSession.Instance.ScoreClient : null;
        saveState.text = client != null ? $"BEST {client.BestScore:N0}   {client.SaveStatus}" : "Offline - score not saved";
    }

    private Text MakeText(string value, int size, float y)
    {
        GameObject textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(600f, 54f);
        rect.anchoredPosition = new Vector2(0f, y);
        Text text = textObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.supportRichText = false;
        text.text = value;
        return text;
    }
}
