using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDamageFlashUI : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Image flashImage;
    [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.38f);
    [SerializeField] private float fadeDuration = 0.35f;

    private Coroutine flashRoutine;

    public static PlayerDamageFlashUI EnsureExists(PlayerHealth target)
    {
        PlayerDamageFlashUI existing = FindFirstObjectByType<PlayerDamageFlashUI>();
        if (existing != null)
        {
            existing.Bind(target);
            return existing;
        }

        GameObject root = new GameObject("PlayerDamageFlashUI");
        PlayerDamageFlashUI flash = root.AddComponent<PlayerDamageFlashUI>();
        flash.Bind(target);
        return flash;
    }

    private void Awake()
    {
        BuildUIIfNeeded();
        Bind(playerHealth != null ? playerHealth : FindFirstObjectByType<PlayerHealth>());
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.Damaged -= HandlePlayerDamaged;
    }

    private void Bind(PlayerHealth target)
    {
        if (target == null || playerHealth == target) return;

        if (playerHealth != null)
            playerHealth.Damaged -= HandlePlayerDamaged;

        playerHealth = target;
        playerHealth.Damaged += HandlePlayerDamaged;
    }

    private void BuildUIIfNeeded()
    {
        if (flashImage != null) return;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("DamageFlashImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(transform, false);

        flashImage = imageObject.GetComponent<Image>();
        flashImage.raycastTarget = false;
        flashImage.color = Color.clear;

        RectTransform rect = flashImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void HandlePlayerDamaged(float amount)
    {
        if (flashImage == null) return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        flashImage.color = flashColor;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(flashColor.a, 0f, elapsed / Mathf.Max(0.01f, fadeDuration));
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, alpha);
            yield return null;
        }

        flashImage.color = Color.clear;
        flashRoutine = null;
    }
}
