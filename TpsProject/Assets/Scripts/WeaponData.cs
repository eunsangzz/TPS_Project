using UnityEngine;

[CreateAssetMenu(fileName = "WeaponData", menuName = "TPS/Weapon Data")]
public class WeaponData : ScriptableObject
{
    public enum FireMode { Single, Auto }

    [Header("Combat")]
    public float damage = 20f;
    public float fireRate = 10f;
    public float range = 200f;
    public LayerMask hitMask = ~0;

    [Header("Ammo")]
    public int magazineSize = 30;
    public float reloadTime = 2f;
    public bool autoReloadWhenEmpty = true;

    [Header("Spread")]
    public float hipSpread = 2.5f;
    public float shoulderSpread = 1.2f;
    public float scopeSpread = 0.25f;

    [Header("Fire Mode")]
    public FireMode fireMode = FireMode.Auto;
}
