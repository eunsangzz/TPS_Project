using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Regen")]
    public float regenPerSecond = 2f;
    public float regenDelayAfterHit = 2f;

    [Header("Death")]
    public float destroyDelay = 2f;

    private float lastHitTime;
    private bool isDead;

    public bool IsDead => isDead || currentHealth <= 0f;

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    private void Update()
    {
        if (IsDead) return;

        if(Time.time >= lastHitTime + regenDelayAfterHit)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + regenPerSecond * Time.deltaTime);
        }
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        ApplyDamage(amount);
    }

    public void ApplyDamage(float dmg)
    {
        if (IsDead) return;
        if (dmg <= 0f) return;

        lastHitTime = Time.time;
        currentHealth = Mathf.Max(0f, currentHealth - dmg);

        if (currentHealth <= 0f) Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        var agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;

        Destroy(gameObject, destroyDelay);
    }
}
