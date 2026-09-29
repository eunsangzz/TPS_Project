using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CombatFeedbackUI : MonoBehaviour
{
    private sealed class Popup
    {
        public TextMeshProUGUI Text;
        public Vector3 Point;
        public float Age;
    }

    private const float Lifetime = 0.8f;
    private const int PoolLimit = 64;
    private readonly List<Popup> active = new List<Popup>();
    private readonly Stack<Popup> pooled = new Stack<Popup>();
    private ThirdPersonShooter shooter;
    private PlayerHealth health;
    private Canvas canvas;
    private RectTransform safeArea, reloadPanel, popupRoot;
    private Image reloadFill;
    private TextMeshProUGUI reloadLabel;

    public void Initialize(ThirdPersonShooter owner, PlayerHealth playerHealth)
    {
        shooter = owner;
        health = playerHealth;
        if (canvas != null) return;
        canvas = CombatHUDStyle.CreateCanvas("CombatFeedbackCanvas", out safeArea);
        popupRoot = (RectTransform)canvas.transform;
        reloadPanel = CombatHUDStyle.Image("ReloadPanel", canvas.transform, new Vector2(0.5f, 0.5f),
            new Vector2(0f, -90f), new Vector2(240f, 58f), CombatHUDStyle.Panel).rectTransform;
        reloadLabel = CombatHUDStyle.Text("ReloadLabel", reloadPanel, new Vector2(0f, 9f), new Vector2(220f, 30f), 21f);
        CombatHUDStyle.Image("Track", reloadPanel, new Vector2(0.5f, 0.5f), new Vector2(0f, -17f),
            new Vector2(208f, 4f), new Color(0.2f, 0.26f, 0.29f));
        reloadFill = CombatHUDStyle.Image("Progress", reloadPanel, new Vector2(0.5f, 0.5f),
            new Vector2(-104f, -17f), new Vector2(208f, 4f), CombatHUDStyle.Accent);
        reloadFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        reloadPanel.gameObject.SetActive(false);
        // Warm a small pool; bounded growth handles automatic fire and multi-target attacks.
        for (int i = 0; i < 12; i++) pooled.Push(CreatePopup());
    }

    private Popup CreatePopup()
    {
        var text = CombatHUDStyle.Text("DamageNumber", popupRoot, Vector2.zero, new Vector2(170f, 52f), 32f);
        text.fontStyle = FontStyles.Bold;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color32(5, 12, 17, 255);
        text.gameObject.SetActive(false);
        return new Popup { Text = text };
    }

    public void ShowDamage(float amount, Vector3 worldPoint)
    {
        if (canvas == null || amount <= 0f || !isActiveAndEnabled || (health != null && health.IsDead)) return;
        Popup popup;
        if (pooled.Count > 0) popup = pooled.Pop();
        else if (active.Count < PoolLimit) popup = CreatePopup();
        else { popup = active[0]; active.RemoveAt(0); }
        popup.Point = worldPoint;
        popup.Age = 0f;
        popup.Text.text = amount.ToString("0.#", CultureInfo.InvariantCulture);
        popup.Text.color = Color.white;
        popup.Text.gameObject.SetActive(false); // Project before the first rendered frame.
        active.Add(popup);
    }

    private void LateUpdate()
    {
        if (canvas == null || shooter == null) return;
        bool alive = health == null || !health.IsDead;
        canvas.enabled = isActiveAndEnabled && shooter.isActiveAndEnabled && alive && Time.timeScale > 0f;
        if (!alive) ClearPopups();
        if (!canvas.enabled) return;
        CombatHUDStyle.ApplySafeArea(safeArea);
        reloadPanel.gameObject.SetActive(shooter.IsGunEquipped && shooter.IsReloading);
        if (reloadPanel.gameObject.activeSelf)
        {
            reloadLabel.text = $"재장전 {shooter.ReloadRemaining:0.0}초";
            reloadFill.rectTransform.sizeDelta = new Vector2(208f * shooter.ReloadProgress, 4f);
        }
        Camera view = shooter.ShooterCamera;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            Popup popup = active[i];
            popup.Age += Time.deltaTime;
            if (popup.Age >= Lifetime)
            {
                popup.Text.gameObject.SetActive(false);
                pooled.Push(popup);
                active.RemoveAt(i);
                continue;
            }
            Vector3 screen = view != null ? view.WorldToScreenPoint(popup.Point) : new Vector3(0f, 0f, -1f);
            bool visible = screen.z > 0f && Screen.safeArea.Contains(new Vector2(screen.x, screen.y));
            popup.Text.gameObject.SetActive(visible);
            if (!visible) continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupRoot, screen, null, out Vector2 local);
            popup.Text.rectTransform.anchoredPosition = local + Vector2.up * (18f + 44f * popup.Age / Lifetime);
            popup.Text.alpha = 1f - Mathf.InverseLerp(0.35f, Lifetime, popup.Age);
        }
    }

    private void ClearPopups()
    {
        foreach (Popup popup in active) { popup.Text.gameObject.SetActive(false); pooled.Push(popup); }
        active.Clear();
    }

    private void OnDisable()
    {
        if (canvas != null) canvas.enabled = false;
        ClearPopups();
    }

    private void OnDestroy() => CombatHUDStyle.DestroyCanvas(canvas);
}
