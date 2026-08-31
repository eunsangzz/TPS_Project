using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StageManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAI[] enemyPrefabs;
    [SerializeField] private GameObject[] enemyPrefabObjects;
    [SerializeField] private Transform player;
    [SerializeField] private StageStatusUI statusUI;
    [SerializeField] private PatrolArea[] patrolAreas;

    [Header("Stage")]
    [SerializeField] private int firstStageEnemyCount = 3;
    [SerializeField] private int enemiesAddedPerStage = 2;
    [SerializeField] private float nextStageDelay = 3f;
    [SerializeField] private bool startOnAwake = true;

    [Header("Spawn")]
    [Tooltip("Spawn across the baked NavMesh. Disable to use the manual bounds and center/edge limits.")]
    [SerializeField] private bool useEntireNavMesh = true;
    [SerializeField] private Vector3 spawnBoundsCenter = new Vector3(550f, 25f, 500f);
    [SerializeField] private Vector3 spawnBoundsSize = new Vector3(900f, 80f, 900f);
    [SerializeField] private float spawnAreaEdgeInset = 0.22f;
    [SerializeField] private float spawnCenterBias = 0.65f;
    [SerializeField] private float minDistanceFromPlayer = 25f;
    [SerializeField] private float navMeshSampleRadius = 8f;
    [SerializeField] private int spawnAttemptsPerEnemy = 80;

    [Header("Enemy Senses")]
    [SerializeField] private float mapWideDetectDistance = 1200f;
    [SerializeField] private float rangedVisionDistance = 2400f;
    [SerializeField] private float rangedHorizontalVisionAngle = 30f;
    [SerializeField] private float rangedVerticalVisionUp = 70f;
    [SerializeField] private float rangedVerticalVisionDown = 70f;
    [SerializeField] private bool ignoreLineOfSight = false;

    private readonly List<EnemyHealth> aliveEnemies = new List<EnemyHealth>();
    private int currentStage;
    private bool startingNextStage;
    private bool warnedMissingEnemyPrefabs;
    private NavMeshTriangulation spawnMesh;
    private readonly List<int> spawnTriangles = new List<int>();
    private readonly List<float> spawnTriangleAreas = new List<float>();
    private NavMeshPath spawnPath;
    private float totalSpawnArea;
    private SkillSelectionUI skillSelection;

    public int CurrentStage => currentStage;
    public bool IsChoosingSkill => skillSelection != null && skillSelection.IsOpen;

#if UNITY_EDITOR
    private GameObject[] editorFallbackEnemyPrefabObjects;
