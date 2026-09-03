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
}
