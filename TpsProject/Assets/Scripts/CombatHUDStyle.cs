using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared presentation only: gameplay state stays with its existing owner.
public static class CombatHUDStyle
{
    public static readonly Color Accent = new Color(0.2f, 0.85f, 0.8f);
    public static readonly Color Danger = new Color(1f, 0.28f, 0.32f);
    public static readonly Color Panel = new Color(0.025f, 0.04f, 0.055f, 0.88f);
    public static readonly Color Muted = new Color(0.65f, 0.74f, 0.78f);

    public static Canvas CreateCanvas(string name, out RectTransform safeArea)
    {
        // Independent root: legacy HUD canvases use constant-pixel scaling.
        var root = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10000; // Above the existing scope mask (9999).
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        safeArea = Rect("SafeArea", root.transform, Vector2.zero, Vector2.zero, Vector2.zero);
        ApplySafeArea(safeArea);
        return canvas;
    }

    public static void ApplySafeArea(RectTransform rect)
    {
        UnityEngine.Rect safe = Screen.safeArea;
        rect.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
        rect.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    public static Image Image(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, anchor, position, size).gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static TextMeshProUGUI Text(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
    {
        var text = Rect(name, parent, new Vector2(0.5f, 0.5f), position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = Resources.Load<TMP_FontAsset>("CombatHUDFont");
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    public static void DestroyCanvas(Canvas canvas)
    {
        if (canvas == null) return;
        if (Application.isPlaying) Object.Destroy(canvas.gameObject);
        else Object.DestroyImmediate(canvas.gameObject);
    }
}