#endif

    private void Awake()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null) player = playerObject.transform;
        }

        if (statusUI == null) statusUI = FindFirstObjectByType<StageStatusUI>();
        if (patrolAreas == null || patrolAreas.Length == 0) patrolAreas = FindObjectsByType<PatrolArea>(FindObjectsSortMode.None);

        EnsureSightIndicatorUI();
    }

    private void Start()
    {
        if (startOnAwake)
        {
            BeginNextStage();
        }
    }

    private void BeginNextStage()
    {
        startingNextStage = false;
        currentStage++;

        ThirdPersonShooter shooter = player != null ? player.GetComponent<ThirdPersonShooter>() : null;
        if (shooter != null) shooter.ResetAmmoForStage();
        PlayerMelee melee = player != null ? player.GetComponent<PlayerMelee>() : null;
        if (melee != null) melee.CancelAttack();

        int enemyCount = Mathf.Max(1, firstStageEnemyCount + (currentStage - 1) * enemiesAddedPerStage);
        SpawnStage(enemyCount);
        RefreshUI();
    }

    private void SpawnStage(int enemyCount)
    {
        RebuildSpawnMesh();
        for (int i = 0; i < enemyCount; i++)
        {
            GameObject prefabObject = PickEnemyPrefabObject();
            if (prefabObject == null)
            {
                WarnMissingEnemyPrefabs();
                continue;
            }

            if (!TryFindSpawnPosition(out Vector3 position))
            {
                Debug.LogWarning("[StageManager] No valid spawn position. Check baked NavMesh coverage, player connectivity, and Min Distance From Player. No forward fallback was used.", this);
                continue;
            }
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            GameObject enemyObject = Instantiate(prefabObject, position, rotation);
            EnemyAI enemy = EnsureSpawnedEnemyComponents(enemyObject, InferEnemyType(prefabObject));
            enemy.name = $"{prefabObject.name}_Stage{currentStage}_{i + 1}";
            ConfigureEnemy(enemy);

            EnemyHealth health = enemy.GetComponent<EnemyHealth>();
            if (health != null)
            {
                health.Died += HandleEnemyDied;
                aliveEnemies.Add(health);
            }
        }
    }

    private EnemyAI PickEnemyPrefab(int index)
    {
        if (enemyPrefabs == null || enemyPrefabs.Length == 0) return null;

        if (enemyPrefabs.Length == 1) return enemyPrefabs[0];
        return enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
    }

    private GameObject PickEnemyPrefabObject()
    {
        EnemyAI enemyPrefab = PickEnemyPrefab(0);
        if (enemyPrefab != null) return enemyPrefab.gameObject;

        GameObject[] prefabObjects = GetEnemyPrefabObjects();
        if (prefabObjects == null || prefabObjects.Length == 0) return null;

        if (prefabObjects.Length == 1) return prefabObjects[0];
        return prefabObjects[Random.Range(0, prefabObjects.Length)];
    }

    private GameObject[] GetEnemyPrefabObjects()
    {
        if (enemyPrefabObjects != null && enemyPrefabObjects.Length > 0) return enemyPrefabObjects;

#if UNITY_EDITOR
        if (editorFallbackEnemyPrefabObjects == null || editorFallbackEnemyPrefabObjects.Length == 0)
        {
            editorFallbackEnemyPrefabObjects = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/MeleeEnemy.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/RangedEnemy.prefab")
            };
        }

        return editorFallbackEnemyPrefabObjects;
#else
        return null;
