using UnityEngine;
using TMPro;
using UnityEngine.UI;

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
    [SerializeField] private TextMeshProUGUI reloadText;

    private float lastHp = -1f;
    private int lastMag = -1;
    private int lastRes = -1;
    private bool lastReload = false;
    private ThirdPersonShooter.FireMode lastMode;

    private void Awake()
    {
        if (playerHealth == null || shooter == null)
        {
            Debug.LogError("[PlayerHUD] playerHealth/shooter∏¶ ¿ŒΩ∫∆Â≈Õø° ø¨∞·«ÿ¡‡!");
            enabled = false;
            return;
        }

        InitHealthUI();
        ForceRefresh();
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
        if (playerHealth == null || shooter == null) return;

        bool hpchanged = !Mathf.Approximately(lastHp, playerHealth.CurrentHealth);
        bool ammoChanged = (lastMag != shooter.AmmoInMag) || (lastRes != shooter.ReserveAmmo);
        bool reloadChanged = (lastReload != shooter.IsReloading);
        bool modeChanged = (lastMode != shooter.CurrentFireMode);

        if (hpchanged) UpdateHealth();
        if (ammoChanged || reloadChanged) UpdateAmmo();
        if (modeChanged) UpdateMode();

        lastHp = playerHealth.CurrentHealth;
        lastMag = shooter.AmmoInMag;
        lastRes = shooter.ReserveAmmo;
        lastReload = shooter.IsReloading;
        lastMode = shooter.CurrentFireMode;
    }

    private void ForceRefresh()
    {
        UpdateHealth();
        UpdateAmmo();
        UpdateMode();
    }

    private void UpdateHealth()
    {
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
        if (ammoText != null)
            ammoText.text = $"{shooter.AmmoInMag} / {shooter.ReserveAmmo}";

        if (reloadText != null)
            reloadText.gameObject.SetActive(shooter.IsReloading);
    }

    private void UpdateMode()
    {
        if (modeText == null) return;

        modeText.text = shooter.CurrentFireMode == ThirdPersonShooter.FireMode.Auto ? "AUTO" : "SINGLE";
    }
}
