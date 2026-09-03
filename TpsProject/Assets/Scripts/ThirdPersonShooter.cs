using System.Collections;
using UnityEngine;

public class ThirdPersonShooter : MonoBehaviour
{
    // One notification per real shot (not per shotgun pellet or trigger press).
    public static event System.Action<ThirdPersonShooter, Vector3> ShotFired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetShotListeners() => ShotFired = null;

    public enum FireMode { Single, Auto }

    [Header("Reference")]
    [SerializeField] private ThirdPersonInput input;
    [SerializeField] private Camera shooterCamera;
    [SerializeField] private Animator animator;
    [SerializeField] private WeaponVFX weaponVFX;
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;
    [SerializeField] private WeaponData shotgunData;
    [SerializeField] private WeaponData sniperData;
    [SerializeField, Min(0)] private int ammoPerStage = 90;
    [SerializeField, Min(0)] private int shotgunAmmoPerStage = 32;
    [SerializeField, Min(0)] private int sniperAmmoPerStage = 10;
    [SerializeField] private bool requireAimToShoot = false;

    [Header("Weapon Audio")]
    [SerializeField, Range(0f, 1f)] private float fireVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float reloadVolume = 0.7f;
    private GameObject weaponAudioRoot;
    private AudioSource fireAudioSource;
    private AudioSource reloadAudioSource;

    [Header("Animator")]
    [SerializeField] private string fireSingleTrigger = "FireSingle";
    [SerializeField] private string fireAutoTrigger = "FireAuto";
    [SerializeField] private string reloadTrigger = "Reload";
    [SerializeField] private string fireModeIntParam = ""; 
    [SerializeField] private string isReloadingBoolParam = "";

    [SerializeField] private WeaponRuntime runtime = new WeaponRuntime();
    [SerializeField] private WeaponRuntime shotgunRuntime = new WeaponRuntime();
    [SerializeField] private WeaponRuntime sniperRuntime = new WeaponRuntime();
    private Coroutine reloadRoutine;
    private PlayerLoadout loadout;
    private PlayerSkills skills;
    private int stageAmmoCapacity;
    private int shotgunStageAmmoCapacity;
    private int sniperStageAmmoCapacity;

    private bool ShotgunSelected => loadout != null && loadout.IsShotgunEquipped;
    private bool SniperSelected => loadout != null && loadout.IsSniperEquipped;
    private WeaponData ActiveWeaponData => SniperSelected ? sniperData : ShotgunSelected ? shotgunData : weaponData;
    private WeaponRuntime ActiveRuntime => SniperSelected ? sniperRuntime : ShotgunSelected ? shotgunRuntime : runtime;

    public int AmmoInMag => ActiveRuntime.AmmoInMag;
    public int ReserveAmmo => ActiveRuntime.ReserveAmmo;
    public bool InfiniteReserveAmmo => ActiveRuntime.InfiniteReserveAmmo;
    public bool IsReloading => ActiveRuntime.IsReloading;
    public WeaponData.FireMode CurrentFireMode => ActiveRuntime.CurrentFireMode;
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

        if (sniperData == null) sniperData = Resources.Load<WeaponData>("SniperWeaponData");
        if (sniperData == null)
        {
            Debug.LogError("[ThirdPersonShooter] SniperWeaponData resource is missing.", this);
            enabled = false;
            return;
        }
        EnsureShotgunData();
        InitializeWeaponAudio();

        skills = GetComponent<PlayerSkills>();
        if (skills == null) skills = gameObject.AddComponent<PlayerSkills>();
        runtime.Initialize(weaponData, 0, false);
        shotgunRuntime.Initialize(shotgunData, 0, false);
        sniperRuntime.Initialize(sniperData, 0, false);
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
        if (!IsGunEquipped || Time.timeScale <= 0f || (loadout != null && !loadout.CanAct)) return;
        if (playerHealth != null && playerHealth.IsDead) return;

        if (input.ToggleFireModePressed) 
            ToggleFireMode();

        if (input.ReloadPressed) 
            TryStartReload();

        WeaponRuntime activeRuntime = ActiveRuntime;
        if (activeRuntime.IsReloading) return;


