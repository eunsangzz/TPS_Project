using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private ThirdPersonShooter shooter;

    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI modeText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI reloadText;

    [Header("Score")]
    [SerializeField] private int pointsPerKill = 100;

    private float lastHp = -1f;
    private float lastMaxHp = -1f;
    private Canvas healthCanvas;
    private RectTransform healthSafeArea;
    private Image healthFill;
    private int lastMag = -1;
    private int lastRes = -1;
    private int score;
    private string lastScoreLabel;
    private bool runEnded;
    private int queuedSkillRevision = -1;
    private PlayerHealth subscribedHealth;
    private bool lastReload = false;
    private WeaponData.FireMode lastMode;
    private float nextEnemyScanTime;
    private readonly HashSet<EnemyHealth> trackedEnemies = new HashSet<EnemyHealth>();

    private void Awake()
    {
        ResolveReferences();

        BuildHealthUI();
        InitHealthUI();
        TrackEnemies();
        ForceRefresh();
    }

    private void OnDestroy()
    {
        CombatHUDStyle.DestroyCanvas(healthCanvas);
        if (subscribedHealth != null) subscribedHealth.Died -= HandlePlayerDied;
        foreach (EnemyHealth enemy in trackedEnemies)
        {
            if (enemy != null)
                enemy.Died -= HandleEnemyDied;
        }

        trackedEnemies.Clear();
    }

    private void ResolveReferences()
    {
        if (playerHealth == null)
            playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (subscribedHealth != playerHealth)
        {
            if (subscribedHealth != null) subscribedHealth.Died -= HandlePlayerDied;
            subscribedHealth = playerHealth;
            if (subscribedHealth != null) subscribedHealth.Died += HandlePlayerDied;
        }

        if (shooter == null)
            shooter = FindFirstObjectByType<ThirdPersonShooter>();

        if (healthSlider == null)
            healthSlider = GetComponentInChildren<Slider>(true);

        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI text in texts)
        {
            string objectName = text.gameObject.name;

            if (ammoText == null && objectName.Contains("Ammo"))
                ammoText = text;
            else if (modeText == null && (objectName.Contains("Mode") || objectName.Contains("Fire")))
                modeText = text;
            else if (scoreText == null && objectName.Contains("Score"))
                scoreText = text;
            else if (reloadText == null && objectName.Contains("Reload"))
                reloadText = text;
            else if (healthText == null && objectName.Contains("Health"))
                healthText = text;
        }

        EnsureGameplayText();
        ArrangeGameplayText();
    }

    private void EnsureGameplayText()
    {
        Transform parent = transform;

        if (ammoText == null)
            ammoText = CreateHudText("AmmoText", parent);

        if (modeText == null)
            modeText = CreateHudText("ShootModeText", parent);

        if (scoreText == null)
            scoreText = CreateHudText("ScoreText", parent);
    }

    private TextMeshProUGUI CreateHudText(string objectName, Transform parent)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.Right;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.color = Color.white;

        TextMeshProUGUI template = ammoText != null ? ammoText : modeText;
        if (template != null && template.font != null)
            text.font = template.font;

        return text;
    }

    private void ArrangeGameplayText()
    {
        SetHudTextPosition(ammoText, new Vector2(-32f, 148f));
        SetHudTextPosition(modeText, new Vector2(-32f, 112f));
        SetHudTextPosition(scoreText, new Vector2(-32f, 40f));
    }

    private void SetHudTextPosition(TextMeshProUGUI text, Vector2 anchoredPosition)
    {
        if (text == null) return;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(260f, 34f);

        text.fontSize = Mathf.Max(text.fontSize, 28f);
        text.alignment = TextAlignmentOptions.Right;
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void InitHealthUI()
    {
        if (playerHealth == null || healthSlider == null) return;

        healthSlider.minValue = 0f;
        healthSlider.maxValue = playerHealth.MaxHealth;
        healthSlider.value = playerHealth.CurrentHealth;
    }

    private void BuildHealthUI()
    {
        if (healthSlider != null) healthSlider.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        healthCanvas = CombatHUDStyle.CreateCanvas("PlayerHealthCanvas", out healthSafeArea);
        RectTransform panel = CombatHUDStyle.Image("HealthPanel", healthSafeArea, Vector2.zero,
            new Vector2(32f, 32f), new Vector2(320f, 90f), CombatHUDStyle.Panel).rectTransform;
        panel.pivot = Vector2.zero;
        CombatHUDStyle.Image("Accent", panel, new Vector2(0f, 0.5f), new Vector2(2f, 0f),
            new Vector2(4f, 90f), CombatHUDStyle.Accent);
        healthText = CombatHUDStyle.Text("HealthValue", panel, new Vector2(0f, 15f), new Vector2(280f, 36f), 25f);
        healthText.alignment = TextAlignmentOptions.Left;
        Image track = CombatHUDStyle.Image("HealthBar", panel, new Vector2(0.5f, 0.5f), new Vector2(0f, -21f),
            new Vector2(280f, 10f), new Color(0.2f, 0.26f, 0.29f));
        healthFill = CombatHUDStyle.Image("Fill", track.transform, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero, CombatHUDStyle.Accent);
        healthFill.rectTransform.anchorMin = Vector2.zero;
        healthFill.rectTransform.anchorMax = Vector2.one;
        healthFill.rectTransform.offsetMin = healthFill.rectTransform.offsetMax = Vector2.zero;
        healthSlider = track.gameObject.AddComponent<Slider>();
        healthSlider.fillRect = healthFill.rectTransform;
        healthSlider.interactable = false;
        healthSlider.transition = Selectable.Transition.None;
        healthSlider.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private void OnDisable()
    {
        if (healthCanvas != null) healthCanvas.enabled = false;
    }

    private void LateUpdate()
    {
        if (playerHealth == null || shooter == null)
            ResolveReferences();

        if (healthCanvas != null)
        {
            healthCanvas.enabled = isActiveAndEnabled && playerHealth != null && !playerHealth.IsDead && Time.timeScale > 0f;
            CombatHUDStyle.ApplySafeArea(healthSafeArea);
        }

        bool centralWeaponBar = shooter != null && shooter.GetComponent<PlayerWeaponBarUI>() != null;
        if (ammoText != null) ammoText.gameObject.SetActive(!centralWeaponBar);
        if (modeText != null) modeText.gameObject.SetActive(!centralWeaponBar);
        if (reloadText != null && centralWeaponBar) reloadText.gameObject.SetActive(false);

        if (Time.time >= nextEnemyScanTime)
        {
            TrackEnemies();
            nextEnemyScanTime = Time.time + 0.5f;
        }

        bool hpchanged = playerHealth != null && (!Mathf.Approximately(lastHp, playerHealth.CurrentHealth) ||
            !Mathf.Approximately(lastMaxHp, playerHealth.MaxHealth));
        bool ammoChanged = shooter != null && ((lastMag != shooter.AmmoInMag) || (lastRes != shooter.ReserveAmmo));
        bool reloadChanged = shooter != null && (lastReload != shooter.IsReloading);
        bool modeChanged = shooter != null && (lastMode != shooter.CurrentFireMode);

        if (hpchanged) UpdateHealth();
        if (ammoChanged || reloadChanged) UpdateAmmo();
        if (modeChanged) UpdateMode();
        UpdateScore();

        if (playerHealth != null)
        {
            lastHp = playerHealth.CurrentHealth;
            lastMaxHp = playerHealth.MaxHealth;
        }

        if (shooter != null)
        {
            lastMag = shooter.AmmoInMag;
            lastRes = shooter.ReserveAmmo;
            lastReload = shooter.IsReloading;
            lastMode = shooter.CurrentFireMode;
        }

    }

    private void ForceRefresh()
    {
        UpdateHealth();
        UpdateAmmo();
        UpdateMode();
        UpdateScore();
    }

    private void UpdateHealth()
    {
        if (playerHealth == null) return;

        if (healthSlider != null)
        {
            if (!Mathf.Approximately(healthSlider.maxValue, playerHealth.MaxHealth))
                healthSlider.maxValue = playerHealth.MaxHealth;

            healthSlider.value = playerHealth.CurrentHealth;
        }

        if(healthText != null)
        {
            healthText.text = $"체력 {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
        }
        if (healthFill != null)
            healthFill.color = playerHealth.CurrentHealth <= playerHealth.MaxHealth * 0.25f ? CombatHUDStyle.Danger : CombatHUDStyle.Accent;
    }

    private void UpdateAmmo()
    {
        if (shooter == null) return;

        if (ammoText != null)
        {
            string reserve = shooter.InfiniteReserveAmmo ? "INF" : shooter.ReserveAmmo.ToString();
            ammoText.text = $"AMMO {shooter.AmmoInMag} / {reserve}";
        }

        if (reloadText != null)
            reloadText.gameObject.SetActive(false);
    }

    private void UpdateMode()
    {
        if (shooter == null) return;
        if (modeText == null) return;

        modeText.text = shooter.CurrentFireMode == WeaponData.FireMode.Auto ? "MODE AUTO" : "MODE SINGLE";
    }

    private void UpdateScore()
    {
        PlayerSkills skills = playerHealth != null ? playerHealth.GetComponent<PlayerSkills>() : null;
        if (!runEnded && skills != null && skills.SelectionCount != queuedSkillRevision) QueueCurrentScore();
        ScoreClient client = GameSession.Instance != null ? GameSession.Instance.ScoreClient : null;
        string label = client != null
            ? $"SCORE {score}  |  BEST {client.BestScore}\n{client.SaveStatus}"
            : $"SCORE {score}  |  OFFLINE";
        if (scoreText != null && label != lastScoreLabel)
        {
            scoreText.fontSize = 22f;
            scoreText.rectTransform.sizeDelta = new Vector2(480f, 60f);
            scoreText.text = label;
            lastScoreLabel = label;
        }
    }

    private void TrackEnemies()
    {
        EnemyHealth[] enemies = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null || trackedEnemies.Contains(enemy) || enemy.IsDead) continue;

            enemy.Died += HandleEnemyDied;
            trackedEnemies.Add(enemy);
        }
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        if (enemy != null)
            enemy.Died -= HandleEnemyDied;

        trackedEnemies.Remove(enemy);
        if (runEnded || (playerHealth != null && playerHealth.IsDead)) return;
        score = (int)System.Math.Min(int.MaxValue, (long)score + Mathf.Max(0, pointsPerKill));
        QueueCurrentScore();
        UpdateScore();
    }

    private void HandlePlayerDied()
    {
        if (runEnded) return;
        runEnded = true;
        QueueCurrentScore();
        GameObject result = new GameObject("RunResultUI");
        result.AddComponent<RunResultUI>().Show(score);
    }

    private void QueueCurrentScore()
    {
        PlayerSkills skills = playerHealth != null ? playerHealth.GetComponent<PlayerSkills>() : null;
        queuedSkillRevision = skills != null ? skills.SelectionCount : 0;
        if (GameSession.Instance != null) GameSession.Instance.ScoreClient.QueueScore(score, skills);
    }
}
