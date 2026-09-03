using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SkillSelectionUI : MonoBehaviour
{
    private sealed class Card
    {
        public RectTransform Rect;
        public Button Button;
        public RectTransform Stripe;
        public SkillIcon Icon;
        public Text Category, Title, Description, Level, Action;
    }

    private Canvas canvas;
    private RectTransform surface, content;
    private Text heading, stageLabel, activeLabel;
    private readonly Card[] cards = new Card[3];
    private PlayerSkill[] offers;
    private PlayerSkills skills;
    private PlayerHealth health;
    private ThirdPersonInput input;
    private ThirdPersonCamera cameraController;
    private Action completed;
    private float previousTimeScale, openedAt;
    private CursorLockMode previousCursor;
    private bool previousCursorVisible, previousCameraEnabled;
    private Vector2 lastSize;
    public bool IsOpen { get; private set; }
    public IReadOnlyList<PlayerSkill> Offers => offers;

    public bool Show(PlayerSkills owner, int completedStage, Action onSelected)
    {
        if (IsOpen || owner == null || Time.timeScale <= 0f) return false;
        health = owner.GetComponent<PlayerHealth>();
        if (health != null && health.IsDead) return false;
        skills = owner;
        input = owner.GetComponent<ThirdPersonInput>();
        if (canvas == null) Build();
        GameUIInput.EnsureEventSystem(transform);
        offers = owner.RollOffers();
        completed = onSelected;
        previousTimeScale = Time.timeScale;
        previousCursor = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        cameraController = owner.GetComponent<ThirdPersonShooter>()?.ShooterCamera?.GetComponentInParent<ThirdPersonCamera>();
        if (cameraController == null) cameraController = FindFirstObjectByType<ThirdPersonCamera>();
        previousCameraEnabled = cameraController != null && cameraController.enabled;
        if (cameraController != null) cameraController.enabled = false;
        owner.GetComponent<PlayerMelee>()?.CancelAttack();
        owner.GetComponent<ThirdPersonShooter>()?.CancelReload();
        input?.SuppressCombatInput();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        IsOpen = true;
        openedAt = Time.unscaledTime;
        if (health != null) health.Died += HandleDeath;
        stageLabel.text = $"STAGE {completedStage:00} COMPLETE";
        activeLabel.text = owner.ActiveSummary();
        for (int i = 0; i < cards.Length; i++)
        {
            PlayerSkill skill = offers[i];
            Card card = cards[i];
            Color accent = PlayerSkills.Accent(skill);
            card.Icon.Skill = skill;
            card.Icon.color = accent;
            card.Icon.SetVerticesDirty();
            card.Category.text = PlayerSkills.Category(skill);
            card.Category.color = accent;
            card.Stripe.GetComponent<Image>().color = accent;
            card.Title.text = PlayerSkills.Title(skill);
            card.Description.text = PlayerSkills.Description(skill);
            card.Level.text = PlayerSkills.IsWeaponUnlock(skill) ? "WEAPON UNLOCK" :
                (int)skill < 6 ? $"LV {owner.Level(skill)} > {owner.Level(skill) + 1}   /   MAX {PlayerSkills.MaxLevel(skill)}" : "BONUS REWARD";
            card.Action.text = PlayerSkills.IsWeaponUnlock(skill) ? "UNLOCK  >" : "ACQUIRE  >";
            card.Action.color = accent;
            card.Button.interactable = true;
        }
        surface.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        Layout();
        EventSystem.current?.SetSelectedGameObject(cards[0].Button.gameObject);
        return true;
    }

    public bool TryChoose(int index)
    {
        if (!IsOpen || Time.unscaledTime - openedAt < 0.2f || index < 0 || index >= offers.Length || skills == null) return false;
        if (!skills.Acquire(offers[index])) return false;
        foreach (Card card in cards) card.Button.interactable = false;
        Action continuation = completed;
        Close(true);
        continuation?.Invoke();
        return true;
    }

    public void Cancel() => Close(true);
    private void HandleDeath() => Close(false);

    private void Close(bool restore)
    {
        if (!IsOpen) return;
        IsOpen = false;
        completed = null;
        if (health != null) health.Died -= HandleDeath;
        surface.gameObject.SetActive(false);
        GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        if (selected != null && selected.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(null);
        input?.SuppressCombatInput();
        if (restore && (health == null || !health.IsDead))
        {
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursor;
            Cursor.visible = previousCursorVisible;
            if (cameraController != null) cameraController.enabled = previousCameraEnabled;
        }
    }

    private void OnDisable() => Cancel();
    private void LateUpdate()
    {
        if (!IsOpen) return;
        if (skills == null || !skills.isActiveAndEnabled) { Cancel(); return; }
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        if (size != lastSize) Layout();
    }

    private void Build()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 1f;
        gameObject.AddComponent<GraphicRaycaster>();
        surface = Rect("SkillSelection", transform);
        Stretch(surface);
        Image shade = surface.gameObject.AddComponent<Image>();
        shade.color = new Color(0.025f, 0.03f, 0.035f, 0.96f);
        content = Rect("Content", surface);
        heading = Label("Title", content, "FIELD UPGRADES", 34, Color.white);
        stageLabel = Label("Stage", content, "", 16, new Color(0.55f, 0.85f, 0.7f));
        activeLabel = Label("ActiveUpgrades", content, "", 14, new Color(0.65f, 0.68f, 0.7f));
        for (int i = 0; i < cards.Length; i++)
        {
            int choice = i;
            Card card = cards[i] = new Card();
            card.Rect = Rect("Choice" + i, content);
            Image background = card.Rect.gameObject.AddComponent<Image>();
            background.color = Color.white;
            card.Button = card.Rect.gameObject.AddComponent<Button>();
            card.Button.targetGraphic = background;
            ColorBlock colors = card.Button.colors;
            colors.normalColor = new Color(0.10f, 0.115f, 0.125f);
            colors.highlightedColor = new Color(0.18f, 0.2f, 0.21f);
            colors.selectedColor = new Color(0.15f, 0.17f, 0.18f);
            colors.pressedColor = new Color(0.24f, 0.27f, 0.28f);
            colors.fadeDuration = 0.08f;
            card.Button.colors = colors;
            card.Button.onClick.AddListener(() => TryChoose(choice));
            card.Stripe = Rect("Accent", card.Rect);
            card.Stripe.gameObject.AddComponent<Image>().raycastTarget = false;
            card.Icon = Rect("Icon", card.Rect).gameObject.AddComponent<SkillIcon>();
            card.Icon.raycastTarget = false;
            card.Category = Label("Category", card.Rect, "", 14, Color.white);
            card.Title = Label("Name", card.Rect, "", 24, Color.white);
            card.Description = Label("Effect", card.Rect, "", 18, new Color(0.79f, 0.82f, 0.83f));
            card.Level = Label("Level", card.Rect, "", 14, new Color(0.62f, 0.66f, 0.68f));
            card.Action = Label("Acquire", card.Rect, "ACQUIRE  >", 16, Color.white);
        }
        surface.gameObject.SetActive(false);
    }

    private void Layout()
    {
        lastSize = ((RectTransform)canvas.transform).rect.size;
        float width = Mathf.Min(1080f, lastSize.x - 40f);
        bool narrow = width < 940f;
        if (narrow) width = Mathf.Min(width, 560f);
        float cardHeight = narrow ? 156f : 360f;
        float height = narrow ? 668f : 558f;
        Place(content, (lastSize.x - width) * 0.5f, (lastSize.y - height) * 0.5f, width, height);
        Place(stageLabel.rectTransform, 0f, 0f, width, 24f);
        heading.fontSize = narrow ? 28 : 34;
        Place(heading.rectTransform, 0f, 30f, width, 42f);
        float cardWidth = narrow ? width : (width - 32f) / 3f;
        for (int i = 0; i < cards.Length; i++)
        {
            Card card = cards[i];
            Place(card.Rect, narrow ? 0f : i * (cardWidth + 16f), 94f + (narrow ? i * (cardHeight + 12f) : 0f), cardWidth, cardHeight);
            Place(card.Stripe, 0f, 0f, cardWidth, 3f);
            if (narrow)
            {
                Place(card.Icon.rectTransform, 14f, 16f, 36f, 36f);
                Place(card.Category.rectTransform, 62f, 12f, cardWidth - 76f, 18f);
                Place(card.Title.rectTransform, 62f, 31f, cardWidth - 76f, 26f);
                card.Title.fontSize = 19;
                card.Description.fontSize = 16;
                Place(card.Description.rectTransform, 16f, 66f, cardWidth - 32f, 46f);
                Place(card.Level.rectTransform, 16f, 124f, cardWidth - 110f, 20f);
                Place(card.Action.rectTransform, cardWidth - 96f, 123f, 84f, 22f);
                card.Action.fontSize = 14;
            }
            else
            {
                Place(card.Category.rectTransform, 24f, 22f, cardWidth - 48f, 22f);
                Place(card.Icon.rectTransform, 24f, 63f, 68f, 68f);
                Place(card.Title.rectTransform, 24f, 153f, cardWidth - 48f, 32f);
                card.Title.fontSize = 24;
                card.Description.fontSize = 18;
                Place(card.Description.rectTransform, 24f, 201f, cardWidth - 48f, 72f);
                Place(card.Level.rectTransform, 24f, 279f, cardWidth - 48f, 22f);
                Place(card.Action.rectTransform, 24f, 320f, cardWidth - 48f, 24f);
                card.Action.fontSize = 16;
            }
        }
        Place(activeLabel.rectTransform, 0f, narrow ? 604f : 480f, width, narrow ? 64f : 78f);
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static Text Label(string name, Transform parent, string value, int size, Color color)
    {
        Text text = Rect(name, parent).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAnchor.UpperLeft;
        text.raycastTarget = false;
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
