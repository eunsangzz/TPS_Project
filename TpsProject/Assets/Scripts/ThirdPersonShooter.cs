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

    [Header("Damage")]
    [SerializeField] private float damage = 20f;

    [Header("VFX")]
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject hitEffectPrefab; //¿Ã∆Â∆Æ
    [SerializeField] private float hitEffectLife = 1.5f;
    [SerializeField] private LineRenderer tracer;
    [SerializeField] private float tracerLife = 0.05f;

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
        Ray ray = new Ray(shooterCamera.transform.position, shooterCamera.transform.forward);

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
        
    }

    private void HideTracer()
    {
        if (tracer != null) tracer.gameObject.SetActive(false);
    }
}

public interface IDamageable
{
    void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection);
}
