using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class EnemySpawnRepair
{
    private const string MeleeEnemyPrefabPath = "Assets/SciFiWarriorPBRHPPolyart/Prefabs/MeleeEnemy.prefab";
    private const string RangedEnemyPrefabPath = "Assets/SciFiWarriorPBRHPPolyart/Prefabs/RangedEnemy.prefab";
    private const string ColonialScenePath = "Assets/Colonial City LittlePack \u2013 Church Graveyard Environment/Scenes/Colonial_Graveyard.unity";

    [MenuItem("Tools/TPS/Repair Enemy Spawning")]
    public static void RepairActiveSceneEnemySpawning()
    {
        RepairEnemyPrefab(MeleeEnemyPrefabPath, EnemyType.Melee);
        RepairEnemyPrefab(RangedEnemyPrefabPath, EnemyType.Ranged);
        RepairStageManagers(SceneManager.GetActiveScene());

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        Debug.Log("Enemy spawning repaired for active scene.");
    }

    [MenuItem("Tools/TPS/Repair Colonial Graveyard Enemy Spawning")]
    public static void RepairColonialGraveyardEnemySpawning()
    {
        RepairEnemyPrefab(MeleeEnemyPrefabPath, EnemyType.Melee);
        RepairEnemyPrefab(RangedEnemyPrefabPath, EnemyType.Ranged);

        Scene scene = EditorSceneManager.OpenScene(ColonialScenePath, OpenSceneMode.Single);
        RepairStageManagers(scene);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("Enemy spawning repaired for Colonial_Graveyard.");
    }

    private static void RepairEnemyPrefab(string path, EnemyType enemyType)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            root.name = enemyType == EnemyType.Ranged ? "RangedEnemy" : "MeleeEnemy";

            Animator animator = root.GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            NavMeshAgent agent = EnsureComponent<NavMeshAgent>(root);
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.speed = enemyType == EnemyType.Ranged ? 2.2f : EnemyAI.DefaultMeleeMoveSpeed;
            agent.angularSpeed = 120f;
            agent.acceleration = 8f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            CapsuleCollider collider = EnsureComponent<CapsuleCollider>(root);
            collider.radius = 0.5f;
            collider.height = 2f;
            collider.center = new Vector3(0f, 1f, 0f);
            collider.direction = 1;

            EnemyHealth health = EnsureComponent<EnemyHealth>(root);
            float startingHealth = EnemyHealth.GetStartingHealth(enemyType);
            health.maxHealth = startingHealth;
            health.currentHealth = startingHealth;

            EnemyAI ai = EnsureComponent<EnemyAI>(root);
            ai.enemyType = enemyType;
            ai.head = root.transform;
            ai.modelRoot = root.transform;
            ai.health = health;
            ai.turnSpeed = 50f;

            EnemyCombat combat = EnsureComponent<EnemyCombat>(root);
            combat.enemyType = enemyType;
            combat.self = root.transform;
            combat.player = null;
            combat.rangedRange = 18f;
            combat.accuracy = enemyType == EnemyType.Ranged ? 0.5f : combat.accuracy;
            combat.spreadAngle = enemyType == EnemyType.Ranged ? 18f : combat.spreadAngle;

            EnemyPerception perception = EnsureComponent<EnemyPerception>(root);
            perception.self = root.transform;
            perception.head = root.transform;
            perception.player = null;
            perception.viewDistance = enemyType == EnemyType.Ranged ? 2400f : 1200f;
            perception.rangedDetectDistance = enemyType == EnemyType.Ranged ? 2400f : 1200f;
            perception.horizontalViewAngleTotal = enemyType == EnemyType.Ranged ? 30f : 240f;
            perception.viewAngleTotal = perception.horizontalViewAngleTotal;
            perception.verticalViewAngleUp = 70f;
            perception.verticalViewAngleDown = 70f;
            perception.useLineOfSight = true;

            EnemyTactics tactics = EnsureComponent<EnemyTactics>(root);
            tactics.enemyType = enemyType;
            tactics.self = root.transform;
            tactics.player = null;
            tactics.health = health;

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void RepairStageManagers(Scene scene)
    {
        EnemyAI meleePrefab = AssetDatabase.LoadAssetAtPath<EnemyAI>(MeleeEnemyPrefabPath);
        EnemyAI rangedPrefab = AssetDatabase.LoadAssetAtPath<EnemyAI>(RangedEnemyPrefabPath);
        GameObject meleePrefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(MeleeEnemyPrefabPath);
        GameObject rangedPrefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(RangedEnemyPrefabPath);

        foreach (StageManager manager in Object.FindObjectsByType<StageManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (manager.gameObject.scene != scene) continue;

            SerializedObject serializedManager = new SerializedObject(manager);
            SerializedProperty enemyPrefabs = serializedManager.FindProperty("enemyPrefabs");
            enemyPrefabs.arraySize = 2;
            enemyPrefabs.GetArrayElementAtIndex(0).objectReferenceValue = meleePrefab;
            enemyPrefabs.GetArrayElementAtIndex(1).objectReferenceValue = rangedPrefab;

            SerializedProperty enemyPrefabObjects = serializedManager.FindProperty("enemyPrefabObjects");
            enemyPrefabObjects.arraySize = 2;
            enemyPrefabObjects.GetArrayElementAtIndex(0).objectReferenceValue = meleePrefabObject;
            enemyPrefabObjects.GetArrayElementAtIndex(1).objectReferenceValue = rangedPrefabObject;

            serializedManager.FindProperty("player").objectReferenceValue = FindPlayer(scene);
            serializedManager.FindProperty("statusUI").objectReferenceValue = FindInScene<StageStatusUI>(scene);

            PatrolArea[] patrolAreas = Object.FindObjectsByType<PatrolArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            SerializedProperty patrolAreasProperty = serializedManager.FindProperty("patrolAreas");
            patrolAreasProperty.arraySize = patrolAreas.Length;
            for (int i = 0; i < patrolAreas.Length; i++)
            {
                patrolAreasProperty.GetArrayElementAtIndex(i).objectReferenceValue = patrolAreas[i];
            }

            serializedManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }
    }

    private static Transform FindPlayer(Scene scene)
    {
        foreach (PlayerHealth health in Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (health.gameObject.scene == scene) return health.transform;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.CompareTag("Player")) return root.transform;
            Transform taggedChild = FindTaggedChild(root.transform, "Player");
            if (taggedChild != null) return taggedChild;
        }

        return null;
    }

    private static T FindInScene<T>(Scene scene) where T : Object
    {
        foreach (T item in Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            Component component = item as Component;
            if (component != null && component.gameObject.scene == scene) return item;
        }

        return null;
    }

    private static Transform FindTaggedChild(Transform root, string tag)
    {
        foreach (Transform child in root)
        {
            if (child.CompareTag(tag)) return child;
            Transform found = FindTaggedChild(child, tag);
            if (found != null) return found;
        }

        return null;
    }

    private static T EnsureComponent<T>(GameObject root) where T : Component
    {
        T component = root.GetComponent<T>();
        return component != null ? component : root.AddComponent<T>();
    }
}
