using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(BoxCollider))]
public class PatrolArea : MonoBehaviour
{
    private BoxCollider box;

    private void Awake()
    {
        EnsureBox();
        box.isTrigger = true;
    }

    public Vector3 GetRandomPoint(float edgeInsetRatio = 0f)
    {
        EnsureBox();

        float inset = Mathf.Clamp(edgeInsetRatio, 0f, 0.45f);
        Vector3 usableSize = box.size;
        usableSize.x *= 1f - inset * 2f;
        usableSize.z *= 1f - inset * 2f;

        Vector3 local = new Vector3(
            Random.Range(-0.5f, 0.5f) * usableSize.x,
            0f,
            Random.Range(-0.5f, 0.5f) * usableSize.z
            );

        Vector3 world = transform.TransformPoint(box.center + local);
        return world;
    }

    public bool TryGetRandomNavMeshPoint(float edgeInsetRatio, float sampleRadius, out Vector3 point)
    {
        Vector3 randomPoint = GetRandomPoint(edgeInsetRatio);
        if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas))
        {
            point = hit.position;
            return true;
        }

        point = randomPoint;
        return false;
    }

    private void EnsureBox()
    {
        if (box == null)
            box = GetComponent<BoxCollider>();
    }
}
