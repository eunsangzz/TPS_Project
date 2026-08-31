using UnityEngine;
using UnityEngine.AI;
using System;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public event Action<EnemyHealth> Died;
    public event Action<EnemyHealth, Vector3, Vector3> Damaged;

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
    public bool HasTakenDamage { get; private set; }

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
    }

    private void Start()
    {
        EnemyHealthBar healthBar = GetComponent<EnemyHealthBar>();
        if (healthBar == null) healthBar = gameObject.AddComponent<EnemyHealthBar>();
        healthBar.Initialize(this);
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
        ApplyDamage(amount, hitPoint, hitDirection);
    }

    public void ApplyDamage(float dmg)
    {
        ApplyDamage(dmg, transform.position, Vector3.zero);
    }

    private void ApplyDamage(float dmg, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (IsDead) return;
        if (dmg <= 0f) return;

        lastHitTime = Time.time;
        HasTakenDamage = true;
        currentHealth = Mathf.Max(0f, currentHealth - dmg);
        Damaged?.Invoke(this, hitPoint, hitDirection);

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

        Died?.Invoke(this);
        Destroy(gameObject, destroyDelay);
    }
}
