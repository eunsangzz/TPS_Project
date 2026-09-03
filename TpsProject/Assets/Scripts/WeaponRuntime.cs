using UnityEngine;

[System.Serializable]
public class WeaponRuntime
{
    [SerializeField] private WeaponData.FireMode currentFireMode;
    [SerializeField] private int ammoInMag;
    [SerializeField] private int reserveAmmo;
    [SerializeField] private bool infiniteReserveAmmo;
    [SerializeField] private bool isReloading;
    [SerializeField] private float nextFireTime;

    public WeaponData.FireMode CurrentFireMode => currentFireMode;
    public int AmmoInMag => ammoInMag;
    public int ReserveAmmo => reserveAmmo;
    public bool InfiniteReserveAmmo => infiniteReserveAmmo;
    public bool IsReloading => isReloading;

    public void Initialize(WeaponData data, int startReserverAmmo, bool useInfiniteReserveAmmo)
    {
        currentFireMode = data.fireMode;
        ammoInMag = Mathf.Clamp(data.magazineSize, 1, 999);
        infiniteReserveAmmo = useInfiniteReserveAmmo;
        reserveAmmo = Mathf.Max(0, startReserverAmmo);
        isReloading = false;
        nextFireTime = 0f;
    }

    public bool CanFire(float timeNow)
    {
        return !isReloading && timeNow >= nextFireTime && ammoInMag > 0;
    }

    public void ConsumeAmmo()
    {
        ammoInMag = Mathf.Max(0, ammoInMag - 1);
    }

    public void SetNextFireTime(float timeNow, float fireRate)
    {
        nextFireTime = timeNow + (1f / Mathf.Max(0.01f, fireRate));
    }

    public void ToggleFireMode()
    {
        currentFireMode = currentFireMode == WeaponData.FireMode.Auto
            ? WeaponData.FireMode.Single
            : WeaponData.FireMode.Auto;
    }

    public bool CanReload(WeaponData data)
        => CanReload(data.magazineSize);

    public bool CanReload(int magazineSize)
    {
        if (isReloading) return false;
        if (ammoInMag >= magazineSize) return false;
        if (!infiniteReserveAmmo && reserveAmmo <= 0) return false;
        return true;
    }

    public void StartReload()
    {
        isReloading = true;
    }

    public void FinishReload(WeaponData data)
        => FinishReload(data.magazineSize);

    public void FinishReload(int capacity)
    {
        int magazineSize = Mathf.Clamp(capacity, 1, 999);
        int replacementRounds = infiniteReserveAmmo ? magazineSize : Mathf.Min(magazineSize, reserveAmmo);

        // Tactical magazine swap: rounds left in the removed magazine are discarded.
        ammoInMag = replacementRounds;
        if (!infiniteReserveAmmo)
            reserveAmmo -= replacementRounds;
        isReloading = false;
    }

    public void CancelReload()
    {
        isReloading = false;
    }

    public void AddReserveAmmo(int amount, int totalCapacity)
    {
        int space = Mathf.Max(0, totalCapacity - ammoInMag - reserveAmmo);
        reserveAmmo += Mathf.Min(Mathf.Max(0, amount), space);
    }

    public void ResetAmmo(WeaponData data, int totalAmmo)
        => ResetAmmo(data.magazineSize, totalAmmo);

    public void ResetAmmo(int magazineSize, int totalAmmo)
    {
        int total = Mathf.Max(0, totalAmmo);
        ammoInMag = Mathf.Min(Mathf.Clamp(magazineSize, 1, 999), total);
        reserveAmmo = total - ammoInMag;
        infiniteReserveAmmo = false;
        isReloading = false;
        nextFireTime = 0f;
    }
}
