using UnityEngine;
using UnityEngine.AI;

public enum EnemyType { Melee, Ranged }

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    [Header("Type")]
    public EnemyType enemyType = EnemyType.Melee;

    [Header("References")]
    public Transform player;
    public Transform head;
    public EnemyHealth health;

    [Header("Patrol Areas")]
    public PatrolArea[] patrolAreas;

    [Header("Vision")]
    public float viewDistance = 15f;
    [Range(10f, 180f)]
    public float viewAngleTotal = 100f;
    public LayerMask obstacleMask = ~0;
    public bool useLineOfSight = true;

    [Header("Chase")]
    public float chaseStopDistance = 1.5f;
    public float loseSightTime = 2f;
    public float repathInterval = 0.2f;

    [Header("Melee Setting")]
    public float meleeRange = 1.2f;
    public float meleeDamage = 10f;
    public float meleeAttackRate = 1.5f;
    public float meleeMoveSpeed = 4.5f;

    [Header("Ranged Setting")]
    public float rangedRange = 18f;
    public float rangedDamage = 12f;
    public float rangedAttackRate = 0.6f;
    public float rangedMoveSpeed = 2.8f;
    public float rangedDetectDistance = 25f;

    [Header("Ranged Projectile")]
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float projectileSpeed = 25f;

    [Header("Rotation")]
    public float turnSpeed = 10f;

    [Header("Model")]
    public Transform modelRoot;

    [Header("Tactics")]
    public bool useTactics = true;
    public float coverSearchRadius = 22f;
    public float coverDecisionInterval = 1.0f;
    public float coverArriveDistance = 1.0f;
    public float peekDuration = 1.3f;
    public float lowHealthCoverRatio = 0.55f;
    public float coverRepathInterval = 0.3f;

    private NavMeshAgent agent;

    private Vector3 smoothDir;
    private enum State { Patrol, Chase, Attack, TakeCover, PeekShoot }
    private State state = State.Patrol;

    private float nextPatrolPickTime;
    private Vector3 patrolTarget;

    private float lastSeenTime = -999f;
    private float nextRepathTime;
    private float nextAttackTime;
    private float nextCoverDecisionTime;
    private float nextCoverRepathTime;
    private float peekEndTime;

    private CoverPoint currentCover;

    [Header("Debug Draw")]
    public bool debugDrawVision = true;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (health == null) health = GetComponent<EnemyHealth>();
        if (head == null) head = transform;

        agent.updateRotation = false;

        ApplyTypeState();
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        PickNewPatrolTarget(true);
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;
        if (player == null) return;

        bool canSee = CanSeePlayer();
        if (canSee) lastSeenTime = Time.time;

        switch (state)
        {
            case State.Patrol:
                PatrolTick(canSee);
                FaceMoveDirection();
                break;

            case State.Chase:
                ChaseTick(canSee);
                FaceMoveDirection();
                break;

            case State.Attack:
                AttackTick(canSee);
                break;

            case State.TakeCover:
                TakeCoverTick(canSee);
                FaceMoveDirection();
                break;

            case State.PeekShoot:
                PeekShootTick(canSee);
                break;
        }

        KeepUpright();
    }

    private void ApplyTypeState()
    {
        if (enemyType == EnemyType.Melee)
        {
            agent.speed = meleeMoveSpeed;
            viewDistance = Mathf.Max(viewDistance, 12f);
            chaseStopDistance = meleeRange;
        }
        else
        {
            agent.speed = rangedMoveSpeed;
            viewDistance = Mathf.Max(viewDistance, rangedDetectDistance);
            chaseStopDistance = rangedRange * 0.85f;
        }
    }

    private void PatrolTick(bool canSee)
    {
        if (canSee)
        {
            state = State.Chase;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (Time.time >= nextPatrolPickTime) PickNewPatrolTarget(false);
        }
    }

    private void ChaseTick(bool canSee)
    {
        if (TryEnterTacticalCover(canSee)) return;

        if (!canSee && Time.time > lastSeenTime + loseSightTime)
        {
            state = State.Patrol;
            currentCover = null;
            PickNewPatrolTarget(true);
            return;
        }

        float dist = Vector3.Distance(transform.position, player.position);

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }

        float attackRange = (enemyType == EnemyType.Melee) ? meleeRange : rangedRange;
        if (dist <= attackRange)
        {
            state = State.Attack;
            agent.isStopped = true;
        }
    }

    private void AttackTick(bool canSee)
    {
        if (TryEnterTacticalCover(canSee)) return;

        float dist = Vector3.Distance(transform.position, player.position);
        float attackRange = (enemyType == EnemyType.Melee) ? meleeRange : rangedRange;

        if (dist > attackRange + 0.5f)
        {
            state = State.Chase;
            agent.isStopped = false;
            return;
        }

        if (!canSee && Time.time > lastSeenTime + loseSightTime)
        {
            state = State.Patrol;
            currentCover = null;
            PickNewPatrolTarget(true);
            return;
        }

        FaceTarget(player.position);

        float interval = (enemyType == EnemyType.Melee)
            ? (1f / Mathf.Max(0.01f, meleeAttackRate))
            : (1f / Mathf.Max(0.01f, rangedAttackRate));

        if (Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + interval;
            DoAttack();
        }
    }

    private void TakeCoverTick(bool canSee)
    {
        if (currentCover == null)
        {
            state = State.Chase;
            return;
        }

        if (Time.time >= nextCoverRepathTime)
        {
            nextCoverRepathTime = Time.time + coverRepathInterval;
            Vector3 desired = currentCover.GetStandPosition(player.position);
            if (NavMesh.SamplePosition(desired, out NavMeshHit sampled, 2f, NavMesh.AllAreas))
                desired = sampled.position;

            agent.isStopped = false;
            agent.SetDestination(desired);
        }

        if (!agent.pathPending && agent.remainingDistance <= coverArriveDistance)
        {
            if (enemyType == EnemyType.Ranged)
            {
                state = State.PeekShoot;
                agent.isStopped = true;
                peekEndTime = Time.time + peekDuration;
            }
            else
            {
                state = State.Chase;
            }
        }

        if (!canSee && Time.time > lastSeenTime + loseSightTime)
        {
            state = State.Patrol;
            currentCover = null;
            PickNewPatrolTarget(true);
        }
    }

    private void PeekShootTick(bool canSee)
    {
        if (currentCover == null)
        {
            state = State.Chase;
            return;
        }

        FaceTarget(player.position);

        float dist = Vector3.Distance(transform.position, player.position);
        float attackRange = (enemyType == EnemyType.Melee) ? meleeRange : rangedRange;
        if (dist > attackRange + 4f)
        {
            state = State.Chase;
            agent.isStopped = false;
            return;
        }

        if (canSee && Time.time >= nextAttackTime)
        {
            float interval = (enemyType == EnemyType.Melee)
                ? (1f / Mathf.Max(0.01f, meleeAttackRate))
                : (1f / Mathf.Max(0.01f, rangedAttackRate));
            nextAttackTime = Time.time + interval;
            DoAttack();
        }

        if (Time.time >= peekEndTime)
        {
            state = State.TakeCover;
            agent.isStopped = false;
        }
    }

    private bool TryEnterTacticalCover(bool canSee)
    {
        if (!useTactics) return false;
        if (player == null) return false;
        if (Time.time < nextCoverDecisionTime) return false;

        nextCoverDecisionTime = Time.time + coverDecisionInterval;

        if (!ShouldTakeCover(canSee)) return false;

        CoverPoint bestCover = FindBestCover();
        if (bestCover == null) return false;

        currentCover = bestCover;
        state = State.TakeCover;
        nextCoverRepathTime = 0f;
        return true;
    }

    private bool ShouldTakeCover(bool canSee)
    {
        if (!canSee) return false;

        float hpRatio = 1f;
        if (health != null && health.maxHealth > 0.01f)
            hpRatio = health.currentHealth / health.maxHealth;

        if (enemyType == EnemyType.Ranged)
            return hpRatio <= lowHealthCoverRatio || state == State.Attack;

        return hpRatio <= lowHealthCoverRatio * 0.7f;
    }

    private CoverPoint FindBestCover()
    {
        if (CoverPoint.All.Count == 0) return null;

        float bestScore = float.MinValue;
        CoverPoint best = null;

        float preferredRange = (enemyType == EnemyType.Melee) ? meleeRange : rangedRange;

        for (int i = 0; i < CoverPoint.All.Count; i++)
        {
            CoverPoint point = CoverPoint.All[i];
            if (point == null) continue;

            float toPoint = Vector3.Distance(transform.position, point.transform.position);
            if (toPoint > coverSearchRadius) continue;

            Vector3 stand = point.GetStandPosition(player.position);
            if (!NavMesh.SamplePosition(stand, out NavMeshHit sampled, 2.0f, NavMesh.AllAreas))
                continue;

            stand = sampled.position;

            Vector3 eye = stand + Vector3.up * 1.2f;
            Vector3 playerEye = player.position + Vector3.up * 1.2f;
            bool blocked = Physics.Linecast(eye, playerEye, obstacleMask, QueryTriggerInteraction.Ignore);
            if (!blocked) continue;

            float distToPlayer = Vector3.Distance(stand, player.position);
            float rangeScore = 1f - Mathf.Clamp01(Mathf.Abs(distToPlayer - preferredRange) / Mathf.Max(preferredRange, 0.1f));

            float score = 0f;
            score += 60f;
            score += rangeScore * 25f;
            score -= toPoint * 1.2f;
            score += Random.Range(0f, 3f);

            if (score > bestScore)
            {
                bestScore = score;
                best = point;
            }
        }

        return best;
    }

    private void DoAttack()
    {
        if (enemyType == EnemyType.Melee)
        {
            if (Vector3.Distance(transform.position, player.position) <= meleeRange + 0.1f)
            {
                player.GetComponent<PlayerHealth>()?.TakeDamage(meleeDamage);
            }
        }
        else
        {
            if (projectilePrefab != null && firePoint != null)
            {
            }
            else
            {
                player.GetComponent<PlayerHealth>()?.TakeDamage(rangedDamage);
            }
        }
    }

    private bool CanSeePlayer()
    {
        Vector3 origin = head.position;
        Vector3 toPlayer = (player.position + Vector3.up * 1.2f) - origin;

        float maxDist = (enemyType == EnemyType.Ranged) ? rangedDetectDistance : viewDistance;
        if (toPlayer.sqrMagnitude > maxDist * maxDist) return false;

        Vector3 dir = toPlayer.normalized;

        float half = viewAngleTotal * 0.5f;
        float angle = Vector3.Angle(head.forward, dir);
        if (angle > half) return false;

        if (!useLineOfSight) return true;

        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDist, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == player || hit.transform.IsChildOf(player)) return true;
            return false;
        }

        return false;
    }

    private void PickNewPatrolTarget(bool immediate)
    {
        if (patrolAreas == null || patrolAreas.Length == 0) return;

        PatrolArea area = patrolAreas[Random.Range(0, patrolAreas.Length)];
        Vector3 candidate = area.GetRandomPoint();

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 5f, NavMesh.AllAreas))
            patrolTarget = hit.position;
        else
            patrolTarget = transform.position;

        agent.isStopped = false;
        agent.SetDestination(patrolTarget);

        nextPatrolPickTime = Time.time + Random.Range(2f, 5f);
        if (immediate) nextPatrolPickTime = Time.time;
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 look = targetPos - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(look.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, rot, turnSpeed * Time.deltaTime);
    }

    private void KeepUpright()
    {
        Vector3 e = transform.eulerAngles;
        Quaternion upright = Quaternion.Euler(0f, e.y, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, upright, 1f - Mathf.Exp(-10f * Time.deltaTime));

        if (modelRoot != null)
        {
            Vector3 me = modelRoot.eulerAngles;
            Quaternion mu = Quaternion.Euler(0f, me.y, 0f);
            modelRoot.rotation = Quaternion.Slerp(modelRoot.rotation, mu, 1f - Mathf.Exp(-10f * Time.deltaTime));
        }
    }

    private void FaceMoveDirection()
    {
        if (agent == null) return;

        Transform t = (modelRoot != null) ? modelRoot : transform;

        Vector3 dir = agent.steeringTarget - t.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.0001f) return;

        if (smoothDir == Vector3.zero) smoothDir = dir;
        float sharpness = 12f;

        smoothDir = Vector3.Slerp(smoothDir, dir, 1f - Mathf.Exp(-sharpness * Time.deltaTime));

        Quaternion rot = Quaternion.LookRotation(smoothDir.normalized, Vector3.up);

        Vector3 e = rot.eulerAngles;
        rot = Quaternion.Euler(0f, e.y, 0f);

        t.rotation = Quaternion.RotateTowards(t.rotation, rot, turnSpeed * Time.deltaTime);
    }

    private void OnDrawGizmos()
    {
        if (!debugDrawVision) return;

        float dist = (enemyType == EnemyType.Ranged) ? rangedDetectDistance : viewDistance;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, dist);

        Vector3 origin = (head != null ? head.position : transform.position) + Vector3.up * 0.1f;
        float half = viewAngleTotal * 0.5f;

        Vector3 forward = (head != null ? head.forward : transform.forward);
        Vector3 leftDir = Quaternion.AngleAxis(-half, Vector3.up) * forward;
        Vector3 rightDir = Quaternion.AngleAxis(half, Vector3.up) * forward;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + forward * dist);
        Gizmos.DrawLine(origin, origin + leftDir * dist);
        Gizmos.DrawLine(origin, origin + rightDir * dist);
    }
}
