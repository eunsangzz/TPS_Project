using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class PatrolArea : MonoBehaviour
{
    private BoxCollider box;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();
        box.isTrigger = true;
    }

    public Vector3 GetRandomPoint()
    {
        Vector3 local = new Vector3(
            Random.Range(-0.5f, 0.5f) * box.size.x,
            0f,
            Random.Range(-0.5f, 0.5f) * box.size.z
            );

        Vector3 world = transform.TransformPoint(box.center + local);
        return world;
    }
}
