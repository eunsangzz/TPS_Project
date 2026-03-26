using UnityEngine;
using System.Collections;

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
    [SerializeField] private float tracerLife = 0.05f;

    [Header("Bullet Hole")]
    [SerializeField] private GameObject bulletHolePrefab;
    [SerializeField] private float bulletHoleLife = 20f;
    [SerializeField] private float bulletHoleOffset = 0.002f;
    [SerializeField] private bool parentToHitObject = true;

    public void PlayMuzzleFlash()
    {
        if (muzzleFlash != null) muzzleFlash.Play();
    }

    public void PlayTracer(Vector3 hitPoint)
    {
        if (tracer == null || muzzle == null) return;

        tracer.gameObject.SetActive(true);
        tracer.positionCount = 2;
        tracer.SetPosition(0, muzzle.position);
        tracer.SetPosition(1, hitPoint);

        StopAllCoroutines();
        StartCoroutine(HideTracerRoutine());
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

    private IEnumerator HideTracerRoutine()
    {
        yield return new WaitForSeconds(tracerLife);

        if(tracer != null)
        {
            tracer.gameObject.SetActive(false);
        }
    }

}
