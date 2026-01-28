using UnityEngine;

public class ThirdPersonShooter : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private ThirdPersonInput input;
    [SerializeField] private Camera shooterCamera;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform muzzle;

    [Header("Shooting")]
    [SerializeField] private float fireRate = 10f;
    [SerializeField] private float range = 200f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool requireAimToShoot = false;

    [Header("Spread")]
    [SerializeField] private float hipSpread = 2.5f;
    [SerializeField] private float shoulderSpread = 1.2f;
    [SerializeField] private float scopeSpread = 0.25f;

    [Header("Damage")]
    [SerializeField] private float damage = 20f;

    [Header("VFX")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject hitEffectPrefab; //¿Ã∆Â∆Æ
    [SerializeField] private float hitEffectLife = 1.5f;
    [SerializeField] private LineRenderer tracer;
    [SerializeField] private float tracerLife = 0.05f;

    [Header("Bullet Hole")]
    [SerializeField] private GameObject bulletHoleProfab;
    [SerializeField] private float bulletHoleLife = 20f;
    [SerializeField] private float bullstHoleOffset = 0.002f;
    [SerializeField] private bool parentToHitObject = true;

    private float nextFireTime;

    private void Awake()
    {
        if (input == null) input = GetComponent<ThirdPersonInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (shooterCamera == null) if (Camera.main != null) shooterCamera = Camera.main;
    }

    private void Update()
    {
        if (input == null || shooterCamera == null) return;

        ThirdPersonCamera camCtrl = shooterCamera != null ? shooterCamera.GetComponentInParent<ThirdPersonCamera>() : null;
        bool isAiming = (camCtrl != null && camCtrl.IsAiming);
        bool canShootByAim = !requireAimToShoot || isAiming;

        if (canShootByAim && input.FireHeld) TryShoot();
    }

    private void TryShoot()
    {
        if (Time.time < nextFireTime) return;
        nextFireTime = Time.time + (1f / fireRate);

        ShootOnce();
    }

    private void ShootOnce()
    {
        ThirdPersonCamera camCtr1 =
            shooterCamera != null ? shooterCamera.GetComponentInParent<ThirdPersonCamera>() : null;

        bool isAiming = (camCtr1 != null && camCtr1.IsAiming);
        bool isScoped = (camCtr1 != null && camCtr1.IsScoped);

        float spreadDeg =
            isScoped ? scopeSpread :
            (isAiming ? shoulderSpread : hipSpread);

        Vector3 dir = ApplySpread(shooterCamera.transform.forward, spreadDeg);
        Ray ray = new Ray(shooterCamera.transform.position, dir);

        Vector3 hitPoint = ray.origin + ray.direction * range;
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore);



        if (hitSomething)
        {
            hitPoint = hit.point;

            var dmg = hit.collider.GetComponentInParent<IDamageable>();
            if (dmg != null) dmg.TakeDamage(damage, hit.point, ray.direction);

            if (hitEffectPrefab != null)
            {
                var fx = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(fx, hitEffectLife);
            }

            if (bulletHoleProfab != null)
            {
                Quaternion rot = Quaternion.LookRotation(hit.normal);

                rot *= Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                Vector3 pos = hit.point + hit.normal * bullstHoleOffset;

                GameObject hole = Instantiate(bulletHoleProfab, pos, rot);

                if (parentToHitObject) hole.transform.SetParent(hit.collider.transform, true);
                if (bulletHoleLife > 0f) Destroy(hole, bulletHoleLife);
            }
        }

        if (muzzleFlash != null) muzzleFlash.Play();

        if (tracer != null && muzzle != null)
        {
            tracer.gameObject.SetActive(true);
            tracer.positionCount = 2;
            tracer.SetPosition(0, muzzle.position);
            tracer.SetPosition(1, hitPoint);
            CancelInvoke(nameof(HideTracer));
            Invoke(nameof(HideTracer), tracerLife);
        }

        if (animator != null) animator.SetTrigger("Fire");

        if (camCtr1 != null) camCtr1.AddRecoil();

    }

    private void HideTracer()
    {
        if (tracer != null) tracer.gameObject.SetActive(false);
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
