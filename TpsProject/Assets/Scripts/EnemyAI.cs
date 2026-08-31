using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public enum EnemyType { Melee, Ranged }

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyPerception))]
[RequireComponent(typeof(EnemyCombat))]
[RequireComponent(typeof(EnemyTactics))]
public class EnemyAI : MonoBehaviour
{
    private enum State
    {
        Patrol,
        Chase,
        Attack,
        TakeCover,
        PeekShoot
    }

    [Header("Type")]
    public EnemyType enemyType = EnemyType.Melee;

    [Header("References")]
    public Transform player;
    public Transform head;
    public Transform modelRoot;
    public EnemyHealth health;

    [Header("Patrol")]
    public PatrolArea[] patrolAreas;
    [SerializeField] private float patrolAreaEdgeInset = 0.18f;
    [SerializeField] private float fallbackPatrolRadius = 14f;
    [SerializeField] private float patrolNavMeshSampleRadius = 5f;

    [Header("Chase")]
    public float loseSightTime = 2f;
    public float repathInterval = 0.2f;
    [SerializeField] private float chaseAfterDamageDuration = 2f;
    [SerializeField] private float chaseAfterDamageRange = 35f;

    [Header("Movement")]
    public float meleeMoveSpeed = 3.6f;
    public float rangedMoveSpeed = 2.2f;
    public float turnSpeed = 10f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    [SerializeField] private string isAimParam = "IsAim";
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [SerializeField] private string isSprintParam = "IsSprint";
    [SerializeField] private string attackTrigger = "FireSingle";
    [SerializeField] private float animatorSpeedDampTime = 0.12f;

    [Header("Ranged Evasion")]
    public float rangedEvasionInterval = 0.85f;
    public float rangedEvasionSideStep = 3f;
    public float rangedPreferredDistance = 14f;

    [Header("Cover")]
    public float coverArriveDistance = 1.0f;
    public float coverRepathInterval = 0.3f;
    public float peekDuration = 1.3f;

    [Header("Debug")]
    public bool debugStateLog = false;

    private NavMeshAgent agent;
    private EnemyPerception perception;
    private EnemyCombat combat;
    private EnemyTactics tactics;
    private readonly Dictionary<string, AnimatorControllerParameterType> animatorParams = new Dictionary<string, AnimatorControllerParameterType>();

    private State state = State.Patrol;
    private CoverPoint currentCover;

    private float lastSeenTime = -999f;
    private float nextRepathTime;
    private float damageChaseEndTime = -999f;
    private float nextCoverRepathTime;
    private float peekEndTime;
    private float nextPatrolPickTime;
    private float nextRangedEvasionTime;
    private int rangedEvasionSide = 1;

    private Vector3 patrolTarget;
    private Vector3 smoothDir;

    public bool HasDetectedPlayer => lastSeenTime > -900f && Time.time <= lastSeenTime + loseSightTime;
    public bool CanCurrentlySeePlayer { get; private set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        perception = GetComponent<EnemyPerception>();
        combat = GetComponent<EnemyCombat>();
        tactics = GetComponent<EnemyTactics>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (health == null) health = GetComponent<EnemyHealth>();
        if (head == null) head = transform;

        agent.updateRotation = false;

        CacheAnimatorParameters();
        SetupSharedReferences();
        SubscribeHealthEvents();
        ApplyTypeState();
    }

