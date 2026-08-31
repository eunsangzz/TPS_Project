using UnityEngine;

public class EnemyCombat : MonoBehaviour
{
    [Header("References")]
    public Transform self;
    public Transform player;
    public Transform firePoint;

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

    [Header("Ranged Timing")]
    public float attackWindup = 0.25f;

    [Header("Shot Visuals")]
    public Color tracerColor = new Color(1f, 0.35f, 0.12f, 1f);

    [Header("Ranged Accuracy / Spread")]
    [Range(0f, 1f)] public float accuracy = 0.5f;
    public float spreadAngle = 18.0f;
    public int pelletCount = 1;
    public float aimHeight = 1.2f;
    public float guaranteedMissRadius = 1.6f;

    [Header("Raycast")]
    public LayerMask hitMask = ~0;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Debug")]
    public bool drawShotRay = true;
    public float debugRayTime = 0.15f;

    private float nextAttackTime;
    private bool rangedAttackPending;
    private float pendingFireTime;
    private EnemyHealth health;
    private WeaponVFX weaponVFX;

    private void Awake()
    {
        if (self == null) self = transform;
        health = GetComponent<EnemyHealth>();
    }

    private void Update()
    {
        if (enemyType != EnemyType.Ranged) return;
        if (!rangedAttackPending) return;
        if (health != null && health.IsDead)
        {
            CancelPendingAttack();
            return;
        }

        if (Time.time >= pendingFireTime)
        {
            rangedAttackPending = false;
            FireRangedHitscan();
        }
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
        if (enemyType == EnemyType.Melee)
            return Time.time >= nextAttackTime;

        return Time.time >= nextAttackTime && !rangedAttackPending;
    }

    public void MarkAttackUsed()
    {
        nextAttackTime = Time.time + GetAttackInterval();
    }

    public void CancelPendingAttack()
    {
        rangedAttackPending = false;
    }

    public bool IsAttackPending()
    {
        return rangedAttackPending;
    }

    public void TryAttack()
    {
        if (player == null) return;
        if (health != null && health.IsDead) return;

        if (enemyType == EnemyType.Melee)
        {
            TryMeleeAttack();
        }
        else
        {
            StartRangedAttack();
        }
    }

    private void TryMeleeAttack()
    {
        if (Vector3.Distance(self.position, player.position) <= meleeRange + 0.1f)
        {
            IDamageable damageable = player.GetComponentInParent<IDamageable>();
            if (damageable != null)
            {
                Vector3 hitPoint = player.position;
                Vector3 hitDirection = (player.position - self.position).normalized;
                damageable.TakeDamage(meleeDamage, hitPoint, hitDirection);
            }
        }
    }

    private void StartRangedAttack()
    {
        if (rangedAttackPending) return;

        rangedAttackPending = true;
        pendingFireTime = Time.time + attackWindup;
    }

    private void FireRangedHitscan()
    {
        if (player == null) return;
        if (health != null && health.IsDead) return;

        Vector3 fallbackOrigin = self.position + Vector3.up * 1.2f;
        // The actor root is at its feet, not at muzzle height.
        Vector3 origin = firePoint != null && firePoint != self ? firePoint.position : fallbackOrigin;
        Vector3 targetPoint = player.position + Vector3.up * aimHeight;

        int shots = Mathf.Max(1, pelletCount);

        for (int i = 0; i < shots; i++)
        {
            bool isAccurateShot = Random.value <= accuracy;
            Vector3 aimPoint = isAccurateShot ? targetPoint : GetMissAimPoint(targetPoint, origin);
            Vector3 dir = (aimPoint - origin).normalized;

            if (isAccurateShot)
            {
                dir = ApplySpread(dir, spreadAngle * 0.1f);
            }

            Ray ray = new Ray(origin, dir);
            bool hitSomething = TryGetShotHit(ray, out RaycastHit hit);

            if (Application.isPlaying)
            {
                if (weaponVFX == null) weaponVFX = GetComponentInChildren<WeaponVFX>();
                if (weaponVFX == null) weaponVFX = gameObject.AddComponent<WeaponVFX>();
                weaponVFX.PlayMuzzleFlash();
                weaponVFX.PlayTracer(origin, hitSomething ? hit.point : origin + dir * rangedRange, tracerColor);
            }

            if (hitSomething)
            {
                if (drawShotRay)
                    Debug.DrawLine(origin, hit.point, Color.red, debugRayTime);

                IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
                if (isAccurateShot && damageable != null)
                {
                    damageable.TakeDamage(rangedDamage, hit.point, dir);
                }
            }
            else
            {
                if (drawShotRay)
                    Debug.DrawLine(origin, origin + dir * rangedRange, Color.yellow, debugRayTime);
            }
        }
    }

    private bool TryGetShotHit(Ray ray, out RaycastHit closestHit)
    {
        closestHit = default;
        float closestDistance = float.PositiveInfinity;
        RaycastHit[] hits = Physics.RaycastAll(ray, rangedRange, hitMask, triggerInteraction);

        // Ignore only this shooter's colliders; walls and other actors still block shots.
        foreach (RaycastHit hit in hits)
        {
            if (IsSelfTransform(hit.collider.transform) || hit.distance >= closestDistance) continue;
            closestHit = hit;
            closestDistance = hit.distance;
        }

        return closestDistance < float.PositiveInfinity;
    }

    private void OnDisable()
    {
        CancelPendingAttack();
    }

    private Vector3 GetMissAimPoint(Vector3 targetPoint, Vector3 origin)
    {
        Vector3 toTarget = (targetPoint - origin).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, toTarget).normalized;
        if (right.sqrMagnitude < 0.0001f) right = self != null ? self.right : Vector3.right;

        Vector3 up = Vector3.Cross(toTarget, right).normalized;
        Vector2 missOffset = Random.insideUnitCircle.normalized * guaranteedMissRadius;
        return targetPoint + right * missOffset.x + up * missOffset.y;
    }

    private Vector3 ApplySpread(Vector3 baseDir, float angleDeg)
    {
        if (angleDeg <= 0.001f) return baseDir;

        Quaternion yaw = Quaternion.AngleAxis(Random.Range(-angleDeg, angleDeg), Vector3.up);


        Vector3 rightAxis = Vector3.Cross(Vector3.up, baseDir).normalized;
        if(rightAxis.sqrMagnitude < 0.0001f)
        {
            rightAxis = Vector3.right;
        }

        Quaternion pitch = Quaternion.AngleAxis(Random.Range(-angleDeg, angleDeg), rightAxis);

        Vector3 finalDir = pitch * yaw * baseDir;
        return finalDir.normalized;
    }

    private bool IsSelfTransform(Transform hitTransform)
    {
        if (hitTransform == null || self == null) return false;
        return hitTransform == self || hitTransform.IsChildOf(self);
    }
}