        ThirdPersonCamera camCtrl = shooterCamera != null ? shooterCamera.GetComponentInParent<ThirdPersonCamera>() : null;
        bool isAiming = (camCtrl != null && camCtrl.IsAiming);
        bool canShootByAim = !requireAimToShoot || isAiming;

        if (!canShootByAim) return;

        bool wantsShoot =
            activeRuntime.CurrentFireMode == WeaponData.FireMode.Auto ? input.FireHeld :
            activeRuntime.CurrentFireMode == WeaponData.FireMode.Single ? input.FirePressed :
            false;

        if (!wantsShoot) return;

        if (activeRuntime.AmmoInMag <= 0) return;

        TryShoot();
    }

    private void ToggleFireMode()
    {
        if (ShotgunSelected || SniperSelected) return;
        ActiveRuntime.ToggleFireMode();
        PushAnimatorState();
    }

    private void PushAnimatorState()
    {
        if (animator == null || weaponData == null) return;

        if (!string.IsNullOrEmpty(fireModeIntParam))
            animator.SetInteger(fireModeIntParam, ActiveRuntime.CurrentFireMode == WeaponData.FireMode.Single ? 0 : 1);

        if (!string.IsNullOrEmpty(isReloadingBoolParam))
            animator.SetBool(isReloadingBoolParam, ActiveRuntime.IsReloading);
    }

    private void TryShoot()
    {
        if (!IsGunEquipped || Time.timeScale <= 0f || (loadout != null && !loadout.CanAct) || (playerHealth != null && playerHealth.IsDead)) return;
        WeaponRuntime activeRuntime = ActiveRuntime;
        WeaponData activeData = ActiveWeaponData;
        if (!activeRuntime.CanFire(Time.time)) return;

        activeRuntime.SetNextFireTime(Time.time, activeData.fireRate);
        ShootOnce();
    }

    private void ShootOnce()
    {
        WeaponRuntime activeRuntime = ActiveRuntime;
        WeaponData activeData = ActiveWeaponData;
        activeRuntime.ConsumeAmmo();
        if (activeData.fireClip != null && fireAudioSource != null)
            fireAudioSource.PlayOneShot(activeData.fireClip, fireVolume);
        ShotFired?.Invoke(this, transform.position);

        ThirdPersonCamera camCtrl = shooterCamera.GetComponentInParent<ThirdPersonCamera>();
        bool isAiming = camCtrl != null && camCtrl.IsAiming;
        bool isScoped = camCtrl != null && camCtrl.IsScoped;

        float spreadDeg =
            isScoped ? activeData.scopeSpread :
            isAiming ? activeData.shoulderSpread :
            activeData.hipSpread;

        int pelletCount = Mathf.Max(1, activeData.pelletCount);
        var hitPoints = new System.Collections.Generic.List<Vector3>(pelletCount);
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 dir = ApplySpread(shooterCamera.transform.forward, spreadDeg);
            Ray ray = new Ray(shooterCamera.transform.position, dir);
            hitPoints.Add(TraceShot(ray, activeData));
        }

        if (weaponVFX != null)
        {
            weaponVFX.PlayMuzzleFlash();
            if (pelletCount > 1) weaponVFX.PlayTracerBurst(hitPoints);
            else weaponVFX.PlayTracer(hitPoints[0]);
        }

        if (animator != null)
        {
            string trig = activeRuntime.CurrentFireMode == WeaponData.FireMode.Single
                ? fireSingleTrigger
                : fireAutoTrigger;

            if (!string.IsNullOrEmpty(trig))
                animator.SetTrigger(trig);
        }

        if (camCtrl != null)
            camCtrl.AddRecoil();

    }

    private void TryStartReload()
    {
        if (!IsGunEquipped || Time.timeScale <= 0f || (loadout != null && !loadout.CanAct) || (playerHealth != null && playerHealth.IsDead)) return;
        WeaponRuntime activeRuntime = ActiveRuntime;
        WeaponData activeData = ActiveWeaponData;
        if (!activeRuntime.CanReload(activeData)) return;
        reloadRoutine = StartCoroutine(ReloadRoutine(activeRuntime, activeData));
    }

    private IEnumerator ReloadRoutine(WeaponRuntime reloadingRuntime, WeaponData reloadingData)
    {
        reloadingRuntime.StartReload();
        PushAnimatorState();

        if (reloadAudioSource != null && reloadingData.reloadClip != null)
        {
            reloadAudioSource.clip = reloadingData.reloadClip;
            reloadAudioSource.volume = reloadVolume;
            reloadAudioSource.Play();
        }

        if (animator != null && !string.IsNullOrEmpty(reloadTrigger))
            animator.SetTrigger(reloadTrigger);

        yield return new WaitForSeconds(reloadingData.reloadTime);

        reloadingRuntime.FinishReload(reloadingData);
        StopReloadAudio();
        reloadRoutine = null;
        PushAnimatorState();
    }

    private Vector3 TraceShot(Ray ray, WeaponData data)
    {
        Vector3 end = ray.GetPoint(data.range);
        RaycastHit[] hits = Physics.RaycastAll(ray, data.range, data.hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        var targets = new System.Collections.Generic.HashSet<EnemyHealth>();
        int limit = skills != null && skills.HasPiercing ? 2 : 1;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null && (enemy.IsDead || !targets.Add(enemy))) continue;
            float multiplier = (skills != null ? skills.GunDamageMultiplier : 1f) * (targets.Count > 1 ? 0.5f : 1f);
            hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(data.damage * multiplier, hit.point, ray.direction);
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
        StopReloadAudio();
        if (reloadRoutine != null) StopCoroutine(reloadRoutine);
        reloadRoutine = null;
        runtime.CancelReload();
        shotgunRuntime.CancelReload();
        sniperRuntime.CancelReload();
        PushAnimatorState();
    }

    public void ResetAmmoForStage()
    {
        if (weaponData == null || shotgunData == null || sniperData == null) return;
        CancelReload();
        stageAmmoCapacity = ammoPerStage + (skills != null ? skills.ConsumeStageAmmoBonus() : 0);
        shotgunStageAmmoCapacity = shotgunAmmoPerStage;
        sniperStageAmmoCapacity = sniperAmmoPerStage;
        runtime.ResetAmmo(weaponData, stageAmmoCapacity);
        shotgunRuntime.ResetAmmo(shotgunData, shotgunStageAmmoCapacity);
        sniperRuntime.ResetAmmo(sniperData, sniperStageAmmoCapacity);
        PushAnimatorState();
    }

    public void RecoverAmmo(int amount)
    {
        if (SniperSelected) sniperRuntime.AddReserveAmmo(amount, sniperStageAmmoCapacity);
        else if (ShotgunSelected) shotgunRuntime.AddReserveAmmo(amount, shotgunStageAmmoCapacity);
        else runtime.AddReserveAmmo(amount, stageAmmoCapacity);
    }

    private void InitializeWeaponAudio()
    {
        if (weaponAudioRoot != null) return;
        weaponAudioRoot = new GameObject("WeaponAudio");
        weaponAudioRoot.transform.SetParent(transform, false);
        fireAudioSource = weaponAudioRoot.AddComponent<AudioSource>();
        reloadAudioSource = weaponAudioRoot.AddComponent<AudioSource>();
        foreach (AudioSource source in new[] { fireAudioSource, reloadAudioSource })
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }
    }

    private void StopReloadAudio()
    {
        if (reloadAudioSource == null) return;
        reloadAudioSource.Stop();
        reloadAudioSource.clip = null;
    }

    private void OnDisable()
    {
        CancelReload();
        if (fireAudioSource != null) fireAudioSource.Stop();
    }

    private void OnDestroy()
    {
        if (weaponAudioRoot != null) Destroy(weaponAudioRoot);
    }

    private void EnsureShotgunData()
    {
        if (shotgunData != null) return;
        shotgunData = Resources.Load<WeaponData>("ShotgunWeaponData");
        if (shotgunData != null) return;
        shotgunData = ScriptableObject.CreateInstance<WeaponData>();
        shotgunData.name = "Runtime Shotgun";
        shotgunData.damage = 15f;
        shotgunData.fireRate = 1.2f;
        shotgunData.range = 80f;
        shotgunData.pelletCount = 4;
        shotgunData.hitMask = weaponData.hitMask;
        shotgunData.magazineSize = 8;
        shotgunData.reloadTime = 2.4f;
        shotgunData.hipSpread = 7f;
        shotgunData.shoulderSpread = 4f;
        shotgunData.scopeSpread = 2f;
        shotgunData.fireMode = WeaponData.FireMode.Single;
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
