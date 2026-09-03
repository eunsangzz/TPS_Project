using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "TPS/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public enum FireMode { Single, Auto }

    [Header("Combat")]
    public float damage = 20f;
    public float fireRate = 10f;
    public float range = 200f;
    [Min(1)] public int pelletCount = 1;
    public LayerMask hitMask = ~0;

    [Header("Ammo")]
    public int magazineSize = 30;
    public float reloadTime = 2f;

    [Header("Spread")]
    public float hipSpread = 2.5f;
    public float shoulderSpread = 1.2f;
    public float scopeSpread = 0.25f;

    [Header("Fire Mode")]
    public FireMode fireMode = FireMode.Auto;

    [Header("Audio")]
    public AudioClip fireClip;
    public AudioClip reloadClip;

    [Header("Maximum Upgrade Stats (Level 3)")]
    public float upgradedDamage = 20f;
    public float upgradedFireRate = 10f;
    public int upgradedMagazineSize = 30;
    public int upgradedPelletCount = 1;
    public float upgradedReloadTime = 2f;
    public float upgradedHipSpread = 2.5f;
    public float upgradedShoulderSpread = 1.2f;
    public float upgradedScopeSpread = 0.25f;

    public Stats GetStats(int level)
    {
        float t = Mathf.Clamp(level, 0, 3) / 3f;
        return new Stats
        {
            Damage = Mathf.Lerp(damage, upgradedDamage, t),
            FireRate = Mathf.Lerp(fireRate, upgradedFireRate, t),
            MagazineSize = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(magazineSize, upgradedMagazineSize, t))),
            PelletCount = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(pelletCount, upgradedPelletCount, t))),
            ReloadTime = Mathf.Max(0.01f, Mathf.Lerp(reloadTime, upgradedReloadTime, t)),
            HipSpread = Mathf.Lerp(hipSpread, upgradedHipSpread, t),
            ShoulderSpread = Mathf.Lerp(shoulderSpread, upgradedShoulderSpread, t),
            ScopeSpread = Mathf.Lerp(scopeSpread, upgradedScopeSpread, t)
        };
    }

    public struct Stats
    {
        public float Damage, FireRate, ReloadTime, HipSpread, ShoulderSpread, ScopeSpread;
        public int MagazineSize, PelletCount;
    }
}
