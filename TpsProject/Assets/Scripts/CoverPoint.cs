using System.Collections.Generic;
using UnityEngine;

public class CoverPoint : MonoBehaviour
{
    private static readonly List<CoverPoint> all = new List<CoverPoint>();
    public static IReadOnlyList<CoverPoint> All => all;

    [Header("Cover")]
    [SerializeField] private bool useTransformForwardAsNormal = true;
    [SerializeField] private Vector3 manualNormal = Vector3.forward;
    [SerializeField] private float standOffset = 0.7f;

    private void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    public Vector3 GetStandPosition(Vector3 threatPosition)
    {
        Vector3 normal = GetCoverNormal();

        Vector3 toThreat = threatPosition - transform.position;
        toThreat.y = 0f;
        if (toThreat.sqrMagnitude > 0.0001f)
        {
            toThreat.Normalize();
            if (Vector3.Dot(normal, toThreat) > 0f)
                normal = -normal;
        }

        return transform.position + normal * standOffset;
    }

    private Vector3 GetCoverNormal()
    {
        Vector3 normal = useTransformForwardAsNormal ? transform.forward : manualNormal;
        normal.y = 0f;

        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.forward;

        return normal.normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 n = GetCoverNormal();
        Vector3 p = transform.position;

        Gizmos.color = Color.green;
        Gizmos.DrawSphere(p, 0.12f);
        Gizmos.DrawLine(p, p + n * standOffset);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(p + n * standOffset, 0.1f);
    }
}
