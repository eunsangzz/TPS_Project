using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerWeaponBarUI : MonoBehaviour
{
    private PlayerLoadout loadout;
    private ThirdPersonShooter shooter;
    private PlayerHealth health;
    private Canvas canvas;
    private RectTransform bar;
    private Text ammo;
    private Text status;
    private readonly Image[] slots = new Image[4];
    private readonly Image[] accents = new Image[4];
    private readonly PlayerWeaponIcon[] icons = new PlayerWeaponIcon[4];
    private readonly Text[] slotNames = new Text[4];
    private Image cooldownFill;
    private string lastAmmo;
    private string lastStatus;

    public void Initialize(PlayerLoadout owner, ThirdPersonShooter gun, PlayerHealth playerHealth)
    {
        loadout = owner;
        shooter = gun;
        health = playerHealth;
        if (canvas == null) BuildUI();
        LateUpdate();
    }

    private void BuildUI()
    {
        GameObject root = new GameObject("WeaponBarCanvas", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        bar = new GameObject("WeaponBar", typeof(RectTransform)).GetComponent<RectTransform>();
        bar.SetParent(root.transform, false);
        bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0f);
        bar.sizeDelta = new Vector2(328f, 126f);
        ammo = MakeText("Ammo", bar, new Vector2(0f, 111f), new Vector2(328f, 28f), 24);
        status = MakeText("Status", bar, new Vector2(0f, 86f), new Vector2(328f, 18f), 12);
        for (int i = 0; i < 4; i++)
        {
            slots[i] = MakeImage($"Slot{i + 1}", bar, new Vector2(-126f + i * 84f, 36f), new Vector2(76f, 72f), Color.black);
            MakeText("Key", slots[i].transform, new Vector2(-26f, 22f), new Vector2(20f, 16f), 12).text = (i + 1).ToString();
            var icon = new GameObject("Icon", typeof(RectTransform), typeof(PlayerWeaponIcon)).GetComponent<PlayerWeaponIcon>();
            icons[i] = icon;
            icon.transform.SetParent(slots[i].transform, false);
            icon.rectTransform.sizeDelta = new Vector2(44f, 28f);
            icon.rectTransform.anchoredPosition = new Vector2(0f, 3f);
            icon.Slot = i + 1;
            icon.raycastTarget = false;
            icon.color = Color.white;
            slotNames[i] = MakeText("Name", slots[i].transform, new Vector2(0f, -22f), new Vector2(72f, 18f), 11);
            slotNames[i].text =
                i == 0 ? "MELEE" : i == 1 ? "RIFLE" : i == 2 ? "SHOTGUN" : "SNIPER";
            accents[i] = MakeImage("Selection", slots[i].transform, new Vector2(0f, -35f), new Vector2(76f, 3f), Color.clear);
        }
        cooldownFill = MakeImage("MeleeCooldown", slots[0].transform, new Vector2(0f, 35f), new Vector2(76f, 2f), new Color(1f, 0.7f, 0.2f));
        cooldownFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        cooldownFill.rectTransform.anchoredPosition = new Vector2(-38f, 35f);
    }

    private void LateUpdate()
    {
        if (canvas == null || loadout == null || shooter == null) return;
        canvas.enabled = isActiveAndEnabled && loadout.isActiveAndEnabled && (health == null || !health.IsDead);
        if (!canvas.enabled) return;
        Rect safe = Screen.safeArea;
        float scale = Mathf.Min(1f, Mathf.Max(0.1f, (safe.width - 24f) / 328f));
        bar.localScale = Vector3.one * scale;
        bar.anchoredPosition = new Vector2(safe.center.x - Screen.width * 0.5f, safe.yMin + 20f);
        Color selected = loadout.IsGunEquipped ? new Color(0.2f, 0.85f, 0.8f) : new Color(1f, 0.7f, 0.2f);
        for (int i = 0; i < 4; i++)
        {
            bool active = loadout.SelectedSlot == i + 1;
            bool unlocked = loadout.IsSlotUnlocked(i + 1);
            slots[i].color = active ? new Color(0.11f, 0.16f, 0.18f, 0.97f) : new Color(0.025f, 0.03f, 0.04f, 0.85f);
            accents[i].color = active ? selected : unlocked ? new Color(0.3f, 0.33f, 0.36f, 0.65f) : Color.clear;
            icons[i].color = unlocked ? Color.white : new Color(0.25f, 0.27f, 0.29f);
            slotNames[i].text = i == 0 ? "MELEE" : !unlocked ? "LOCKED" : i == 1 ? "RIFLE" : i == 2 ? "SHOTGUN" : "SNIPER";
            slotNames[i].color = unlocked ? Color.white : new Color(0.42f, 0.44f, 0.46f);
        }
        string ammoLabel = loadout.IsGunEquipped ? $"{shooter.AmmoInMag} / {shooter.ReserveAmmo}" : "MELEE";
        string statusLabel = loadout.IsMeleeEquipped ? (loadout.Melee.CooldownRemaining > 0f ? "RECOVERING" : "READY") :
            shooter.IsReloading ? "RELOADING" : shooter.AmmoInMag + shooter.ReserveAmmo == 0 ? "EMPTY" :
            loadout.IsShotgunEquipped ? "SHOTGUN / 4 PELLETS" :
            loadout.IsSniperEquipped ? "SNIPER / SINGLE / 6X" :
            shooter.CurrentFireMode == WeaponData.FireMode.Auto ? "AUTO" : "SINGLE";
        if (lastAmmo != ammoLabel) { ammo.text = ammoLabel; lastAmmo = ammoLabel; }
        if (lastStatus != statusLabel) { status.text = statusLabel; lastStatus = statusLabel; }
        ammo.color = selected;
        float ready = 1f - Mathf.Clamp01(loadout.Melee.CooldownRemaining / Mathf.Max(0.01f, loadout.Melee.CooldownDuration));
        cooldownFill.rectTransform.sizeDelta = new Vector2(76f * ready, 2f);
        cooldownFill.enabled = loadout.IsMeleeEquipped;
    }

    private static Image MakeImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, parent.name == "WeaponBar" ? 0f : 0.5f);
        image.rectTransform.anchoredPosition = position;
        image.rectTransform.sizeDelta = size;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Text MakeText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        Text text = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
        text.transform.SetParent(parent, false);
        text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0.5f, parent.name == "WeaponBar" ? 0f : 0.5f);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return text;
    }

    private void OnDisable() { if (canvas != null) canvas.enabled = false; }
    private void OnDestroy()
    {
        if (canvas == null) return;
        if (Application.isPlaying) Destroy(canvas.gameObject);
        else DestroyImmediate(canvas.gameObject);
    }
}
