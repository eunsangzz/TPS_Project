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
    public const float MeleeSpeedMultiplier = 1.5f;
    public const float DefaultMeleeMoveSpeed = 5.4f;
    public const float DefaultMeleeAttackAnimationSpeed = 1.45f;
    public const float DefaultMeleeAttackRange = 1.4f;
    public const float DefaultMeleeWindup = 0.24f;
    public const float DefaultMeleeHitRangeGrace = 0.75f;
    public const string MeleeWeaponName = "EnemyMeleeBlade";
    private const string MeleeAnimationLayerName = "PlayerMelee";
    private const string MeleeAttackStateName = "Attack";
    private const int SurroundSlotsPerRing = 6;
    private static readonly List<EnemyAI> ActiveEnemies = new List<EnemyAI>();
    private static readonly Color MeleeBladeColor = new Color(0.1f, 0.9f, 1f, 1f);
    private static readonly Color MeleeWarningColor = new Color(1f, 0.16f, 0.04f, 1f);
    private enum State
    {
        Patrol,
        Chase,
        Investigate,
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

    [Header("Patrol Boundary Avoidance")]
    private const string PatrolBoundaryTag = "OutLine";
    [SerializeField, Min(0.1f)] private float patrolBoundaryDistance = 1.5f;
    [SerializeField, Min(0.5f)] private float patrolBoundaryTurnDistance = 4f;
    [SerializeField, Min(0.05f)] private float patrolBoundaryCheckInterval = 0.25f;
    private float nextPatrolBoundaryCheckTime;
    private Collider[] patrolBoundaryColliders = new Collider[32];

    [Header("Chase")]
    public float loseSightTime = 2f;
    public float repathInterval = 0.2f;
    [SerializeField] private float chaseAfterDamageDuration = 2f;
    [SerializeField] private float chaseAfterDamageRange = 35f;

    [Header("Movement")]
    public float meleeMoveSpeed = DefaultMeleeMoveSpeed;
    public float rangedMoveSpeed = 2.2f;
    public float turnSpeed = 10f;

    [Header("Gunshot Hearing")]
    [SerializeField, Min(0f)] private float gunshotHearingRange = 30f;
    [SerializeField, Min(1f)] private float gunshotMemoryDuration = 20f;
    [SerializeField, Min(0f)] private float gunshotSearchDuration = 2f;
    private bool hasHeardGunshot;
    private Vector3 lastHeardShotPosition;
    private float heardShotExpiresAt;
    private float gunshotSearchEndsAt = -1f;
    public bool IsInvestigatingGunshot => state == State.Investigate;
    public Vector3 LastHeardShotPosition => lastHeardShotPosition;

    [Header("Melee Surround")]
    [SerializeField, Min(0.4f)] private float meleeSurroundRadius = 0.85f;
    [SerializeField, Min(0.4f)] private float meleeSurroundRingSpacing = 1.0f;
    [SerializeField, Min(0.1f)] private float meleeSurroundSampleRadius = 1.5f;

    [Header("Attack Evasion")]
    [SerializeField, Range(0f, 1f)] private float dodgeChance = 0.32f;
    [SerializeField, Min(0.1f)] private float dodgeCooldown = 2.5f;
    [SerializeField, Min(0.1f)] private float dodgeDuration = 0.62f;
    [SerializeField, Min(0.1f)] private float dodgeSpeed = 7.5f;
    [SerializeField, Min(0.1f)] private float dodgeDistance = 3f;
    [SerializeField, Min(0.1f)] private float meleeThreatDistance = 3.5f;
    [SerializeField, Min(0.1f)] private float aimThreatDistance = 22f;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string moveSpeedParam = "MoveSpeed";
    [SerializeField] private string isAimParam = "IsAim";
    [SerializeField] private string isGroundedParam = "IsGrounded";
    [SerializeField] private string isSprintParam = "IsSprint";
    [SerializeField] private string attackTrigger = "FireSingle";
    [SerializeField] private string movementSpeedMultiplierParam = "MovementSpeedMultiplier";
    [SerializeField] private float animatorSpeedDampTime = 0.12f;
    [SerializeField, Min(1f)] private float meleeAttackAnimationSpeed = DefaultMeleeAttackAnimationSpeed;

    [Header("Footsteps")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.35f;
    [SerializeField, Min(0f)] private float footstepMovingThreshold = 0.15f;
    [SerializeField, Range(0f, 0.5f)] private float firstFootContactPhase = 0.1f;
    [SerializeField] private Vector2 footstepPitchRange = new Vector2(0.94f, 1.06f);

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
    private Transform meleeWeaponRoot;
    private Transform meleeWeaponHand;
    private GameObject rangedWeaponObject;
    private Renderer meleeBladeRenderer;
    private MaterialPropertyBlock meleeBladeProperties;
    private DodgeRollAnimation dodgeAnimation;
    private AudioSource footstepAudioSource;
    private bool wasMovingForFootsteps;
    private int lastFootstepStateHash;
    private int lastFootContactIndex;
    private float lastFootstepNormalizedTime;

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
    private int observedPlayerAttackSequence = -1;
    private float nextDodgeTime;
    private float dodgeEndTime;
    private bool isDodging;
    private bool evaluatedCurrentAim;
    private float preDodgeAcceleration;
    private float preDodgeStoppingDistance;

    private Vector3 patrolTarget;
    private Vector3 smoothDir;

    public bool HasDetectedPlayer => lastSeenTime > -900f && Time.time <= lastSeenTime + loseSightTime;
    public bool CanCurrentlySeePlayer { get; private set; }

    private void OnEnable()
    {
        if (!ActiveEnemies.Contains(this)) ActiveEnemies.Add(this);
        ThirdPersonShooter.ShotFired += HandlePlayerGunshot;
    }

    private void Awake()
    {
        meleeBladeProperties = new MaterialPropertyBlock();
        agent = GetComponent<NavMeshAgent>();
        perception = GetComponent<EnemyPerception>();
        combat = GetComponent<EnemyCombat>();
        tactics = GetComponent<EnemyTactics>();
        // Also support older/runtime-created enemies whose required components are missing.
        if (agent == null) agent = gameObject.AddComponent<NavMeshAgent>();
        if (perception == null) perception = gameObject.AddComponent<EnemyPerception>();
        if (combat == null) combat = gameObject.AddComponent<EnemyCombat>();
        if (tactics == null) tactics = gameObject.AddComponent<EnemyTactics>();
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
        ActiveEnemies.Remove(this);
        if (health != null)
            health.Damaged -= HandleDamaged;
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
        ThirdPersonShooter.ShotFired -= HandlePlayerGunshot;
        hasHeardGunshot = false;
        if (isDodging) FinishDodge();
        if (animator != null) animator.speed = 1f;
    }

    private void LateUpdate()
    {
        if (enemyType != EnemyType.Melee || meleeWeaponRoot == null || meleeWeaponHand == null) return;

        meleeWeaponRoot.SetPositionAndRotation(
            meleeWeaponHand.position + meleeWeaponHand.rotation * new Vector3(0.02f, 0.01f, 0.03f),
            meleeWeaponHand.rotation * Quaternion.Euler(0f, 90f, 0f));
        UpdateMeleeWeaponTelegraph();
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
        if (health != null && health.IsDead)
        {
            if (animator != null) animator.speed = 1f;
            return;
        }

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                SetupSharedReferences();
            }
        }

        if (isDodging)
        {
            UpdateDodge();
            UpdateAnimator(false);
            FaceMoveDirection();
            KeepUpright();
            return;
        }

        if (TryStartDodgeFromPlayerThreat())
        {
            UpdateDodge();
            UpdateAnimator(false);
            FaceMoveDirection();
            KeepUpright();
            return;
        }

        bool canSee = player != null && perception.CanSeePlayer(enemyType == EnemyType.Ranged);
        CanCurrentlySeePlayer = canSee;
        if (canSee)
        {
            lastSeenTime = Time.time;
            hasHeardGunshot = false;
            if (state == State.Investigate) ChangeState(State.Chase);
        }
        else if (hasHeardGunshot && state != State.Investigate)
        {
            BeginGunshotInvestigation();
        }

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

            case State.Investigate:
                UpdateGunshotInvestigation();
                FaceMoveDirection();
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
            combat.meleeRange = Mathf.Max(combat.meleeRange, DefaultMeleeAttackRange);
            combat.meleeWindup = Mathf.Min(combat.meleeWindup, DefaultMeleeWindup);
            combat.meleeHitRangeGrace = Mathf.Max(combat.meleeHitRangeGrace, DefaultMeleeHitRangeGrace);
        }
        else
        {
            agent.speed = rangedMoveSpeed;
        }

        ConfigureWeaponPresentation();
    }

    private void HandlePlayerGunshot(ThirdPersonShooter shooter, Vector3 position)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || shooter == null || (health != null && health.IsDead)) return;
        if (agent == null || !agent.enabled || !agent.isOnNavMesh || gunshotHearingRange <= 0f) return;
        if (player != null && player != shooter.transform) return;
        if ((position - transform.position).sqrMagnitude > gunshotHearingRange * gunshotHearingRange) return;
        if (player == null)
        {
            player = shooter.transform;
            SetupSharedReferences();
        }

        // Sound reveals a fixed location, not the shooter's future movements.
        lastHeardShotPosition = position;
        heardShotExpiresAt = Time.time + gunshotMemoryDuration;
        gunshotSearchEndsAt = -1f;
        hasHeardGunshot = true;
        if (!isDodging && !perception.CanSeePlayer(enemyType == EnemyType.Ranged))
            BeginGunshotInvestigation();
    }

    private void BeginGunshotInvestigation()
    {
        if (Time.time > heardShotExpiresAt || !NavMesh.SamplePosition(lastHeardShotPosition,
            out NavMeshHit sampled, 2f, agent.areaMask) || !HasCompletePath(sampled.position))
        {
            hasHeardGunshot = false;
            return;
        }
        if (!agent.SetDestination(sampled.position))
        {
            hasHeardGunshot = false;
            return;
        }
        currentCover = null;
        damageChaseEndTime = -999f;
        agent.isStopped = false;
        ChangeState(State.Investigate);
    }

    private void UpdateGunshotInvestigation()
    {
        if (!hasHeardGunshot || Time.time >= heardShotExpiresAt ||
            (!agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete))
        {
            EndGunshotInvestigation();
            return;
        }
        if (agent.pathPending || agent.remainingDistance > Mathf.Max(0.6f, agent.stoppingDistance + 0.1f)) return;
        agent.isStopped = true;
        if (gunshotSearchEndsAt < 0f) gunshotSearchEndsAt = Time.time + gunshotSearchDuration;
        if (Time.time >= gunshotSearchEndsAt) EndGunshotInvestigation();
    }

    private void EndGunshotInvestigation()
    {
        hasHeardGunshot = false;
        gunshotSearchEndsAt = -1f;
        StopChasingAndPatrol();
    }

    private void ConfigureWeaponPresentation()
    {
        if (rangedWeaponObject == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "AssaultRifle")
                {
                    rangedWeaponObject = child.gameObject;
                    break;
                }
            }
        }

        bool isMelee = enemyType == EnemyType.Melee;
        if (rangedWeaponObject != null) rangedWeaponObject.SetActive(!isMelee);

        if (isMelee && meleeWeaponRoot == null) CreateMeleeWeapon();
        if (meleeWeaponRoot != null) meleeWeaponRoot.gameObject.SetActive(isMelee);

        if (animator != null)
        {
            int meleeLayer = animator.GetLayerIndex(MeleeAnimationLayerName);
            if (meleeLayer >= 0) animator.SetLayerWeight(meleeLayer, isMelee ? 1f : 0f);
        }
    }

    private bool TryStartDodgeFromPlayerThreat()
    {
        if (Time.timeScale <= 0f || player == null || agent == null || !agent.enabled || !agent.isOnNavMesh) return false;
        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead) return false;

        bool threatened = false;
        PlayerMelee playerMelee = player.GetComponent<PlayerMelee>();
        if (playerMelee != null && playerMelee.AttackSequence != observedPlayerAttackSequence)
        {
            observedPlayerAttackSequence = playerMelee.AttackSequence;
            Vector3 toEnemy = Vector3.ProjectOnPlane(transform.position - player.position, Vector3.up);
            threatened = playerMelee.IsAttacking && toEnemy.magnitude <= meleeThreatDistance &&
                Vector3.Dot(player.forward, toEnemy.normalized) > 0.3f && HasClearThreatLine(player.position + Vector3.up);
        }

        ThirdPersonInput playerInput = player.GetComponent<ThirdPersonInput>();
        PlayerLoadout playerLoadout = player.GetComponent<PlayerLoadout>();
        bool aiming = playerInput != null && playerInput.AimHeld &&
            (playerLoadout == null || (playerLoadout.IsGunEquipped && playerLoadout.CanAct)) && IsPlayerAimingAtThisEnemy();
        if (!aiming)
        {
            evaluatedCurrentAim = false;
        }
        else if (!evaluatedCurrentAim)
        {
            evaluatedCurrentAim = true;
            threatened = true;
        }

        if (!threatened || Time.time < nextDodgeTime || Random.value >= dodgeChance) return false;
        return StartDodge();
    }

    private bool IsPlayerAimingAtThisEnemy()
    {
        Transform view = Camera.main != null ? Camera.main.transform : player;
        Vector3 target = transform.position + Vector3.up * 1.2f - view.position;
        if (target.sqrMagnitude > aimThreatDistance * aimThreatDistance) return false;
        return Vector3.Dot(view.forward, target.normalized) >= 0.96f && HasClearThreatLine(view.position);
    }

    private bool HasClearThreatLine(Vector3 origin)
    {
        Vector3 offset = transform.position + Vector3.up * 1.2f - origin;
        foreach (RaycastHit hit in Physics.RaycastAll(origin, offset.normalized, offset.magnitude,
            perception != null ? perception.obstacleMask.value : ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform) ||
                hit.transform == player || hit.transform.IsChildOf(player)) continue;
            return false;
        }
        return true;
    }

    private bool StartDodge()
    {
        Vector3 away = transform.position - player.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f) away = transform.forward;
        Vector3 side = Vector3.Cross(Vector3.up, away.normalized);
        if (Random.value < 0.5f) side = -side;
        // Roll along a clear segment instead of following a detour around a wall.
        if (!TryGetDodgeDestination(side, out Vector3 destination) &&
            !TryGetDodgeDestination(-side, out destination)) return false;
        if (!agent.SetDestination(destination)) return false;

        combat?.CancelPendingAttack();
        currentCover = null;
        isDodging = true;
        dodgeEndTime = Time.time + dodgeDuration;
        nextDodgeTime = Time.time + dodgeCooldown;
        lastSeenTime = Time.time;
        preDodgeAcceleration = agent.acceleration;
        preDodgeStoppingDistance = agent.stoppingDistance;
        agent.acceleration = 60f;
        agent.stoppingDistance = 0.1f;
        agent.isStopped = false;
        agent.speed = dodgeSpeed;
        Vector3 rollDirection = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).normalized;
        (modelRoot != null ? modelRoot : transform).rotation = Quaternion.LookRotation(rollDirection, Vector3.up);
        smoothDir = rollDirection;
        ChangeState(State.Chase);

        if (animator != null)
        {
            if (dodgeAnimation == null) dodgeAnimation = animator.GetComponent<DodgeRollAnimation>();
            if (dodgeAnimation == null) dodgeAnimation = animator.gameObject.AddComponent<DodgeRollAnimation>();
            dodgeAnimation.Initialize(animator);
            dodgeAnimation.Play(dodgeDuration);
        }
        return true;
    }

    private void UpdateDodge()
    {
        if (!isDodging) return;
        if (Time.time < dodgeEndTime && agent.enabled && agent.isOnNavMesh) return;

        FinishDodge();
    }

    private bool TryGetDodgeDestination(Vector3 direction, out Vector3 destination)
    {
        destination = transform.position;
        if (!NavMesh.SamplePosition(transform.position + direction * dodgeDistance,
            out NavMeshHit sampled, 0.6f, agent.areaMask)) return false;
        if (Vector3.Distance(transform.position, sampled.position) < dodgeDistance * 0.5f ||
            Mathf.Abs(sampled.position.y - transform.position.y) > 0.5f) return false;
        if (agent.Raycast(sampled.position, out _) || !HasCompletePath(sampled.position)) return false;
        destination = sampled.position;
        return true;
    }

    private void FinishDodge()
    {
        isDodging = false;
        dodgeAnimation?.Cancel();
        agent.acceleration = preDodgeAcceleration;
        agent.stoppingDistance = preDodgeStoppingDistance;
        ApplyTypeState();
        nextRepathTime = 0f;
        ChangeState(State.Chase);
    }

    private void CreateMeleeWeapon()
    {
        if (animator != null && animator.isHuman)
            meleeWeaponHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (meleeWeaponHand == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == "Hand_Right")
                {
                    meleeWeaponHand = child;
                    break;
                }
            }
        }
        if (meleeWeaponHand == null) return;

        GameObject root = new GameObject(MeleeWeaponName);
        root.transform.SetParent(transform, false);
        meleeWeaponRoot = root.transform;

        meleeBladeRenderer = CreateWeaponPart("Blade", root.transform, new Vector3(0f, 0f, 0.62f), new Vector3(0.09f, 0.035f, 1.05f),
            new Color(0.1f, 0.9f, 1f, 1f), new Color(0.1f, 1.6f, 2.2f, 1f));
        CreateWeaponPart("Grip", root.transform, new Vector3(0f, 0f, -0.12f), new Vector3(0.12f, 0.12f, 0.32f),
            new Color(0.08f, 0.1f, 0.13f, 1f), Color.black);
        CreateWeaponPart("Guard", root.transform, new Vector3(0f, 0f, 0.08f), new Vector3(0.38f, 0.08f, 0.08f),
            new Color(0.28f, 0.34f, 0.4f, 1f), Color.black);
    }

    private static Renderer CreateWeaponPart(string partName, Transform parent, Vector3 localPosition, Vector3 localScale,
        Color color, Color emission)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = localScale;

        Collider partCollider = part.GetComponent<Collider>();
        if (partCollider != null) partCollider.enabled = false;
        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer == null) return null;

        MaterialPropertyBlock properties = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        properties.SetColor("_EmissionColor", emission);
        renderer.SetPropertyBlock(properties);
        return renderer;
    }

    private void UpdateMeleeWeaponTelegraph()
    {
        if (meleeBladeRenderer == null) return;

        bool warning = combat != null && combat.IsMeleeTelegraphActive;
        float pulse = warning ? 0.55f + Mathf.PingPong(Time.time * 5f, 0.45f) : 0f;
        Color color = warning ? Color.Lerp(MeleeBladeColor, MeleeWarningColor, pulse) : MeleeBladeColor;
        Color emission = warning ? color * 3.2f : MeleeBladeColor * 1.8f;
        meleeBladeRenderer.GetPropertyBlock(meleeBladeProperties);
        meleeBladeProperties.SetColor("_BaseColor", color);
        meleeBladeProperties.SetColor("_Color", color);
        meleeBladeProperties.SetColor("_EmissionColor", emission);
        meleeBladeRenderer.SetPropertyBlock(meleeBladeProperties);
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

        if (agent.pathPending) return;

        // A nearby boundary can interrupt the normal destination wait, but failed
        // escape searches keep the same one-second retry delay as patrol searches.
        if (Time.time >= nextPatrolBoundaryCheckTime)
        {
            nextPatrolBoundaryCheckTime = Time.time + Mathf.Max(0.05f, patrolBoundaryCheckInterval);
            if (TryRedirectPatrolFromBoundary()) return;
        }

        if (Time.time < nextPatrolPickTime) return;

        if (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            PickNewPatrolTarget(true);
            return;
        }

        if (agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            PickNewPatrolTarget(false);
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

        float dist = perception.DistanceToPlayer();
        if (dist <= combat.GetAttackRange())
        {
            EnterAttackState();
            return;
        }

        if (Time.time >= nextRepathTime)
        {
            nextRepathTime = Time.time + repathInterval;
            agent.isStopped = false;
            agent.SetDestination(enemyType == EnemyType.Melee ? GetMeleeSurroundDestination() : player.position);
        }
    }

    private void UpdateAttack(bool canSee)
    {
        if (TryStartTakeCover(canSee)) return;

        float dist = perception.DistanceToPlayer();

        bool meleeSwingInProgress = enemyType == EnemyType.Melee && combat.IsAttackPending();
        if (dist > combat.GetAttackRange() + 0.5f && !meleeSwingInProgress)
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

        TryPerformAttack();
    }

    private void EnterAttackState()
    {
        agent.isStopped = enemyType != EnemyType.Ranged;
        ChangeState(State.Attack);
        if (enemyType == EnemyType.Melee)
        {
            FaceTarget(player.position);
            TryPerformAttack();
        }
    }

    private void TryPerformAttack()
    {
        if (combat.CanAttackNow())
        {
            combat.MarkAttackUsed();
            TriggerAttackAnimation();
            combat.TryAttack();
        }
    }

    private Vector3 GetMeleeSurroundDestination()
    {
        int slotIndex = 0;
        EntityId ownId = GetEntityId();
        for (int i = ActiveEnemies.Count - 1; i >= 0; i--)
        {
            EnemyAI candidate = ActiveEnemies[i];
            if (candidate == null)
            {
                ActiveEnemies.RemoveAt(i);
                continue;
            }
            if (candidate == this || candidate.enemyType != EnemyType.Melee || candidate.player != player) continue;
            if (candidate.health != null && candidate.health.IsDead) continue;
            if (candidate.GetEntityId() < ownId) slotIndex++;
        }

        Vector3 playerForward = player.forward;
        playerForward.y = 0f;
        if (playerForward.sqrMagnitude < 0.001f) playerForward = Vector3.forward;
        Quaternion playerFacing = Quaternion.LookRotation(playerForward.normalized, Vector3.up);
        Vector3 offset = CalculateSurroundOffset(slotIndex, meleeSurroundRadius, meleeSurroundRingSpacing);
        Vector3 desired = player.position + playerFacing * offset;
        if (NavMesh.SamplePosition(desired, out NavMeshHit sampled, meleeSurroundSampleRadius, NavMesh.AllAreas))
            return sampled.position;
        return player.position;
    }

    public static Vector3 CalculateSurroundOffset(int slotIndex, float innerRadius, float ringSpacing)
    {
        int safeIndex = Mathf.Max(0, slotIndex);
        int ring = safeIndex / SurroundSlotsPerRing;
        int slot = safeIndex % SurroundSlotsPerRing;
        float angle = slot * (360f / SurroundSlotsPerRing) + ring * (180f / SurroundSlotsPerRing);
        float radius = Mathf.Max(0.4f, innerRadius) + ring * Mathf.Max(0.4f, ringSpacing);
        return Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * radius;
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
            SchedulePatrolRetry();
            return;
        }

        patrolTarget = target;

        agent.isStopped = false;
        if (!agent.SetDestination(patrolTarget))
        {
            SchedulePatrolRetry();
            return;
        }

        nextPatrolPickTime = immediate ? Time.time : Time.time + Random.Range(2f, 5f);
    }

    private void SchedulePatrolRetry()
    {
        nextPatrolPickTime = Time.time + 1f;
        nextPatrolBoundaryCheckTime = nextPatrolPickTime;
    }

    private bool TryRedirectPatrolFromBoundary()
    {
        if (!TryGetPatrolBoundaryDirection(transform.position, out Vector3 away)) return false;

        Vector3 moveDirection = Vector3.ProjectOnPlane(agent.steeringTarget - transform.position, Vector3.up);
        if (agent.hasPath && agent.pathStatus == NavMeshPathStatus.PathComplete &&
            Vector3.Dot(moveDirection.normalized, away) > 0.35f) return false;

        float distance = Mathf.Max(patrolBoundaryTurnDistance, (patrolBoundaryDistance + agent.radius) * 2f);
        for (int i = 0; i < 5; i++)
        {
            float angle = i == 0 ? 0f : ((i + 1) / 2) * 35f * (i % 2 == 0 ? -1f : 1f);
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * away;
            Vector3 candidate = transform.position + direction * distance;
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.75f, agent.areaMask)) continue;
            if (Vector3.Dot(hit.position - transform.position, away) < patrolBoundaryDistance) continue;
            if (TryGetPatrolBoundaryDirection(hit.position, out _)) continue;
            if (agent.Raycast(hit.position, out _) || !HasCompletePath(hit.position)) continue;
            if (!agent.SetDestination(hit.position)) continue;

            patrolTarget = hit.position;
            agent.isStopped = false;
            nextPatrolPickTime = Time.time + 1f;
            return true;
        }

        // Do not keep pushing into the wall while waiting for a reachable escape.
        agent.ResetPath();
        SchedulePatrolRetry();
        return true;
    }

    private bool TryGetPatrolBoundaryDirection(Vector3 position, out Vector3 away)
    {
        away = Vector3.zero;
        float radius = Mathf.Max(0.1f, patrolBoundaryDistance) + agent.radius;
        Vector3 center = position + Vector3.up * Mathf.Max(agent.radius, agent.height * 0.5f);
        int count;
        // Reuse the buffer; grow only when a crowded area fills it.
        while (true)
        {
            count = Physics.OverlapSphereNonAlloc(center, radius, patrolBoundaryColliders,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            if (count < patrolBoundaryColliders.Length) break;
            System.Array.Resize(ref patrolBoundaryColliders, patrolBoundaryColliders.Length * 2);
        }

        bool found = false;
        for (int i = 0; i < count; i++)
        {
            Collider wall = patrolBoundaryColliders[i];
            if (wall == null || wall.transform.IsChildOf(transform) || !IsPatrolBoundary(wall.transform)) continue;
            Vector3 offset = Vector3.ProjectOnPlane(center - wall.ClosestPoint(center), Vector3.up);
            if (offset.sqrMagnitude > radius * radius) continue;
            if (offset.sqrMagnitude < 0.0001f)
                offset = Vector3.ProjectOnPlane(center - wall.bounds.center, Vector3.up);
            if (offset.sqrMagnitude < 0.0001f) offset = -transform.forward;
            away += offset.normalized / Mathf.Max(offset.magnitude, 0.1f);
            found = true;
        }
        System.Array.Clear(patrolBoundaryColliders, 0, count);

        if (found)
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
        return found;
    }

    private bool IsPatrolBoundary(Transform candidate)
    {
        for (Transform current = candidate; current != null; current = current.parent)
        {
            // String comparison also works in scenes where the optional tag is not registered.
            if (current.tag == PatrolBoundaryTag) return true;
        }
        return false;
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
            if (TryGetPatrolBoundaryDirection(candidate, out _)) continue;
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
            if (TryGetPatrolBoundaryDirection(hit.position, out _)) continue;
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

        animator.speed = enemyType == EnemyType.Melee && state == State.Attack
            ? Mathf.Max(1f, meleeAttackAnimationSpeed)
            : 1f;
        float maxSpeed = Mathf.Max(agent.speed, 0.01f);
        float speed01 = agent.isStopped ? 0f : Mathf.Clamp01(agent.velocity.magnitude / maxSpeed);
        bool isMoving = speed01 > 0.05f;
        bool isAiming = enemyType == EnemyType.Ranged && (canSee || state == State.Attack || state == State.PeekShoot);

        SetAnimatorFloat(moveSpeedParam, speed01);
        SetAnimatorFloat(movementSpeedMultiplierParam, enemyType == EnemyType.Melee ? MeleeSpeedMultiplier : 1f);
        SetAnimatorBool(isGroundedParam, true);
        SetAnimatorBool(isSprintParam, isMoving && enemyType == EnemyType.Melee);
        SetAnimatorBool(isAimParam, isAiming);
        UpdateFootsteps(isMoving && agent.velocity.magnitude >= footstepMovingThreshold);
    }

    public void ConfigureFootsteps(AudioClip clip, float volume)
    {
        footstepClip = clip;
        footstepVolume = Mathf.Clamp01(volume);
        EnsureFootstepAudioSource();
    }

    private void UpdateFootsteps(bool isMoving)
    {
        if (!isMoving || footstepClip == null)
        {
            wasMovingForFootsteps = false;
            return;
        }

        AnimatorStateInfo animationState = animator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = animationState.normalizedTime;
        int contactIndex = Mathf.FloorToInt((normalizedTime - firstFootContactPhase) * 2f);

        if (!wasMovingForFootsteps || animationState.fullPathHash != lastFootstepStateHash ||
            normalizedTime < lastFootstepNormalizedTime)
        {
            wasMovingForFootsteps = true;
            lastFootstepStateHash = animationState.fullPathHash;
            lastFootContactIndex = contactIndex;
            lastFootstepNormalizedTime = normalizedTime;
            return;
        }

        if (contactIndex > lastFootContactIndex)
        {
            PlayFootstep();
            lastFootContactIndex = contactIndex;
        }

        lastFootstepNormalizedTime = normalizedTime;
    }

    private void EnsureFootstepAudioSource()
    {
        if (footstepAudioSource != null) return;

        GameObject audioObject = new GameObject("FootstepAudio");
        audioObject.transform.SetParent(transform, false);
        footstepAudioSource = audioObject.AddComponent<AudioSource>();
        footstepAudioSource.playOnAwake = false;
        footstepAudioSource.loop = false;
        footstepAudioSource.spatialBlend = 1f;
        footstepAudioSource.dopplerLevel = 0f;
        footstepAudioSource.rolloffMode = AudioRolloffMode.Linear;
        footstepAudioSource.minDistance = 1.5f;
        footstepAudioSource.maxDistance = 22f;
    }

    private void PlayFootstep()
    {
        EnsureFootstepAudioSource();
        footstepAudioSource.pitch = Random.Range(
            Mathf.Min(footstepPitchRange.x, footstepPitchRange.y),
            Mathf.Max(footstepPitchRange.x, footstepPitchRange.y));
        footstepAudioSource.PlayOneShot(footstepClip, footstepVolume);
    }

    private void TriggerAttackAnimation()
    {
        if (animator == null) return;

        if (enemyType == EnemyType.Melee)
        {
            int meleeLayer = animator.GetLayerIndex(MeleeAnimationLayerName);
            if (meleeLayer >= 0)
            {
                animator.SetLayerWeight(meleeLayer, 1f);
                animator.Play(MeleeAttackStateName, meleeLayer, 0f);
                return;
            }
        }

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