    private void OnDestroy()
    {
        if (health != null)
            health.Damaged -= HandleDamaged;
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        SetupSharedReferences();
        ApplyTypeState();
        ResolvePatrolAreas();
        PickNewPatrolTarget(true);
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                SetupSharedReferences();
            }
        }

        bool canSee = player != null && perception.CanSeePlayer(enemyType == EnemyType.Ranged);
        CanCurrentlySeePlayer = canSee;
        if (canSee) lastSeenTime = Time.time;

        switch (state)
        {
            case State.Patrol:
                UpdatePatrol(canSee);
                FaceMoveDirection();
                break;

            case State.Chase:
                UpdateChase(canSee);
                FaceMoveDirection();
                break;

            case State.Attack:
                UpdateAttack(canSee);
                break;

            case State.TakeCover:
                UpdateTakeCover(canSee);
                FaceMoveDirection();
                break;

            case State.PeekShoot:
                UpdatePeekShoot(canSee);
                break;
        }

        UpdateAnimator(canSee);
        KeepUpright();
    }

    private void SetupSharedReferences()
    {
        if (perception != null)
        {
            perception.player = player;
            perception.head = head;
            perception.self = transform;
        }

        if (combat != null)
        {
            combat.player = player;
            combat.self = transform;
            combat.enemyType = enemyType;
        }

        if (tactics != null)
        {
            tactics.player = player;
            tactics.self = transform;
            tactics.health = health;
            tactics.enemyType = enemyType;
        }
    }

    private void ApplyTypeState()
    {
        if (enemyType == EnemyType.Melee)
        {
            agent.speed = meleeMoveSpeed;
        }
        else
        {
            agent.speed = rangedMoveSpeed;
        }
    }

    private void ChangeState(State next)
    {
        if (state == next) return;

        bool leavingAttackState = state == State.Attack || state == State.PeekShoot;
        bool enteringAttackState = next == State.Attack || next == State.PeekShoot;

        if (leavingAttackState && !enteringAttackState)
        {
            combat.CancelPendingAttack();
        }

        state = next;

        if (debugStateLog)
            Debug.Log($"{name} -> {state}");
    }

    private void UpdatePatrol(bool canSee)
    {
        if (canSee)
        {
            ChangeState(State.Chase);
            return;
        }

        if (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            PickNewPatrolTarget(true);
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            if (Time.time >= nextPatrolPickTime)
            {
                PickNewPatrolTarget(false);
            }
        }
    }

    private void UpdateChase(bool canSee)
    {
        if (TryStartTakeCover(canSee)) return;

        if (IsDamageChaseActive() && !canSee)
        {
            float damageChaseDistance = perception.DistanceToPlayer();
            if (damageChaseDistance > chaseAfterDamageRange || Time.time > damageChaseEndTime)
            {
                StopChasingAndPatrol();
                return;
            }
        }
        else if (!canSee && Time.time > lastSeenTime + loseSightTime)
        {
            StopChasingAndPatrol();
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }

        float dist = perception.DistanceToPlayer();

        if (dist <= combat.GetAttackRange())
        {
            agent.isStopped = enemyType != EnemyType.Ranged;
            ChangeState(State.Attack);
        }
    }

    private void UpdateAttack(bool canSee)
    {
        if (TryStartTakeCover(canSee)) return;

        float dist = perception.DistanceToPlayer();

        if (dist > combat.GetAttackRange() + 0.5f)
        {
            agent.isStopped = false;
            ChangeState(State.Chase);
            return;
        }

        if (!canSee && Time.time > lastSeenTime + loseSightTime)
        {
            currentCover = null;
            PickNewPatrolTarget(true);
            ChangeState(State.Patrol);
            return;
        }

        FaceTarget(player.position);

        if (enemyType == EnemyType.Ranged)
        {
            UpdateRangedEvasion(dist);
        }
        else
        {
            agent.isStopped = true;
        }

        if(combat.CanAttackNow())
        {
            combat.MarkAttackUsed();
            TriggerAttackAnimation();
            combat.TryAttack();
        }
    }

    private void UpdateRangedEvasion(float dist)
    {
        if (Time.time < nextRangedEvasionTime) return;

        nextRangedEvasionTime = Time.time + rangedEvasionInterval;
        rangedEvasionSide *= -1;

        Vector3 awayFromPlayer = transform.position - player.position;
        awayFromPlayer.y = 0f;
        if (awayFromPlayer.sqrMagnitude < 0.0001f) awayFromPlayer = -transform.forward;
        awayFromPlayer.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, awayFromPlayer).normalized * rangedEvasionSide;
        Vector3 distanceAdjust = Vector3.zero;

        if (dist < rangedPreferredDistance - 2f)
            distanceAdjust = awayFromPlayer * 2.5f;
        else if (dist > rangedPreferredDistance + 3f)
            distanceAdjust = -awayFromPlayer * 1.5f;

        Vector3 desired = transform.position + side * rangedEvasionSideStep + distanceAdjust;
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 4f, NavMesh.AllAreas))
        {
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    private void UpdateTakeCover(bool canSee)
    {
        if (currentCover == null)
        {
            ChangeState(State.Chase);
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
                agent.isStopped = true;
                peekEndTime = Time.time + peekDuration;
                ChangeState(State.PeekShoot);
            }
            else
            {
                ChangeState(State.Chase);
            }
        }

        if (!canSee && Time.time > lastSeenTime + loseSightTime)
        {
            currentCover = null;
            PickNewPatrolTarget(true);
            ChangeState(State.Patrol);
        }
    }

    private void UpdatePeekShoot(bool canSee)
    {
        if (currentCover == null)
        {
            ChangeState(State.Chase);
            return;
        }

        FaceTarget(player.position);

        float dist = perception.DistanceToPlayer();

        if (dist > combat.GetAttackRange() + 4f)
        {
            agent.isStopped = false;
            ChangeState(State.Chase);
            return;
        }

        if (canSee && combat.CanAttackNow())
        {
            combat.MarkAttackUsed();
            TriggerAttackAnimation();
            combat.TryAttack();
        }

        if (Time.time >= peekEndTime)
        {
            agent.isStopped = false;
            ChangeState(State.TakeCover);
        }
    }

    private bool TryStartTakeCover(bool canSee)
    {
        bool isInAttackState = state == State.Attack;
        if (!tactics.ShouldTakeCover(canSee, isInAttackState)) return false;

        CoverPoint bestCover = tactics.FindBestCover(combat.GetAttackRange());
        if (bestCover == null) return false;

        currentCover = bestCover;
        nextCoverRepathTime = 0f;
        ChangeState(State.TakeCover);
        return true;
    }

    private void SubscribeHealthEvents()
    {
        if (health == null) return;

        health.Damaged -= HandleDamaged;
        health.Damaged += HandleDamaged;
    }

    private void HandleDamaged(EnemyHealth damagedEnemy, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (health != damagedEnemy) return;

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                SetupSharedReferences();
            }
        }

        if (player == null) return;
        if (Vector3.Distance(transform.position, player.position) > chaseAfterDamageRange) return;

        damageChaseEndTime = Time.time + chaseAfterDamageDuration;
        lastSeenTime = Time.time;

        // Damage reveals the target but must not repeatedly interrupt a ranged windup.
        if (enemyType == EnemyType.Ranged && state == State.Attack &&
            perception.DistanceToPlayer() <= combat.GetAttackRange())
        {
            return;
        }

        currentCover = null;
        nextRepathTime = 0f;
        agent.isStopped = false;
        ChangeState(State.Chase);
    }

    private bool IsDamageChaseActive()
    {
        return Time.time <= damageChaseEndTime;
    }

    private void StopChasingAndPatrol()
    {
        damageChaseEndTime = -999f;
        currentCover = null;
        ChangeState(State.Patrol);
        PickNewPatrolTarget(true);
    }

    private void PickNewPatrolTarget(bool immediate)
    {
        ResolvePatrolAreas();

        if (!TryPickPatrolAreaPoint(out Vector3 target) &&
            !TryPickNearbyNavMeshPoint(out target))
        {
            nextPatrolPickTime = Time.time + 1f;
            return;
        }

        patrolTarget = target;

        agent.isStopped = false;
        if (!agent.SetDestination(patrolTarget))
        {
            nextPatrolPickTime = Time.time + 1f;
            return;
        }

        nextPatrolPickTime = immediate ? Time.time : Time.time + Random.Range(2f, 5f);
    }

    private void ResolvePatrolAreas()
    {
        if (patrolAreas != null && patrolAreas.Length > 0) return;

        patrolAreas = FindObjectsByType<PatrolArea>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private bool TryPickPatrolAreaPoint(out Vector3 point)
    {
        point = transform.position;
        if (patrolAreas == null || patrolAreas.Length == 0) return false;

        for (int i = 0; i < 24; i++)
        {
            PatrolArea area = patrolAreas[Random.Range(0, patrolAreas.Length)];
            if (area == null) continue;
            if (!area.TryGetRandomNavMeshPoint(patrolAreaEdgeInset, patrolNavMeshSampleRadius, out Vector3 candidate)) continue;
            if (!HasCompletePath(candidate)) continue;

            point = candidate;
            return true;
        }

        return false;
    }

    private bool TryPickNearbyNavMeshPoint(out Vector3 point)
    {
        for (int i = 0; i < 24; i++)
        {
            Vector2 random = Random.insideUnitCircle.normalized * Random.Range(fallbackPatrolRadius * 0.35f, fallbackPatrolRadius);
            Vector3 candidate = transform.position + new Vector3(random.x, 0f, random.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, patrolNavMeshSampleRadius, NavMesh.AllAreas)) continue;
            if (!HasCompletePath(hit.position)) continue;

            point = hit.position;
            return true;
        }

        point = transform.position;
        return false;
    }

    private bool HasCompletePath(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh) return false;

        NavMeshPath path = new NavMeshPath();
        return agent.CalculatePath(destination, path) && path.status == NavMeshPathStatus.PathComplete;
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

    private void CacheAnimatorParameters()
    {
        animatorParams.Clear();
        if (animator == null) return;

        foreach (AnimatorControllerParameter param in animator.parameters)
        {
            if (!animatorParams.ContainsKey(param.name))
                animatorParams.Add(param.name, param.type);
        }
    }

    private void UpdateAnimator(bool canSee)
    {
        if (animator == null || agent == null) return;

        float maxSpeed = Mathf.Max(agent.speed, 0.01f);
        float speed01 = agent.isStopped ? 0f : Mathf.Clamp01(agent.velocity.magnitude / maxSpeed);
        bool isMoving = speed01 > 0.05f;
        bool isAiming = canSee || state == State.Attack || state == State.PeekShoot;

        SetAnimatorFloat(moveSpeedParam, speed01);
        SetAnimatorBool(isGroundedParam, true);
        SetAnimatorBool(isSprintParam, isMoving && enemyType == EnemyType.Melee);
        SetAnimatorBool(isAimParam, isAiming);
    }

    private void TriggerAttackAnimation()
    {
        if (animator == null) return;

        if (HasAnimatorParam(attackTrigger, AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger(attackTrigger);
        }
        else if (HasAnimatorParam("Fire", AnimatorControllerParameterType.Trigger))
        {
            animator.SetTrigger("Fire");
        }
    }

    private void SetAnimatorFloat(string paramName, float value)
    {
        if (HasAnimatorParam(paramName, AnimatorControllerParameterType.Float))
            animator.SetFloat(paramName, value, animatorSpeedDampTime, Time.deltaTime);
    }

    private void SetAnimatorBool(string paramName, bool value)
    {
        if (HasAnimatorParam(paramName, AnimatorControllerParameterType.Bool))
            animator.SetBool(paramName, value);
    }

    private bool HasAnimatorParam(string paramName, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(paramName)) return false;
        return animatorParams.TryGetValue(paramName, out AnimatorControllerParameterType foundType) && foundType == type;
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


}
