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
    {
        if (isReloading) return false;
        if (ammoInMag >= data.magazineSize) return false;
        if (!infiniteReserveAmmo && reserveAmmo <= 0) return false;
        return true;
    }

    public void StartReload()
    {
        isReloading = true;
    }

    public void FinishReload(WeaponData data)
    {
        int need = data.magazineSize - ammoInMag;
        int take = infiniteReserveAmmo ? need : Mathf.Min(need, reserveAmmo);

        ammoInMag += take;
        if (!infiniteReserveAmmo)
            reserveAmmo -= take;
        isReloading = false;
    }

    public void CancelReload()
    {
        isReloading = false;
    }
}
