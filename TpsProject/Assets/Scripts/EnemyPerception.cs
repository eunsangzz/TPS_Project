using UnityEngine;

public class EnemyPerception : MonoBehaviour
{
    [Header("References")]
    public Transform self;
    public Transform head;
    public Transform player;

    [Header("Vision")]
    public float viewDistance = 15f;
    [Range(10f, 180f)]
    public float viewAngleTotal = 100f;
    public float rangedDetectDistance = 25f;
    public LayerMask obstacleMask = ~0;
    public bool useLineOfSight = true;

    private void Awake()
    {
        if (self == null) self = transform;
        if (head == null) head = transform;
    }

    public bool CanSeePlayer(bool isRanged)
    {
        if (player == null || head == null) return false;

        Vector3 origin = head.position;
        Vector3 toPlayer = (player.position + Vector3.up * 1.2f) - origin;

        float maxDist = isRanged ? rangedDetectDistance : viewDistance;
        if (toPlayer.sqrMagnitude > maxDist * maxDist) return false;

        Vector3 dir = toPlayer.normalized;

        float half = viewAngleTotal * 0.5f;
        float angle = Vector3.Angle(head.forward, dir);
        if (angle > half) return false;

        if (!useLineOfSight) return true;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return hit.transform == player || hit.transform.IsChildOf(player);
            
        }

        return false;
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
        float half = viewAngleTotal * 0.5f;

        Vector3 forward = (head != null ? head.forward : transform.forward);
        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * forward;
        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + forward * dist);
        Gizmos.DrawLine(origin, origin + leftDir * dist);
        Gizmos.DrawLine(origin, origin + rightDir * dist);
    }
}
