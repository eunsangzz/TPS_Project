using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerCrosshairUI : MonoBehaviour
{
    private ThirdPersonShooter shooter;
    private Camera shooterCamera;
    private ThirdPersonCamera cameraController;
    private PlayerHealth health;
    private Canvas canvas;
    private RectTransform[] arms;

    public void Initialize(ThirdPersonShooter owner, Camera view, PlayerHealth playerHealth)
    {
        shooter = owner;
        shooterCamera = view;
        cameraController = view != null ? view.GetComponentInParent<ThirdPersonCamera>() : null;
        health = playerHealth;
        if (canvas == null) BuildUI();
        LateUpdate();
    }

    private void BuildUI()
    {
        GameObject root = new GameObject("CrosshairCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        CreateMark("Center", new Vector2(3f, 3f));
        arms = new[]
        {
            CreateMark("Left", new Vector2(7f, 2f)),
            CreateMark("Right", new Vector2(7f, 2f)),
            CreateMark("Top", new Vector2(2f, 7f)),
            CreateMark("Bottom", new Vector2(2f, 7f))
        };
    }

    private RectTransform CreateMark(string label, Vector2 size)
    {
        GameObject mark = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Outline));
        mark.transform.SetParent(canvas.transform, false);
        Image image = mark.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = false;
        Outline outline = mark.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = Vector2.one;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        return rect;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;

        canvas.enabled = shooter != null && shooter.isActiveAndEnabled &&
            shooterCamera != null && shooterCamera.isActiveAndEnabled &&
            (health == null || !health.IsDead) && Time.timeScale > 0f &&
            (cameraController == null || (cameraController.isActiveAndEnabled && !cameraController.IsScoped));
        if (!canvas.enabled) return;

        float gap = cameraController != null && cameraController.IsAiming ? 7f : 11f;
        arms[0].anchoredPosition = Vector2.left * gap;
        arms[1].anchoredPosition = Vector2.right * gap;
        arms[2].anchoredPosition = Vector2.up * gap;
        arms[3].anchoredPosition = Vector2.down * gap;
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
