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
    private int lastMag = -1;
    private int lastRes = -1;
    private int score;
    private int lastScore = -1;
    private bool lastReload = false;
    private WeaponData.FireMode lastMode;
    private float nextEnemyScanTime;
    private readonly HashSet<EnemyHealth> trackedEnemies = new HashSet<EnemyHealth>();

    private void Awake()
    {
        ResolveReferences();

        InitHealthUI();
        TrackEnemies();
        ForceRefresh();
    }

    private void OnDestroy()
    {
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
        SetHudTextPosition(ammoText, new Vector2(-32f, 112f));
        SetHudTextPosition(modeText, new Vector2(-32f, 76f));
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

    private void LateUpdate()
    {
        if (playerHealth == null || shooter == null)
            ResolveReferences();

        if (Time.time >= nextEnemyScanTime)
        {
            TrackEnemies();
            nextEnemyScanTime = Time.time + 0.5f;
        }

        bool hpchanged = playerHealth != null && !Mathf.Approximately(lastHp, playerHealth.CurrentHealth);
        bool ammoChanged = shooter != null && ((lastMag != shooter.AmmoInMag) || (lastRes != shooter.ReserveAmmo));
        bool reloadChanged = shooter != null && (lastReload != shooter.IsReloading);
        bool modeChanged = shooter != null && (lastMode != shooter.CurrentFireMode);
        bool scoreChanged = lastScore != score;

        if (hpchanged) UpdateHealth();
        if (ammoChanged || reloadChanged) UpdateAmmo();
        if (modeChanged) UpdateMode();
        if (scoreChanged) UpdateScore();

        if (playerHealth != null)
            lastHp = playerHealth.CurrentHealth;

        if (shooter != null)
        {
            lastMag = shooter.AmmoInMag;
            lastRes = shooter.ReserveAmmo;
            lastReload = shooter.IsReloading;
            lastMode = shooter.CurrentFireMode;
        }

        lastScore = score;
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
            healthText.text = $"{Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
        }
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
            reloadText.gameObject.SetActive(shooter.IsReloading);
    }

    private void UpdateMode()
    {
        if (shooter == null) return;
        if (modeText == null) return;

        modeText.text = shooter.CurrentFireMode == WeaponData.FireMode.Auto ? "MODE AUTO" : "MODE SINGLE";
    }

    private void UpdateScore()
    {
        if (scoreText != null)
            scoreText.text = $"SCORE {score}";
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
        score += pointsPerKill;
        UpdateScore();
    }
}
