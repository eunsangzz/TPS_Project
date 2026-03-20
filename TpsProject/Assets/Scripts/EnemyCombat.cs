using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [Header("References")]
    public Transform self;
    public Transform player;

    [Header("Type")]
    public EnemyType enemyType = EnemyType.Melee;

    [Header("Melee")]
    public float meleeRange = 1.2f;
    public float meleeDamage = 10f;
    public float meleeAttackRate = 1.5f;

    [Header("Ranged")]
    public float rangedRange = 18f;
    public float rangedDamage = 12f;
    public float rangedAttackRate = 0.6f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 25f;

    private float nextAttackTime;

    private void Awake()
    {
        if (self == null) self = transform;
    }

    public float GetAttackRange()
    {
        return enemyType == EnemyType.Melee ? meleeRange : rangedRange;
    }

    public float GetAttackInterval()
    {
        float rate = enemyType == EnemyType.Melee ? meleeAttackRate : rangedAttackRate;
        return 1f / Mathf.Max(0.01f, rate);
    }

    public bool CanAttackNow()
    {
        return Time.time >= nextAttackTime;
    }

    public void MarkAttackUsed()
    {
        nextAttackTime = Time.time + GetAttackInterval();
    }

    public void TryAttack()
    {
        if (player == null) return;

        if(enemyType == EnemyType.Melee)
        {
            if(Vector3.Distance(self.position, player.position) <= meleeRange + 0.1f)
            {
                player.GetComponent<PlayerHealth>()?.TakeDamage(meleeDamage);
            }
        }
        else
        {
            if (projectilePrefab != null && firePoint != null)
            {
                GameObject bullet = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);

                Rigidbody rb = bullet.GetComponent<Rigidbody>();
                if(rb != null)
                {
                    Vector3 dir = (player.position + Vector3.up * 1.2f - firePoint.position).normalized;
                    rb.linearVelocity = dir * projectileSpeed;
                }
            }
            else
            {
                player.GetComponent<PlayerHealth>()?.TakeDamage(rangedDamage);
            }
        }
    }
}
