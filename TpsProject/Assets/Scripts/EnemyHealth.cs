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
    [SerializeField] private Animator animator;
    [SerializeField] private string dieTrigger = "Die";
    [SerializeField] private string isDeadBool = "IsDead";

    private float lastHitTime;
    private bool isDead;

    public bool IsDead => isDead || currentHealth <= 0f;
    public bool HasTakenDamage { get; private set; }

    private void Awake()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        if (animator == null) animator = GetComponentInChildren<Animator>();
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

        GetComponent<EnemyCombat>()?.CancelPendingAttack();
        GetComponentInChildren<DodgeRollAnimation>()?.Cancel();

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            // Upper-body aim layers would otherwise cover the full-body death clip.
            for (int i = 1; i < animator.layerCount; i++) animator.SetLayerWeight(i, 0f);
            if (HasAnimatorParameter(isDeadBool, AnimatorControllerParameterType.Bool)) animator.SetBool(isDeadBool, true);
            if (HasAnimatorParameter(dieTrigger, AnimatorControllerParameterType.Trigger)) animator.SetTrigger(dieTrigger);
            else if (animator.HasState(0, Animator.StringToHash("Base Layer.Die"))) animator.Play("Base Layer.Die", 0, 0f);
        }

        var cols = GetComponentsInChildren<Collider>();
        foreach (var c in cols) c.enabled = false;

        Died?.Invoke(this);
        Destroy(gameObject, destroyDelay);
    }

    private bool HasAnimatorParameter(string parameter, AnimatorControllerParameterType type)
    {
        if (animator == null || string.IsNullOrEmpty(parameter)) return false;
        foreach (AnimatorControllerParameter candidate in animator.parameters)
            if (candidate.name == parameter && candidate.type == type) return true;
        return false;
    }
}
