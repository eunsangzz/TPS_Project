using System.Collections;
using UnityEngine;

public class ThirdPersonShooter : MonoBehaviour
{
    public enum FireMode { Single, Auto }

    [Header("Reference")]
    [SerializeField] private ThirdPersonInput input;
    [SerializeField] private Camera shooterCamera;
    [SerializeField] private Animator animator;
    [SerializeField] private WeaponVFX weaponVFX;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private int startReserveAmmo = 90;
    [SerializeField] private bool requireAimToShoot = false;

    [Header("Animator")]
    [SerializeField] private string fireSingleTrigger = "FireSingle";
    [SerializeField] private string fireAutoTrigger = "FireAuto";
    [SerializeField] private string reloadTrigger = "Reload";
    [SerializeField] private string fireModeIntParam = ""; 
    [SerializeField] private string isReloadingBoolParam = "";

    [SerializeField] private WeaponRuntime runtime = new WeaponRuntime();

    public int AmmoInMag => runtime.AmmoInMag;
    public int ReserveAmmo => runtime.ReserveAmmo;
    public bool InfiniteReserveAmmo => runtime.InfiniteReserveAmmo;
    public bool IsReloading => runtime.IsReloading;
    public WeaponData.FireMode CurrentFireMode => runtime.CurrentFireMode;

    private void Awake()
    {
        if (input == null) input = GetComponent<ThirdPersonInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (weaponVFX == null) weaponVFX = GetComponentInChildren<WeaponVFX>();
        if (shooterCamera == null) if (Camera.main != null) shooterCamera = Camera.main;
        if (playerHealth == null) playerHealth = GetComponent<PlayerHealth>();

        if (weaponData == null)
        {
            Debug.LogError("[ThirdPersonShooter] weaponData is not assigned.", this);
            enabled = false;
            return;
        }

        runtime.Initialize(weaponData, startReserveAmmo, true);
        PushAnimatorState();
    }

    private void Update()
    {
        if (input == null || shooterCamera == null) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        if (input.ToggleFireModePressed) 
            ToggleFireMode();

        if (input.ReloadPressed) 
            TryStartReload();

        if (runtime.IsReloading) return;


        ThirdPersonCamera camCtrl = shooterCamera != null ? shooterCamera.GetComponentInParent<ThirdPersonCamera>() : null;
        bool isAiming = (camCtrl != null && camCtrl.IsAiming);
        bool canShootByAim = !requireAimToShoot || isAiming;

        if (!canShootByAim) return;

        bool wantsShoot =
            runtime.CurrentFireMode == WeaponData.FireMode.Auto ? input.FireHeld :
            runtime.CurrentFireMode == WeaponData.FireMode.Single ? input.FirePressed :
            false;

        if (!wantsShoot) return;

        if (runtime.AmmoInMag <= 0)
        {
            if (weaponData.autoReloadWhenEmpty)
                TryStartReload();
            return;
        }

        TryShoot();
    }

    private void ToggleFireMode()
    {
        runtime.ToggleFireMode();
        PushAnimatorState();
    }

    private void PushAnimatorState()
    {
        if (animator == null || weaponData == null) return;

        if (!string.IsNullOrEmpty(fireModeIntParam))
            animator.SetInteger(fireModeIntParam, runtime.CurrentFireMode == WeaponData.FireMode.Single ? 0 : 1);

        if (!string.IsNullOrEmpty(isReloadingBoolParam))
            animator.SetBool(isReloadingBoolParam, runtime.IsReloading);
    }

    private void TryShoot()
    {
        if (!runtime.CanFire(Time.time)) return;

        runtime.SetNextFireTime(Time.time, weaponData.fireRate);
        ShootOnce();
    }

    private void ShootOnce()
    {
        runtime.ConsumeAmmo();

        ThirdPersonCamera camCtrl = shooterCamera.GetComponentInParent<ThirdPersonCamera>();
        bool isAiming = camCtrl != null && camCtrl.IsAiming;
        bool isScoped = camCtrl != null && camCtrl.IsScoped;

        float spreadDeg =
            isScoped ? weaponData.scopeSpread :
            isAiming ? weaponData.shoulderSpread :
            weaponData.hipSpread;

        Vector3 dir = ApplySpread(shooterCamera.transform.forward, spreadDeg);
        Ray ray = new Ray(shooterCamera.transform.position, dir);

        Vector3 hitPoint = ray.origin + ray.direction * weaponData.range;

        bool hitSomething = Physics.Raycast(
            ray,
            out RaycastHit hit,
            weaponData.range,
            weaponData.hitMask,
            QueryTriggerInteraction.Ignore);

        if (hitSomething)
        {
            hitPoint = hit.point;

            var damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable != null)
                damageable.TakeDamage(weaponData.damage, hit.point, ray.direction);

            if (weaponVFX != null)
            {
                weaponVFX.PlayHitEffect(hit);
                weaponVFX.SpawnBulletHole(hit);
            }
        }

        if (weaponVFX != null)
        {
            weaponVFX.PlayMuzzleFlash();
            weaponVFX.PlayTracer(hitPoint);
        }

        if (animator != null)
        {
            string trig = runtime.CurrentFireMode == WeaponData.FireMode.Single
                ? fireSingleTrigger
                : fireAutoTrigger;

            if (!string.IsNullOrEmpty(trig))
                animator.SetTrigger(trig);
        }

        if (camCtrl != null)
            camCtrl.AddRecoil();

        if (runtime.AmmoInMag <= 0 && weaponData.autoReloadWhenEmpty)
            TryStartReload();
    }

    private void TryStartReload()
    {
        if (!runtime.CanReload(weaponData)) return;
        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        runtime.StartReload();
        PushAnimatorState();

        if (animator != null && !string.IsNullOrEmpty(reloadTrigger))
            animator.SetTrigger(reloadTrigger);

        yield return new WaitForSeconds(weaponData.reloadTime);

        runtime.FinishReload(weaponData);
        PushAnimatorState();
    }


    private Vector3 ApplySpread(Vector3 forward, float spreadDeg)
    {
        if (spreadDeg <= 0.0001f) return forward;

        Vector2 rnd = Random.insideUnitCircle * spreadDeg;

        Quaternion rot = Quaternion.Euler(-rnd.y, rnd.x, 0f);

        return (rot * forward).normalized;
    }
}

public interface IDamageable
{
    void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection);
}
