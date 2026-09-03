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
    private Collider[] muzzleOverlapBuffer = new Collider[16];
    private bool shotDamagedEnemy;
    private bool ownsShotgunData;

    private WeaponData.Stats StatsFor(WeaponData data)
    {
        PlayerSkill upgrade = data == sniperData ? PlayerSkill.SniperUpgrade :
            data == shotgunData ? PlayerSkill.ShotgunUpgrade : PlayerSkill.RifleUpgrade;
        return data.GetStats(skills != null ? skills.Level(upgrade) : 0);
    }

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

        activeRuntime.SetNextFireTime(Time.time, StatsFor(activeData).FireRate);
        ShootOnce();
    }

    private void ShootOnce()
    {
        WeaponRuntime activeRuntime = ActiveRuntime;
        WeaponData activeData = ActiveWeaponData;
        activeRuntime.ConsumeAmmo();
        WeaponData.Stats stats = StatsFor(activeData);
        shotDamagedEnemy = false;
        if (activeData.fireClip != null && fireAudioSource != null)
            fireAudioSource.PlayOneShot(activeData.fireClip, fireVolume);
        ShotFired?.Invoke(this, transform.position);

        ThirdPersonCamera camCtrl = shooterCamera.GetComponentInParent<ThirdPersonCamera>();
        bool isAiming = camCtrl != null && camCtrl.IsAiming;
        bool isScoped = camCtrl != null && camCtrl.IsScoped;

        float spreadDeg =
            isScoped ? stats.ScopeSpread :
            isAiming ? stats.ShoulderSpread :
            stats.HipSpread;

        int pelletCount = stats.PelletCount;
        Vector3 origin = weaponVFX != null ? weaponVFX.MuzzlePosition :
            transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
        Vector3 aimPoint = FindAimPoint(origin, activeData);
        Vector3 aimDirection = (aimPoint - origin).normalized;
        if (aimDirection.sqrMagnitude < 0.0001f) aimDirection = shooterCamera.transform.forward;
        bool muzzleBlocked = IsMuzzleInsideObstacle(origin, activeData);
        var hitPoints = new System.Collections.Generic.List<Vector3>(pelletCount);
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 dir = ApplySpread(aimDirection, spreadDeg);
            Ray ray = new Ray(origin, dir);
            hitPoints.Add(muzzleBlocked ? origin : TraceShot(ray, activeData));
        }
        if (shotDamagedEnemy) skills?.OnAttackHit();

        if (weaponVFX != null)
        {
            weaponVFX.PlayMuzzleFlash();
            if (pelletCount > 1) weaponVFX.PlayTracerBurstFromMuzzle(origin, hitPoints);
            else weaponVFX.PlayTracerFromMuzzle(origin, hitPoints[0]);
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
        if (!activeRuntime.CanReload(StatsFor(activeData).MagazineSize)) return;
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

        yield return new WaitForSeconds(StatsFor(reloadingData).ReloadTime);

        reloadingRuntime.FinishReload(StatsFor(reloadingData).MagazineSize);
        StopReloadAudio();
        reloadRoutine = null;
        PushAnimatorState();
    }

    private Vector3 FindAimPoint(Vector3 muzzlePosition, WeaponData data)
    {
        Ray viewRay = shooterCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        // The camera sits behind the player. Ignore targets behind the muzzle plane
        // so a nearby camera obstruction cannot turn the shot back toward the player.
        float muzzleDepth = Mathf.Max(0f, Vector3.Dot(muzzlePosition - viewRay.origin, viewRay.direction));
        float closestDistance = muzzleDepth + data.range;
        Vector3 aimPoint = viewRay.GetPoint(closestDistance);
        foreach (RaycastHit hit in Physics.RaycastAll(viewRay, closestDistance, data.hitMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.distance <= muzzleDepth || hit.distance >= closestDistance || IsOwnCollider(hit.collider)) continue;
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null && enemy.IsDead) continue;
            closestDistance = hit.distance;
            aimPoint = hit.point;
        }
        return aimPoint;
    }

    private bool IsMuzzleInsideObstacle(Vector3 origin, WeaponData data)
    {
        // Raycasts miss surfaces when starting inside them. A tiny overlap probe
        // prevents a muzzle clipped into a wall from firing through that wall.
        int count;
        while (true)
        {
            count = Physics.OverlapSphereNonAlloc(origin, 0.01f, muzzleOverlapBuffer, data.hitMask, QueryTriggerInteraction.Ignore);
            if (count < muzzleOverlapBuffer.Length) break;
            System.Array.Resize(ref muzzleOverlapBuffer, muzzleOverlapBuffer.Length * 2);
        }
        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = muzzleOverlapBuffer[i];
            if (!IsOwnCollider(obstacle) && obstacle.GetComponentInParent<IDamageable>() == null)
                blocked = true;
        }
        System.Array.Clear(muzzleOverlapBuffer, 0, count);
        return blocked;
    }

    private bool IsOwnCollider(Collider collider) => collider == null ||
        collider.transform == transform || collider.transform.IsChildOf(transform);

    private Vector3 TraceShot(Ray ray, WeaponData data)
    {
        Vector3 end = ray.GetPoint(data.range);
        RaycastHit[] hits = Physics.RaycastAll(ray, data.range, data.hitMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        var targets = new System.Collections.Generic.HashSet<EnemyHealth>();
        int limit = skills != null && skills.HasPiercing ? 2 : 1;
        foreach (RaycastHit hit in hits)
        {
            if (IsOwnCollider(hit.collider)) continue;
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null && (enemy.IsDead || !targets.Add(enemy))) continue;
            float multiplier = targets.Count > 1 ? 0.5f : 1f;
            float healthBefore = enemy != null ? enemy.currentHealth : 0f;
            hit.collider.GetComponentInParent<IDamageable>()?.TakeDamage(StatsFor(data).Damage * multiplier, hit.point, ray.direction);
            if (enemy != null && enemy.currentHealth < healthBefore) shotDamagedEnemy = true;
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
        runtime.ResetAmmo(StatsFor(weaponData).MagazineSize, stageAmmoCapacity);
        shotgunRuntime.ResetAmmo(StatsFor(shotgunData).MagazineSize, shotgunStageAmmoCapacity);
        sniperRuntime.ResetAmmo(StatsFor(sniperData).MagazineSize, sniperStageAmmoCapacity);
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
        if (ownsShotgunData && shotgunData != null) Destroy(shotgunData);
    }

    private void EnsureShotgunData()
    {
        if (shotgunData != null) return;
        shotgunData = Resources.Load<WeaponData>("ShotgunWeaponData");
        if (shotgunData != null) return;
        shotgunData = ScriptableObject.CreateInstance<WeaponData>();
        ownsShotgunData = true;
        shotgunData.name = "Runtime Shotgun";
        shotgunData.damage = 15f;
        shotgunData.fireRate = 1.2f;
        shotgunData.range = 80f;
        shotgunData.pelletCount = 4;
        shotgunData.hitMask = weaponData.hitMask;
        shotgunData.magazineSize = 8;
        shotgunData.reloadTime = 3.6f;
        shotgunData.hipSpread = 18f;
        shotgunData.shoulderSpread = 14f;
        shotgunData.scopeSpread = 14f;
        shotgunData.upgradedDamage = 15f;
        shotgunData.upgradedFireRate = 1.2f;
        shotgunData.upgradedMagazineSize = 8;
        shotgunData.upgradedPelletCount = 7;
        shotgunData.upgradedReloadTime = 2.4f;
        shotgunData.upgradedHipSpread = 7f;
        shotgunData.upgradedShoulderSpread = 4f;
        shotgunData.upgradedScopeSpread = 2f;
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
