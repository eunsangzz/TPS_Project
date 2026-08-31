using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyHealthBar : MonoBehaviour
{
    [Header("Placement")]
    [SerializeField] private float fallbackHeight = 2f;
    [SerializeField] private float headPadding = 0.3f;
    [SerializeField] private float worldWidth = 1.4f;

    private EnemyHealth health;
    private Camera viewCamera;
    private Collider bodyCollider;
    private Renderer[] bodyRenderers;
    private Canvas canvas;
    private RectTransform fillRect;
    private Image fill;
    private Text healthText;
    private int lastCurrent = -1;
    private int lastMax = -1;

    public void Initialize(EnemyHealth target)
    {
        health = target;
        bodyCollider = target.GetComponent<Collider>();
        if (bodyRenderers == null) bodyRenderers = target.GetComponentsInChildren<Renderer>();
        if (canvas == null) BuildUI();
        Refresh();
    }

    private void BuildUI()
    {
        GameObject root = new GameObject("EnemyHealthCanvas", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 36f);

        Image background = MakeImage("Background", root.transform, new Color(0.025f, 0.03f, 0.035f, 0.95f));
        background.rectTransform.anchorMin = new Vector2(0f, 0f);
        background.rectTransform.anchorMax = new Vector2(1f, 0f);
        background.rectTransform.pivot = new Vector2(0.5f, 0f);
        background.rectTransform.sizeDelta = new Vector2(0f, 10f);

        Image track = MakeImage("Track", background.transform, new Color(0.18f, 0.2f, 0.22f, 1f));
        Stretch(track.rectTransform, 2f);
        fill = MakeImage("Health", track.transform, Color.green);
        fillRect = fill.rectTransform;
        Stretch(fillRect, 0f);

        GameObject label = new GameObject("HealthText", typeof(RectTransform), typeof(Text), typeof(Outline));
        label.transform.SetParent(root.transform, false);
        healthText = label.GetComponent<Text>();
        healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        healthText.fontSize = 18;
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.color = Color.white;
        healthText.raycastTarget = false;
        healthText.supportRichText = false;
        healthText.rectTransform.anchorMin = new Vector2(0f, 0f);
        healthText.rectTransform.anchorMax = new Vector2(1f, 1f);
        healthText.rectTransform.offsetMin = new Vector2(0f, 10f);
        healthText.rectTransform.offsetMax = Vector2.zero;
        Outline outline = label.GetComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = Vector2.one;
    }

    private void LateUpdate() => Refresh();

    private void Refresh()
    {
        if (canvas == null || health == null) return;
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
        canvas.enabled = health.HasTakenDamage && health.isActiveAndEnabled && !health.IsDead &&
            viewCamera != null && viewCamera.isActiveAndEnabled;
        if (!canvas.enabled) return;

        float top = float.NegativeInfinity;
        if (bodyCollider != null && bodyCollider.enabled && !bodyCollider.isTrigger)
            top = bodyCollider.bounds.max.y;
        foreach (Renderer body in bodyRenderers)
        {
            if (body == null || !body.enabled || !body.gameObject.activeInHierarchy) continue;
            if (!(body is MeshRenderer) && !(body is SkinnedMeshRenderer)) continue;
            top = Mathf.Max(top, body.bounds.max.y);
        }
        if (float.IsNegativeInfinity(top)) top = transform.position.y + fallbackHeight;

        canvas.transform.position = new Vector3(transform.position.x, top + headPadding, transform.position.z);
        canvas.transform.rotation = viewCamera.transform.rotation;
        // Keep the bar's world size stable even on differently scaled enemy prefabs.
        Vector3 scale = transform.lossyScale;
        float unit = Mathf.Max(0.1f, worldWidth) / 140f;
        canvas.transform.localScale = new Vector3(
            unit / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
            unit / Mathf.Max(0.001f, Mathf.Abs(scale.y)),
            unit / Mathf.Max(0.001f, Mathf.Abs(scale.z)));

        float ratio = health.maxHealth > 0f ? Mathf.Clamp01(health.currentHealth / health.maxHealth) : 0f;
        fillRect.anchorMax = new Vector2(ratio, 1f);
        fill.color = ratio > 0.5f ? new Color(0.2f, 0.9f, 0.4f) :
            ratio > 0.25f ? new Color(1f, 0.75f, 0.15f) : new Color(1f, 0.2f, 0.15f);

        int current = Mathf.CeilToInt(Mathf.Max(0f, health.currentHealth));
        int maximum = Mathf.CeilToInt(Mathf.Max(0f, health.maxHealth));
        if (current != lastCurrent || maximum != lastMax)
        {
            healthText.text = $"{current} / {maximum}";
            lastCurrent = current;
            lastMax = maximum;
        }
    }

    private static Image MakeImage(string name, Transform parent, Color color)
    {
        GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        Image image = item.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * inset;
        rect.offsetMax = Vector2.one * -inset;
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDestroy()
    {
        if (canvas == null) return;
        if (Application.isPlaying) Destroy(canvas.gameObject);
        else DestroyImmediate(canvas.gameObject);
    }
}
