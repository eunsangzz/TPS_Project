using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerMelee : MonoBehaviour
{
    [Header("Attack")]
    [SerializeField, Min(0f)] private float damage = 40f;
    [SerializeField, Min(0.1f)] private float range = 2.4f;
    [SerializeField, Range(1f, 180f)] private float arc = 100f;
    [SerializeField, Min(0.05f)] private float cooldown = 0.65f;
    [SerializeField, Min(0f)] private float windup = PlayerMeleeAnimation.ImpactTime;
    [SerializeField] private LayerMask hitMask = ~0;
    private PlayerLoadout loadout;
    private Camera view;
    private Transform weapon;
    private Material gripMaterial;
    private Material headMaterial;
    private float nextAttackTime;
    private float attackStartTime = -100f;
    private bool pendingHit;
    private Vector3 attackDirection;
    private PlayerMeleeAnimation bodyAnimation;
    private bool animationDriven;
    private Quaternion gripRotation;
    private PlayerSkills skills;
    private readonly HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();
    private int attackSequence;

    public bool IsAttacking => pendingHit || Time.time < attackStartTime + PlayerMeleeAnimation.Duration;
    public bool IsEquipped => isActiveAndEnabled && loadout != null && loadout.IsMeleeEquipped && loadout.IsAlive && loadout.isActiveAndEnabled;
    public float CooldownRemaining => Mathf.Max(0f, nextAttackTime - Time.time);
    public float CooldownDuration => Mathf.Max(cooldown, PlayerMeleeAnimation.Duration);
    public int AttackSequence => attackSequence;

    public void Initialize(PlayerLoadout owner, Camera camera)
    {
        loadout = owner;
        skills = GetComponent<PlayerSkills>();
        view = camera;
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            bodyAnimation = animator.GetComponent<PlayerMeleeAnimation>();
            if (bodyAnimation == null) bodyAnimation = animator.gameObject.AddComponent<PlayerMeleeAnimation>();
            bodyAnimation.Initialize(this, animator);
        }
        if (weapon == null) BuildWeapon();
        LateUpdate();
    }

    public bool TryAttack()
    {
        if (!isActiveAndEnabled || loadout == null || !loadout.CanAct || !loadout.IsMeleeEquipped || Time.time < nextAttackTime)
            return false;
        attackDirection = ViewDirection();
        attackSequence++;
        attackStartTime = Time.time;
        nextAttackTime = Time.time + Mathf.Max(cooldown, PlayerMeleeAnimation.Duration);
        pendingHit = true;
        animationDriven = bodyAnimation != null && bodyAnimation.PlayAttack();
        if (animationDriven) transform.rotation = Quaternion.LookRotation(attackDirection, Vector3.up);
        return true;
    }

    private void Update()
    {
        if (loadout == null || !loadout.CanAct || !loadout.IsMeleeEquipped)
        {
            // Pausing freezes the windup; death, disabling, or changing weapons cancels it.
            if (loadout == null || !loadout.IsMeleeEquipped || Time.timeScale > 0f) CancelAttack();
            return;
        }
        if (animationDriven && (bodyAnimation == null || !bodyAnimation.Available)) CancelAttack();
        if (pendingHit && !animationDriven && Time.time >= attackStartTime + windup)
        {
            pendingHit = false;
            ApplyHit();
        }
    }

    public void DeliverAnimationHit()
    {
        if (!animationDriven || !pendingHit || loadout == null || !loadout.CanAct || !IsEquipped) return;
        pendingHit = false;
        ApplyHit();
    }

    private void ApplyHit()
    {
        Vector3 origin = transform.position + Vector3.up;
        hitEnemies.Clear();
        bool damagedEnemy = false;
        float reach = skills != null ? skills.MeleeReachMultiplier : 1f;
        float attackArc = Mathf.Min(180f, arc * reach);
        foreach (Collider collider in Physics.OverlapSphere(origin, range * reach, hitMask, QueryTriggerInteraction.Ignore))
        {
            EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || hitEnemies.Contains(enemy)) continue;
            Vector3 point = collider.ClosestPoint(origin);
            Vector3 offset = point - origin;
            Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
            if (horizontal.sqrMagnitude > 0.001f && Vector3.Angle(attackDirection, horizontal) > attackArc * 0.5f) continue;
            if (IsBlocked(origin, point, enemy)) continue;
            hitEnemies.Add(enemy);
            float healthBefore = enemy.currentHealth;
            enemy.TakeDamage(damage * (skills != null ? skills.MeleeDamageMultiplier : 1f), point, attackDirection);
            if (enemy.currentHealth < healthBefore) damagedEnemy = true;
            if (enemy.IsDead && skills != null) skills.OnMeleeKill();
        }
        if (damagedEnemy) skills?.OnAttackHit();
    }

    private bool IsBlocked(Vector3 origin, Vector3 point, EnemyHealth target)
    {
        Vector3 delta = point - origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            if (hit.collider.GetComponentInParent<EnemyHealth>() == target) continue;
            return true;
        }
        return false;
    }

    private Vector3 ViewDirection()
    {
        Vector3 direction = Vector3.ProjectOnPlane(view != null ? view.transform.forward : transform.forward, Vector3.up);
        return direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
    }

    public void CancelAttack()
    {
        bool wasAttacking = IsAttacking;
        pendingHit = false;
        animationDriven = false;
        attackStartTime = -100f;
        if (wasAttacking && bodyAnimation != null) bodyAnimation.CancelAttack();
        // Keep the cooldown across weapon switches to prevent rapid switch attacks.
    }

    private void LateUpdate()
    {
        if (weapon == null) return;
        bool visible = IsEquipped;
        weapon.gameObject.SetActive(visible);
        if (!visible) return;
        if (bodyAnimation != null && bodyAnimation.Available)
        {
            weapon.localRotation = gripRotation;
            return;
        }
        float elapsed = Time.time - attackStartTime;
        float angle = 20f;
        if (elapsed < windup) angle = Mathf.Lerp(20f, -60f, elapsed / Mathf.Max(0.001f, windup));
        else if (elapsed < windup + 0.22f) angle = Mathf.Lerp(-60f, 120f, (elapsed - windup) / 0.22f);
        weapon.rotation = Quaternion.LookRotation(IsAttacking ? attackDirection : ViewDirection()) * Quaternion.Euler(angle, 0f, -20f);
    }

    private void BuildWeapon()
    {
        Transform hand = null;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == "Hand_Right") { hand = child; break; }
        weapon = new GameObject("MeleeBaton").transform;
        weapon.SetParent(hand != null ? hand : transform, false);
        weapon.localPosition = hand != null ? Vector3.zero : new Vector3(0.45f, 1.1f, 0.4f);
        // The imported hand is in centimeters (0.01 scale); preserve the weapon's meter size.
        Vector3 parentScale = weapon.parent.lossyScale;
        Vector3 modelScale = transform.lossyScale;
        weapon.localScale = new Vector3(modelScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
            modelScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), modelScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
        gripRotation = Quaternion.Euler(-22f, -50f, 74f);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        gripMaterial = new Material(shader) { color = new Color(0.12f, 0.14f, 0.16f) };
        headMaterial = new Material(shader) { color = new Color(0.92f, 0.63f, 0.18f) };
        MakePart("Grip", new Vector3(0f, 0.06f, 0f), new Vector3(0.1f, 0.28f, 0.1f), gripMaterial);
        MakePart("Baton", new Vector3(0f, 0.48f, 0f), new Vector3(0.14f, 0.65f, 0.14f), headMaterial);
        MakePart("Guard", new Vector3(0f, 0.18f, 0f), new Vector3(0.24f, 0.045f, 0.18f), headMaterial);
    }

    private void MakePart(string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(weapon, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.GetComponent<Renderer>().sharedMaterial = material;
        Collider collider = part.GetComponent<Collider>();
        collider.enabled = false;
        Release(collider);
    }

    private void OnDisable()
    {
        CancelAttack();
        if (weapon != null) weapon.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (weapon != null) Release(weapon.gameObject);
        Release(gripMaterial);
        Release(headMaterial);
    }

    private static void Release(Object item)
    {
        if (item == null) return;
        if (Application.isPlaying) Destroy(item);
        else DestroyImmediate(item);
    }
}
