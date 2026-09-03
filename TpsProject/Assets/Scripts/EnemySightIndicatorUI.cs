using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemySightIndicatorUI : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private RectTransform indicatorRoot;
    [SerializeField] private Vector2 indicatorSize = new Vector2(34f, 72f);
    [SerializeField] private Color indicatorColor = new Color(1f, 0f, 0.23f, 0.9f);
    [SerializeField] private float verticalOffset = 1.8f;
    [SerializeField] private float screenPadding = 28f;

    private readonly Dictionary<EnemyPerception, RectTransform> indicators = new Dictionary<EnemyPerception, RectTransform>();
    private readonly HashSet<EnemyPerception> visibleThisFrame = new HashSet<EnemyPerception>();
    private readonly List<EnemyPerception> indicatorsToRemove = new List<EnemyPerception>();
    private Sprite indicatorSprite;
    private Texture2D indicatorTexture;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (indicatorRoot == null) indicatorRoot = transform as RectTransform;
        indicatorSprite = CreateIndicatorSprite();
    }

    private void LateUpdate()
    {
        visibleThisFrame.Clear();

        if (targetCamera != null && indicatorRoot != null)
        {
            EnemyPerception[] perceptions = FindObjectsByType<EnemyPerception>(FindObjectsSortMode.None);
            foreach (EnemyPerception perception in perceptions)
            {
                if (perception == null || !perception.isActiveAndEnabled) continue;

                EnemyAI enemy = perception.GetComponent<EnemyAI>();
                if (enemy == null || !enemy.CanCurrentlySeePlayer) continue;

                visibleThisFrame.Add(perception);
                RectTransform indicator = GetOrCreateIndicator(perception);
                indicator.gameObject.SetActive(true);
                PositionIndicator(indicator, perception.transform);
            }
        }

        // Cleanup must also run when the camera or UI root has been removed.
        foreach (var pair in indicators)
        {
            if (pair.Key == null || !pair.Key.isActiveAndEnabled || pair.Value == null)
            {
                if (pair.Value != null) Destroy(pair.Value.gameObject);
                indicatorsToRemove.Add(pair.Key);
            }
            else if (!visibleThisFrame.Contains(pair.Key))
            {
                pair.Value.gameObject.SetActive(false);
            }
        }

        foreach (EnemyPerception perception in indicatorsToRemove)
            indicators.Remove(perception);

        indicatorsToRemove.Clear();
        visibleThisFrame.Clear();
    }

    private void OnDisable()
    {
        ClearIndicators();
    }

    private void OnDestroy()
    {
        ClearIndicators();
        if (indicatorSprite != null) Destroy(indicatorSprite);
        if (indicatorTexture != null) Destroy(indicatorTexture);
        indicatorSprite = null;
        indicatorTexture = null;
    }

    private void ClearIndicators()
    {
        foreach (RectTransform indicator in indicators.Values)
        {
            if (indicator != null) Destroy(indicator.gameObject);
        }

        indicators.Clear();
        visibleThisFrame.Clear();
        indicatorsToRemove.Clear();
    }

    private RectTransform GetOrCreateIndicator(EnemyPerception perception)
    {
        if (indicators.TryGetValue(perception, out RectTransform existing) && existing != null)
        {
            return existing;
        }

        GameObject marker = new GameObject($"SightIndicator_{perception.name}", typeof(RectTransform), typeof(Image));
        marker.transform.SetParent(indicatorRoot, false);

        RectTransform rect = marker.GetComponent<RectTransform>();
        rect.sizeDelta = indicatorSize;
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = marker.GetComponent<Image>();
        image.sprite = indicatorSprite;
        image.color = indicatorColor;
        image.raycastTarget = false;

        indicators[perception] = rect;
        return rect;
    }

    private void PositionIndicator(RectTransform indicator, Transform enemyTransform)
    {
        Vector3 world = enemyTransform.position + Vector3.up * verticalOffset;
        Vector3 screen = targetCamera.WorldToScreenPoint(world);

        if (screen.z < 0f)
        {
            screen.x = Screen.width - screen.x;
            screen.y = Screen.height - screen.y;
        }

        screen.x = Mathf.Clamp(screen.x, screenPadding, Screen.width - screenPadding);
        screen.y = Mathf.Clamp(screen.y, screenPadding, Screen.height - screenPadding);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            indicatorRoot,
            screen,
            null,
            out Vector2 localPoint);

        indicator.anchoredPosition = localPoint;

        Vector3 toEnemy = enemyTransform.position - targetCamera.transform.position;
        Vector3 flat = Vector3.ProjectOnPlane(toEnemy, targetCamera.transform.forward);
        float angle = flat.sqrMagnitude > 0.0001f
            ? Vector3.SignedAngle(targetCamera.transform.up, flat.normalized, targetCamera.transform.forward)
            : 0f;
        indicator.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private Sprite CreateIndicatorSprite()
    {
        const int width = 32;
        const int height = 72;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        indicatorTexture = texture;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, Color.clear);
            }
        }

        Vector2 center = new Vector2(width * 0.5f, height * 0.5f);
        float outerRadius = 30f;
        float innerRadius = 23f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2(x, y) - center;
                float distance = p.magnitude;
                float angle = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;

                bool inArc = distance >= innerRadius && distance <= outerRadius && angle > -72f && angle < 72f;
                bool inTip = x > width * 0.58f && Mathf.Abs(y - center.y) < 9f - (x - width * 0.58f) * 0.25f;

                if (inArc || inTip)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
    }
}
