using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    //만약 투사체 프리팹을 쓸거면 사용할것
    public float lifeTime = 5f;
    public float damage = 10f;

    private void Start()
    {
        Destroy(gameObject, lifeTime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.CompareTag("Player"))
            collision.transform.GetComponent<PlayerHealth>()?.TakeDamage(damage);

        Destroy(gameObject);
    }
}
