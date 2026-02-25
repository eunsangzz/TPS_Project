using UnityEngine;

public class CoverDetector : MonoBehaviour
{
    [Header("Detection")]
    public LayerMask coverMask;
    public float detectDistance = 1.2f;
    public float chestHeight = 1.2f;
    public float sphereRadius = 0.25f;

    [Header("Cover Placement")]
    public float coverOffset = 0.35f;
    public float minWallDotUp = 0.2f;

    [Header("Debug")]
    public bool drawDebug = true;

    public bool HasCover { get; private set; }
    public RaycastHit Hit { get; private set; }
    public Vector3 CoverNormal { get; private set; }
    public Vector3 CoverPoint { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public Vector3 CoverRight { get; private set; }

    public void TickDetect(Vector3 forward)
    {
        Vector3 origin = transform.position + Vector3.up * chestHeight;

        bool hit = Physics.SphereCast(
            origin,
            sphereRadius,
            forward,
            out RaycastHit hitInfo,
            detectDistance,
            coverMask,
            QueryTriggerInteraction.Ignore);

        if (!hit)
        {
            Clear();
            if (drawDebug) Debug.DrawRay(origin, forward * detectDistance, Color.red);
            return;
        }

        Vector3 n = hitInfo.normal.normalized;
        float dotUp = Mathf.Abs(Vector3.Dot(n, Vector3.up));

        if (dotUp > (1f - minWallDotUp)) 
        {
            Clear();
            return;
        }

        HasCover = true;
        Hit = hitInfo;
        CoverNormal = n;
        CoverPoint = hitInfo.point;

        TargetPosition = hitInfo.point + n * coverOffset;

        CoverRight = Vector3.Cross(Vector3.up, CoverNormal).normalized;

        if (drawDebug)
        {
            Debug.DrawRay(origin, forward * detectDistance, Color.green);
            Debug.DrawRay(hitInfo.point, hitInfo.normal, Color.cyan);
            Debug.DrawRay(TargetPosition, Vector3.up * 0.5f, Color.yellow);
        }
    }

    public void Clear()
    {
        HasCover = false;
        CoverNormal = Vector3.zero;
        CoverPoint = Vector3.zero;
        TargetPosition = Vector3.zero;
        CoverRight = Vector3.zero;
    }

}
