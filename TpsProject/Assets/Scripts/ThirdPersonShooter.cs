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
    [SerializeField, Min(0)] private int ammoPerStage = 90;
    [SerializeField] private bool requireAimToShoot = false;

    [Header("Animator")]
    [SerializeField] private string fireSingleTrigger = "FireSingle";
    [SerializeField] private string fireAutoTrigger = "FireAuto";
    [SerializeField] private string reloadTrigger = "Reload";
    [SerializeField] private string fireModeIntParam = ""; 
    [SerializeField] private string isReloadingBoolParam = "";

    [SerializeField] private WeaponRuntime runtime = new WeaponRuntime();
    private Coroutine reloadRoutine;
    private PlayerLoadout loadout;
    private PlayerSkills skills;
    private int stageAmmoCapacity;

    public int AmmoInMag => runtime.AmmoInMag;
    public int ReserveAmmo => runtime.ReserveAmmo;
    public bool InfiniteReserveAmmo => runtime.InfiniteReserveAmmo;
    public bool IsReloading => runtime.IsReloading;
    public WeaponData.FireMode CurrentFireMode => runtime.CurrentFireMode;
    public bool IsGunEquipped => loadout == null || loadout.IsGunEquipped;
    public Camera ShooterCamera => shooterCamera;

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

        skills = GetComponent<PlayerSkills>();
        if (skills == null) skills = gameObject.AddComponent<PlayerSkills>();
        runtime.Initialize(weaponData, 0, false);
        ResetAmmoForStage();
        if (weaponVFX == null) weaponVFX = gameObject.AddComponent<WeaponVFX>();
        PlayerCrosshairUI crosshair = GetComponent<PlayerCrosshairUI>();
        if (crosshair == null) crosshair = gameObject.AddComponent<PlayerCrosshairUI>();
        crosshair.Initialize(this, shooterCamera, playerHealth);
        loadout = GetComponent<PlayerLoadout>();
        if (loadout == null) loadout = gameObject.AddComponent<PlayerLoadout>();
        loadout.Initialize(this);
        PushAnimatorState();
    }

    private void Update()
    {
        if (input == null || shooterCamera == null) return;
        if (!IsGunEquipped || Time.timeScale <= 0f) return;
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
        if (!IsGunEquipped || Time.timeScale <= 0f || (playerHealth != null && playerHealth.IsDead)) return;
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

        Vector3 hitPoint = TraceShot(ray);

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
        if (!IsGunEquipped || Time.timeScale <= 0f || (playerHealth != null && playerHealth.IsDead)) return;
        if (!runtime.CanReload(weaponData)) return;
        reloadRoutine = StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        runtime.StartReload();
        PushAnimatorState();

        if (animator != null && !string.IsNullOrEmpty(reloadTrigger))
            animator.SetTrigger(reloadTrigger);

        yield return new WaitForSeconds(weaponData.reloadTime);

        runtime.FinishReload(weaponData);
        reloadRoutine = null;
        PushAnimatorState();
    }

    private Vector3 TraceShot(Ray ray)
    {
        Vector3 end = ray.GetPoint(weaponData.range);
        RaycastHit[] hits = Physics.RaycastAll(ray, weaponData.range, weaponData.hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        var targets = new System.Collections.Generic.HashSet<EnemyHealth>();
        int limit = skills != null && skills.HasPiercing ? 2 : 1;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null && (enemy.IsDead || !targets.Add(enemy))) continue;
            float multiplier = (skills != null ? skills.GunDamageMultiplier : 1f) * (targets.Count > 1 ? 0.5f : 1f);
            hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(weaponData.damage * multiplier, hit.point, ray.direction);
            if (weaponVFX != null)
            {
                weaponVFX.PlayHitEffect(hit);
                weaponVFX.SpawnBulletHole(hit);
            }
            // Only living enemies can be penetrated, never walls or other scenery.
            if (enemy == null || targets.Count >= limit) return hit.point;
        }
        return end;
    }

    public void CancelReload()
    {
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        runtime.CancelReload();
        PushAnimatorState();
    }

    public void ResetAmmoForStage()
    {
        if (weaponData == null) return;
        CancelReload();
        stageAmmoCapacity = ammoPerStage + (skills != null ? skills.ConsumeStageAmmoBonus() : 0);
        runtime.ResetAmmo(weaponData, stageAmmoCapacity);
        PushAnimatorState();
    }

    public void RecoverAmmo(int amount) => runtime.AddReserveAmmo(amount, stageAmmoCapacity);

    private void OnDisable() => CancelReload();


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
