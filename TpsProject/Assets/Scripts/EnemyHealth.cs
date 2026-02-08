using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    [Header("Regen")]
    public float regenPerSecond = 2f;
    public float regenDelayAfterHit = 2f;

    private float lastHitTime;

    public bool IsDead => currentHealth <= 0f;

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

    public void TakeDamage(float dmg)
    {
        if (IsDead) return;

        lastHitTime = Time.time;
        currentHealth = Mathf.Max(0f, currentHealth - dmg);

        if (IsDead) Die();
    }

    private void Die()
    {
        Destroy(gameObject, 2f);
    }
}
