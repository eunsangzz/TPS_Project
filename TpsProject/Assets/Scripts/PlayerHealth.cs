using UnityEngine;

public class PlayerHealth : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = false;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string hitTrigger = "Hit";
    [SerializeField] private string dieTrigger = "Die";
    [SerializeField] private string isDeadBool = "IsDead";

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead { get; private set; }

    private void Awake()
    {
        CurrentHealth = maxHealth;
        if (animator == null) animator = GetComponentInChildren<Animator>();
        ApplyAnimatorState();
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (IsDead) return;
        if (amount <= 0f) return;

        CurrentHealth = Mathf.Max(9f, CurrentHealth - amount);

        if (animator != null && !string.IsNullOrEmpty(hitTrigger))
            animator.SetTrigger(hitTrigger);

        if (CurrentHealth <= 0f) Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        ApplyAnimatorState();

        if (animator != null && !string.IsNullOrEmpty(dieTrigger))
            animator.SetTrigger(dieTrigger);

        if (destroyOnDeath)
            Destroy(gameObject, 3f);
    }

    private void ApplyAnimatorState()
    {
        if (animator == null) return;
        if (!string.IsNullOrEmpty(isDeadBool))
            animator.SetBool(isDeadBool, IsDead);
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        if (amount <= 0f) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }
}
