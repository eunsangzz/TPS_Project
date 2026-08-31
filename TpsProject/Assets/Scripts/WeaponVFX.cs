using UnityEngine;

public class WeaponVFX : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] private Transform muzzle;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private LineRenderer tracer;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;
    [SerializeField] private float hitEffectLife = 1.5f;

    [Header("Tracer")]
    [SerializeField] private float tracerLife = 0.1f;
    [SerializeField] private float tracerWidth = 0.045f;
    [SerializeField] private Color tracerColor = new Color(0.25f, 0.95f, 1f, 1f);

    [Header("Bullet Hole")]
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private float bulletHoleLife = 20f;
    [SerializeField] private float bulletHoleOffset = 0.002f;
    [SerializeField] private bool parentToHitObject = true;

    private float tracerEndTime;
    private float activeTracerLife;
    private Color activeTracerColor;

    public void PlayMuzzleFlash()
    {
        if (muzzleFlash != null) muzzleFlash.Play();
    }

    public void PlayTracer(Vector3 hitPoint)
    {
        Vector3 origin = muzzle != null ? muzzle.position :
            transform.position + Vector3.up * 1.2f + transform.forward * 0.5f;
        PlayTracer(origin, hitPoint, tracerColor);
    }

    public void PlayTracer(Vector3 origin, Vector3 hitPoint, Color color)
    {
        EnsureTracer();
        if (tracer == null) return;

        tracer.gameObject.SetActive(true);
        tracer.enabled = true;
        tracer.useWorldSpace = true;
        tracer.positionCount = 2;
        tracer.SetPosition(0, origin);
        tracer.SetPosition(1, hitPoint);
        activeTracerColor = color;
        tracer.startColor = color;
        tracer.endColor = color;
        activeTracerLife = Mathf.Max(0.08f, tracerLife);
        tracerEndTime = Time.time + activeTracerLife;
    }

    private void EnsureTracer()
    {
        if (tracer != null) return;

        GameObject tracerObject = new GameObject("BulletTracer");
        tracerObject.transform.SetParent(transform, false);
        tracer = tracerObject.AddComponent<LineRenderer>();
        tracer.sharedMaterial = Resources.Load<Material>("BulletTracer");
        tracer.widthMultiplier = Mathf.Max(0.005f, tracerWidth);
        tracer.numCapVertices = 2;
        tracer.alignment = LineAlignment.View;
        tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tracer.receiveShadows = false;
        tracer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        tracer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        tracer.enabled = false;
    }

    private void LateUpdate()
    {
        if (tracer == null || !tracer.enabled) return;

        float remaining = tracerEndTime - Time.time;
        if (remaining <= 0f)
        {
            tracer.enabled = false;
            return;
        }

        Color color = activeTracerColor;
        color.a *= Mathf.Clamp01(remaining / activeTracerLife);
        tracer.startColor = color;
        tracer.endColor = color;
    }

    private void OnDisable()
    {
        if (tracer != null) tracer.enabled = false;
    }

    public void PlayHitEffect(RaycastHit hit)
    {
        if (hitEffectPrefab == null) return;

        var fx = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
        Destroy(fx, hitEffectLife);
    }

    public void SpawnBulletHole(RaycastHit hit)
    {
        if (bulletHolePrefab == null) return;

        Quaternion rot = Quaternion.LookRotation(hit.normal);
        rot *= Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Vector3 pos = hit.point + hit.normal * bulletHoleOffset;
        GameObject hole = Instantiate(bulletHolePrefab, pos, rot);

        if (parentToHitObject)
            hole.transform.SetParent(hit.collider.transform, true);

        if (bulletHoleLife > 0f)
            Destroy(hole, bulletHoleLife);
    }

}
