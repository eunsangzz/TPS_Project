using UnityEngine;
using UnityEngine.AI;

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

    [Header("Chase")]
    public float loseSightTime = 2f;
    public float repathInterval = 0.2f;

    [Header("Movement")]
    public float meleeMoveSpeed = 4.5f;
    public float rangedMoveSpeed = 2.8f;
    public float turnSpeed = 10f;

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

    private State state = State.Patrol;
    private CoverPoint currentCover;

    private float lastSeenTime = -999f;
    private float nextRepathTime;
    private float nextCoverRepathTime;
    private float peekEndTime;
    private float nextPatrolPickTime;

    private Vector3 patrolTarget;
    private Vector3 smoothDir;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        perception = GetComponent<EnemyPerception>();
        combat = GetComponent<EnemyCombat>();
        tactics = GetComponent<EnemyTactics>();

        if (health == null) health = GetComponent<EnemyHealth>();
        if (head == null) head = transform;

        agent.updateRotation = false;

        SetupSharedReferences();
        ApplyTypeState();
    }

    private void Start()
    {
        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        SetupSharedReferences();
        PickNewPatrolTarget(true);
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;
        if (player == null) return;

        bool canSee = perception.CanSeePlayer(enemyType == EnemyType.Ranged);
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

        KeepUpright();
    }

    private void SetupSharedReferences()
    {
        perception.player = player;
        perception.head = head;
        perception.self = transform;

        combat.player = player;
        combat.self = transform;
        combat.enemyType = enemyType;

        tactics.player = player;
        tactics.self = transform;
        tactics.health = health;
        tactics.enemyType = enemyType;
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

        if (!canSee && Time.time > lastSeenTime + loseSightTime)
        {
            ChangeState(State.Patrol);
            currentCover = null;
            PickNewPatrolTarget(true);
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
            agent.isStopped = true;
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

        if(combat.CanAttackNow())
        {
            combat.MarkAttackUsed();
            combat.TryAttack();
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

        nextPatrolPickTime = immediate ? Time.time : Time.time + Random.Range(2f, 5f);
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


}
