using UnityEngine;

public class EnemyPerception : MonoBehaviour
{
    [Header("References")]
    public Transform self;
    public Transform head;
    public Transform player;

    [Header("Vision")]
    public float viewDistance = 15f;
    [Range(10f, 360f)]
    public float viewAngleTotal = 100f;
    [Range(10f, 360f)] public float horizontalViewAngleTotal = 100f;
    [Range(0f, 89f)] public float verticalViewAngleUp = 70f;
    [Range(0f, 89f)] public float verticalViewAngleDown = 70f;
    public float rangedDetectDistance = 25f;
    public LayerMask obstacleMask = ~0;
    public bool useLineOfSight = true;
    public float eyeHeight = 1.2f;

    private void Awake()
    {
        if (self == null) self = transform;
        if (head == null) head = transform;
    }

    public bool CanSeePlayer(bool isRanged)
    {
        if (player == null || head == null) return false;

        Vector3 origin = head.position;
        Vector3 playerEye = player.position + Vector3.up * eyeHeight;
        Vector3 toPlayer = playerEye - origin;

        float maxDist = isRanged ? rangedDetectDistance : viewDistance;
        if (toPlayer.sqrMagnitude > maxDist * maxDist) return false;

        Vector3 dir = toPlayer.normalized;

        if (!IsInsideViewCone(dir))
        {
            return false;
        }

        if (!useLineOfSight) return true;

        return HasLineOfSight(origin, dir, toPlayer.magnitude);
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 direction, float distance)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, distance, obstacleMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            Transform hitTransform = hit.transform;
            if (hitTransform == null) continue;
            if (self != null && (hitTransform == self || hitTransform.IsChildOf(self))) continue;

            return hitTransform == player || hitTransform.IsChildOf(player);
        }

        return true;
    }

    private bool IsInsideViewCone(Vector3 worldDir)
    {
        Transform basis = head != null ? head : transform;
        Vector3 forward = basis.forward;

        if (horizontalViewAngleTotal < 359.9f)
        {
            Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
            Vector3 flatDir = Vector3.ProjectOnPlane(worldDir, Vector3.up);

            if (flatForward.sqrMagnitude < 0.0001f) flatForward = transform.forward;
            if (flatDir.sqrMagnitude < 0.0001f) return true;

            float horizontalAngle = Vector3.Angle(flatForward.normalized, flatDir.normalized);
            if (horizontalAngle > horizontalViewAngleTotal * 0.5f) return false;
        }

        Vector3 localDir = basis.InverseTransformDirection(worldDir);
        float verticalAngle = Mathf.Atan2(localDir.y, new Vector2(localDir.x, localDir.z).magnitude) * Mathf.Rad2Deg;

        if (verticalAngle > verticalViewAngleUp) return false;
        if (verticalAngle < -verticalViewAngleDown) return false;

        return true;
    }

    public float DistanceToPlayer()
    {
        if (player == null || self == null) return float.MaxValue;
        return Vector3.Distance(self.position, player.position);
    }

    public Vector3 PlayerPosition()
    {
        return player != null ? player.position : Vector3.zero;
    }

    public Vector3 PlayerAimPoint()
    {
        return player != null ? player.position + Vector3.up * 1.2f : Vector3.zero;
    }

    public bool HasPlayer()
    {
        return player != null;
    }

    private void OnDrawGizmos()
    {
        if (self == null) self = transform;
        if (head == null) head = transform;

        float dist = viewDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dist);

        Vector3 origin = head.position + Vector3.up * 0.1f;
        float half = horizontalViewAngleTotal * 0.5f;

        Vector3 forward = (head != null ? head.forward : transform.forward);
        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * forward;
        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + forward * dist);
        Gizmos.DrawLine(origin, origin + leftDir * dist);
        Gizmos.DrawLine(origin, origin + rightDir * dist);
    }
}