#endif
    }

    private EnemyAI EnsureSpawnedEnemyComponents(GameObject enemyObject, EnemyType enemyType)
    {
        NavMeshAgent agent = EnsureComponent<NavMeshAgent>(enemyObject);
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.speed = enemyType == EnemyType.Ranged ? 2.2f : 3.6f;
        agent.angularSpeed = 120f;
        agent.acceleration = 8f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        CapsuleCollider capsule = EnsureComponent<CapsuleCollider>(enemyObject);
        capsule.radius = 0.5f;
        capsule.height = 2f;
        capsule.center = new Vector3(0f, 1f, 0f);
        capsule.direction = 1;

        EnemyHealth health = EnsureComponent<EnemyHealth>(enemyObject);
        health.maxHealth = 100f;

        EnemyAI enemy = EnsureComponent<EnemyAI>(enemyObject);
        enemy.enemyType = enemyType;
        enemy.head = enemyObject.transform;
        enemy.modelRoot = enemyObject.transform;
        enemy.health = health;
        enemy.turnSpeed = 50f;

        EnemyCombat combat = EnsureComponent<EnemyCombat>(enemyObject);
        combat.enemyType = enemyType;
        combat.self = enemyObject.transform;

        EnemyPerception perception = EnsureComponent<EnemyPerception>(enemyObject);
        perception.self = enemyObject.transform;
        perception.head = enemyObject.transform;

        EnemyTactics tactics = EnsureComponent<EnemyTactics>(enemyObject);
        tactics.enemyType = enemyType;
        tactics.self = enemyObject.transform;
        tactics.health = health;

        Animator animator = enemyObject.GetComponent<Animator>();
        if (animator != null) animator.applyRootMotion = false;

        return enemy;
    }

    private EnemyType InferEnemyType(GameObject prefabObject)
    {
        EnemyAI enemy = prefabObject.GetComponent<EnemyAI>();
        if (enemy != null) return enemy.enemyType;

        return prefabObject.name.ToLowerInvariant().Contains("ranged") ? EnemyType.Ranged : EnemyType.Melee;
    }

    private T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private void WarnMissingEnemyPrefabs()
    {
        if (warnedMissingEnemyPrefabs) return;

        warnedMissingEnemyPrefabs = true;
        Debug.LogError("[StageManager] Enemy prefab slots are empty. Assign MeleeEnemy/RangedEnemy to Enemy Prefab Objects, or keep the default editor fallback paths available.", this);
    }

    private void ConfigureEnemy(EnemyAI enemy)
    {
        ignoreLineOfSight = false;

        enemy.player = player;
        enemy.patrolAreas = patrolAreas;
        enemy.loseSightTime = 9999f;
        enemy.repathInterval = 0.15f;

        EnemyPerception perception = enemy.GetComponent<EnemyPerception>();
        if (perception != null)
        {
            perception.player = player;
            perception.self = enemy.transform;
            perception.head = enemy.head != null ? enemy.head : enemy.transform;
            if (enemy.enemyType == EnemyType.Ranged)
            {
                perception.viewDistance = rangedVisionDistance;
                perception.rangedDetectDistance = rangedVisionDistance;
                perception.viewAngleTotal = rangedHorizontalVisionAngle;
                perception.horizontalViewAngleTotal = rangedHorizontalVisionAngle;
                perception.verticalViewAngleUp = rangedVerticalVisionUp;
                perception.verticalViewAngleDown = rangedVerticalVisionDown;
                perception.useLineOfSight = !ignoreLineOfSight;
            }
            else
            {
                perception.viewDistance = mapWideDetectDistance;
                perception.rangedDetectDistance = mapWideDetectDistance;
                perception.viewAngleTotal = 360f;
                perception.horizontalViewAngleTotal = 360f;
                perception.useLineOfSight = !ignoreLineOfSight;
            }
        }

        EnemyCombat combat = enemy.GetComponent<EnemyCombat>();
        if (combat != null && enemy.enemyType == EnemyType.Ranged)
        {
            combat.rangedRange *= 2f;
            combat.accuracy = 0.5f;
            combat.spreadAngle = 18f;
        }

        NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
        if (agent != null && agent.enabled)
        {
            agent.Warp(enemy.transform.position);
        }
    }

    private void RebuildSpawnMesh()
    {
        spawnMesh = NavMesh.CalculateTriangulation();
        spawnTriangles.Clear();
        spawnTriangleAreas.Clear();
        totalSpawnArea = 0f;
        for (int i = 0; i < spawnMesh.indices.Length; i += 3)
        {
            Vector3 a = spawnMesh.vertices[spawnMesh.indices[i]];
            Vector3 b = spawnMesh.vertices[spawnMesh.indices[i + 1]];
            Vector3 c = spawnMesh.vertices[spawnMesh.indices[i + 2]];
            float area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            if (area <= 0.0001f) continue;
            totalSpawnArea += area;
            spawnTriangles.Add(i);
            spawnTriangleAreas.Add(totalSpawnArea);
        }
    }

    private bool TryFindSpawnPosition(out Vector3 position)
    {
        position = Vector3.zero;
        if (spawnTriangles.Count == 0) return false;
        if (spawnPath == null) spawnPath = new NavMeshPath();
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        Vector3 playerAnchor = Vector3.zero;
        if (player != null)
        {
            if (!NavMesh.SamplePosition(player.position, out NavMeshHit playerHit, Mathf.Max(0.5f, navMeshSampleRadius), filter))
                return false;
            playerAnchor = playerHit.position;
        }

        bool found = false;
        float bestSpacing = -1f;
        int validCandidates = 0;
        for (int i = 0; i < Mathf.Max(1, spawnAttemptsPerEnemy); i++)
        {
            Vector3 candidate = PickSpawnCandidate();
            // Candidates already lie on triangles: only correct small precision errors,
            // rather than pulling distant points onto the same nearby NavMesh edge.
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 0.5f, filter)) continue;
            if (!useEntireNavMesh && !IsInsideBiasedSpawnBounds(hit.position)) continue;
            if (player != null && HorizontalDistanceSquared(hit.position, player.position) < minDistanceFromPlayer * minDistanceFromPlayer)
                continue;
            if (player != null && (!NavMesh.CalculatePath(hit.position, playerAnchor, filter, spawnPath) ||
                spawnPath.status != NavMeshPathStatus.PathComplete)) continue;

            float spacing = float.PositiveInfinity;
            foreach (EnemyHealth enemy in aliveEnemies)
            {
                if (enemy == null || enemy.IsDead) continue;
                spacing = Mathf.Min(spacing, HorizontalDistanceSquared(hit.position, enemy.transform.position));
            }
            if (!found || spacing > bestSpacing)
            {
                found = true;
                bestSpacing = spacing;
                position = hit.position;
            }
            // First spawn is uniform by surface area; later spawns prefer open gaps.
            if (float.IsPositiveInfinity(spacing) || ++validCandidates >= 24) break;
        }
        return found;
    }

    private Vector3 PickSpawnCandidate()
    {
        float area = Random.value * totalSpawnArea;
        int triangle = spawnTriangleAreas.BinarySearch(area);
        if (triangle < 0) triangle = ~triangle;
        int index = spawnTriangles[Mathf.Min(triangle, spawnTriangles.Count - 1)];
        Vector3 a = spawnMesh.vertices[spawnMesh.indices[index]];
        Vector3 b = spawnMesh.vertices[spawnMesh.indices[index + 1]];
        Vector3 c = spawnMesh.vertices[spawnMesh.indices[index + 2]];
        float u = Mathf.Sqrt(Random.value);
        float v = Random.value;
        return (1f - u) * a + u * (1f - v) * b + u * v * c;
    }

    private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
    {
        float x = a.x - b.x;
        float z = a.z - b.z;
        return x * x + z * z;
    }

    private bool IsInsideBiasedSpawnBounds(Vector3 point)
    {
        float bias = Mathf.Min(Mathf.Clamp01(spawnCenterBias), 1f - 2f * Mathf.Clamp(spawnAreaEdgeInset, 0f, 0.49f));
        Vector3 half = spawnBoundsSize * 0.5f * bias;
        Vector3 delta = point - spawnBoundsCenter;

        return Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.z) <= half.z;
    }

    private void HandleEnemyDied(EnemyHealth enemy)
    {
        enemy.Died -= HandleEnemyDied;
        if (!aliveEnemies.Remove(enemy)) return;
        RefreshUI();

        if (aliveEnemies.Count == 0 && !startingNextStage)
        {
            StartCoroutine(StartNextStageAfterDelay());
        }
    }

    private IEnumerator StartNextStageAfterDelay()
    {
        startingNextStage = true;
        RefreshUI();
        yield return new WaitForSeconds(nextStageDelay);
        PlayerHealth health = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (player == null || (health != null && health.IsDead)) yield break;
        PlayerSkills skills = player.GetComponent<PlayerSkills>();
        if (skills == null) skills = player.gameObject.AddComponent<PlayerSkills>();
        if (skillSelection == null)
        {
            GameObject root = new GameObject("SkillSelectionUI", typeof(RectTransform), typeof(SkillSelectionUI));
            root.transform.SetParent(transform, false);
            skillSelection = root.GetComponent<SkillSelectionUI>();
        }
        skillSelection.Show(skills, currentStage, BeginNextStage);
    }

    private void RefreshUI()
    {
        if (statusUI != null)
        {
            statusUI.SetStageStatus(currentStage, aliveEnemies.Count, startingNextStage);
        }
    }

    private void EnsureSightIndicatorUI()
    {
        if (FindFirstObjectByType<EnemySightIndicatorUI>() != null) return;

        Canvas canvas = statusUI != null ? statusUI.GetComponentInParent<Canvas>() : FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject indicatorObject = new GameObject("EnemySightIndicatorUI", typeof(RectTransform), typeof(EnemySightIndicatorUI));
        indicatorObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = indicatorObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        foreach (EnemyHealth enemy in aliveEnemies)
            if (enemy != null) enemy.Died -= HandleEnemyDied;
        if (skillSelection != null) skillSelection.Cancel();
    }
}
