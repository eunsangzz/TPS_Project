using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ScopeOverlay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ThirdPersonCamera thirdPersonCamera;

    [Header("Overlay Texture")]
    [SerializeField] private int textureSize = 1024;
    [SerializeField, Range(0.0f, 1.0f)] private float outsideAlpha = 0.9f;
    [SerializeField, Range(0.2f, 0.49f)] private float circleRadius = 0.38f;
    [SerializeField, Range(1, 12)] private int ringThickness = 3;
    [SerializeField, Range(1, 6)] private int crosshairThickness = 2;
    [SerializeField, Range(0.0f, 1.0f)] private float crosshairAlpha = 0.85f;

    [Header("Fade")]
    [SerializeField] private float fadeSpeed = 18f;

    private Canvas canvas;
    private RawImage rawImage;
    private Texture2D overlayTex;

    private float currentAlpha;

    private void Awake()
    {
        if (thirdPersonCamera == null) thirdPersonCamera = GetComponent<ThirdPersonCamera>();

        CreateCanvasIfNeeded();
        BuildOverlayTexture();
        rawImage.texture = overlayTex;

        currentAlpha = 0f;
        SetRawAlpha(0f);
        rawImage.enabled = true;
    }

    private void Update()
    {
        if (thirdPersonCamera == null) return;

        float target = thirdPersonCamera.IsScoped ? 1f : 0f;
        currentAlpha = thirdPersonCamera.IsAimSuppressed || !thirdPersonCamera.CanUseScope
            ? 0f : Mathf.Lerp(currentAlpha, target, Time.deltaTime * fadeSpeed);

        SetRawAlpha(currentAlpha);
    }

    private void CreateCanvasIfNeeded()
    {
        GameObject canvasGO = new GameObject("ScopeOverlayCanvas");
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;

        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();

        DontDestroyOnLoad(canvasGO);

        GameObject imgGO = new GameObject("ScopeOverlayImage");
        imgGO.transform.SetParent(canvasGO.transform, false);

        rawImage = imgGO.AddComponent<RawImage>();
        rawImage.raycastTarget = false;

        RectTransform rt = rawImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void BuildOverlayTexture()
    {
        overlayTex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        overlayTex.wrapMode = TextureWrapMode.Clamp;
        overlayTex.filterMode = FilterMode.Bilinear;

        int w = textureSize;
        int h = textureSize;

        Vector2 center = new Vector2(w * 0.5f, h * 0.5f);
        float radiusPx = Mathf.Min(w, h) * circleRadius;

        Color outside = new Color(0f, 0f, 0f, outsideAlpha);
        Color clear = new Color(0f, 0f, 0f, 0f);
        Color ring = new Color(0f, 0f, 0f, 1f);
        Color cross = new Color(0f, 0f, 0f, crosshairAlpha);

        // 픽셀 채우기
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 원 안은 투명
                Color c = (dist <= radiusPx) ? clear : outside;

                // 링(테두리)
                if (Mathf.Abs(dist - radiusPx) <= ringThickness)
                    c = ring;

                // 십자선(원 안에만)
                if (dist <= radiusPx)
                {
                    // 세로선
                    if (Mathf.Abs(dx) <= crosshairThickness)
                        c = cross;
                    // 가로선
                    if (Mathf.Abs(dy) <= crosshairThickness)
                        c = cross;

                    // 중앙 점(조금 진하게)
                    if (Mathf.Abs(dx) <= crosshairThickness + 1 && Mathf.Abs(dy) <= crosshairThickness + 1)
                        c = new Color(0f, 0f, 0f, 1f);
                }

                overlayTex.SetPixel(x, y, c);
            }
        }

        overlayTex.Apply(false, false);
    }

    private void SetRawAlpha(float a)
    {
        if (rawImage == null) return;
        Color col = rawImage.color;
        col.a = a;
        rawImage.color = col;
    }

    private void OnDestroy()
    {
        if (overlayTex != null)
            Destroy(overlayTex);

        if (canvas != null)
            Destroy(canvas.gameObject);
    }
}
