using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public static class FloodedGroundsSceneSetup
{
    private const string SourceEnvironmentScene = "Assets/Flooded_Grounds/Scenes/Scene_A.unity";
    private const string SourceGameplayScene = "Assets/Scenes/MainScene.unity";
    private const string TargetScene = "Assets/Scenes/FloodedGrounds_TPS.unity";

    [MenuItem("Tools/TPS/Build Flooded Grounds TPS Scene")]
    public static void BuildFloodedGroundsTpsScene()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) != null)
        {
            AssetDatabase.DeleteAsset(TargetScene);
        }

        if (!AssetDatabase.CopyAsset(SourceEnvironmentScene, TargetScene))
        {
            throw new System.InvalidOperationException($"Could not copy scene from {SourceEnvironmentScene} to {TargetScene}");
        }

        AssetDatabase.ImportAsset(TargetScene);

        Scene targetScene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        RemoveSampleController(targetScene);
        RemoveDuplicateGameplay(targetScene);

        Scene gameplayScene = EditorSceneManager.OpenScene(SourceGameplayScene, OpenSceneMode.Additive);
        MoveGameplayObjects(gameplayScene, targetScene);
        EditorSceneManager.CloseScene(gameplayScene, true);

        PositionGameplay(targetScene);
        BuildSceneNavMesh(targetScene);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Built {TargetScene}");
    }

    public static void VerifyFloodedGroundsTpsScene()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        int players = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int cameras = Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int huds = Object.FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int navMeshSurfaces = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int stageManagers = Object.FindObjectsByType<StageManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int stageStatusUis = Object.FindObjectsByType<StageStatusUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int sightIndicatorUis = Object.FindObjectsByType<EnemySightIndicatorUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x.gameObject.scene == scene);
        int sampleControllers = scene.GetRootGameObjects()
            .Count(root => FindDeepChild(root.transform, "FpsController") != null);
        EnemyPerception firstPerception = Object.FindObjectsByType<EnemyPerception>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);

        Debug.Log($"FloodedGrounds_TPS verify: players={players}, cameras={cameras}, huds={huds}, eventSystems={eventSystems}, enemies={enemies}, navMeshSurfaces={navMeshSurfaces}, stageManagers={stageManagers}, stageStatusUis={stageStatusUis}, sightIndicatorUis={sightIndicatorUis}, sampleFpsControllers={sampleControllers}");
        if (firstPerception != null)
        {
            Debug.Log($"FloodedGrounds_TPS perception verify: viewDistance={firstPerception.viewDistance}, rangedDetectDistance={firstPerception.rangedDetectDistance}, viewAngleTotal={firstPerception.viewAngleTotal}, useLineOfSight={firstPerception.useLineOfSight}");
        }
    }

    [MenuItem("Tools/TPS/Configure Flooded Grounds Map-Wide Tracking")]
    public static void ConfigureMapWideTracking()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

        ConfigureEnemyPerception(scene);
        ConfigureMapWidePatrol(scene);
        BuildSceneNavMesh(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Configured FloodedGrounds_TPS for map-wide enemy tracking and rebuilt NavMesh.");
    }

    [MenuItem("Tools/TPS/Configure Flooded Grounds Stages")]
    public static void ConfigureStages()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

        RemoveSceneEnemies(scene);
        ConfigureMapWidePatrol(scene);
        StageStatusUI statusUI = EnsureStageStatusUI(scene);
        EnsureEnemySightIndicatorUI(scene);
        EnsureStageManager(scene, statusUI);
        BuildSceneNavMesh(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("Configured FloodedGrounds_TPS stages with random enemy spawning and remaining enemy UI.");
    }

    private static void RemoveSampleController(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Transform fpsController = FindDeepChild(root.transform, "FpsController");
            if (fpsController != null)
            {
                Object.DestroyImmediate(fpsController.gameObject);
            }
        }
    }

    private static void RemoveDuplicateGameplay(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects().ToArray())
        {
            if (root.GetComponentInChildren<PlayerHealth>(true) != null ||
                root.GetComponentInChildren<EnemyAI>(true) != null ||
                root.GetComponentInChildren<PlayerHUD>(true) != null ||
                root.GetComponentInChildren<EventSystem>(true) != null ||
                root.GetComponentInChildren<ThirdPersonCamera>(true) != null)
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void RemoveSceneEnemies(Scene scene)
    {
        var enemyObjects = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene)
            .Select(x => x.gameObject)
            .Distinct()
            .ToArray();

        foreach (GameObject enemyObject in enemyObjects)
        {
            Object.DestroyImmediate(enemyObject);
        }
    }

    private static void MoveGameplayObjects(Scene sourceScene, Scene targetScene)
    {
        var rootsToMove = new HashSet<GameObject>();

        foreach (GameObject root in sourceScene.GetRootGameObjects())
        {
            AddRootIfContains<PlayerHealth>(root, rootsToMove);
            AddRootIfContains<ThirdPersonCamera>(root, rootsToMove);
            AddRootIfContains<PlayerHUD>(root, rootsToMove);
            AddRootIfContains<EventSystem>(root, rootsToMove);
            AddRootIfContains<EnemyAI>(root, rootsToMove);
            AddRootIfContains<PatrolArea>(root, rootsToMove);
            AddRootIfContains<CoverPoint>(root, rootsToMove);
        }

        foreach (GameObject root in rootsToMove)
        {
            EditorSceneManager.MoveGameObjectToScene(root, targetScene);
        }
    }

    private static void AddRootIfContains<T>(GameObject root, HashSet<GameObject> rootsToMove) where T : Component
    {
        if (root.GetComponentInChildren<T>(true) != null)
        {
            rootsToMove.Add(root);
        }
    }

    private static void PositionGameplay(Scene scene)
    {
        var playerHealth = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        GameObject player = playerHealth != null ? playerHealth.gameObject : GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            PlaceOnGround(player.transform, new Vector3(456.03f, 0f, 153.83f), 0f);
            player.tag = "Player";
            player.name = "Player";
        }

        var camera = Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (camera != null && player != null)
        {
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0f, 2.1f, -1f);
            camera.transform.localRotation = Quaternion.identity;
            camera.name = "PlayerCameraRoot";
        }

        var enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene)
            .OrderBy(x => x.enemyType)
            .ToArray();

        Vector3[] enemyPositions =
        {
            new Vector3(469.5f, 0f, 170.5f),
            new Vector3(442.0f, 0f, 173.0f),
            new Vector3(482.0f, 0f, 142.0f),
        };

        for (int i = 0; i < enemies.Length; i++)
        {
            EnemyAI enemy = enemies[i];
            Vector3 pos = enemyPositions[Mathf.Min(i, enemyPositions.Length - 1)];
            PlaceOnGround(enemy.transform, pos, 180f);
            enemy.name = enemy.enemyType == EnemyType.Ranged ? $"RangedEnemy_{i + 1}" : $"MeleeEnemy_{i + 1}";

            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.Warp(enemy.transform.position);
            }
        }

        RepositionPatrolAreas(scene);
        RepositionCoverPoints(scene);
        RewireHud(scene, playerHealth);
    }

    private static void RepositionPatrolAreas(Scene scene)
    {
        Vector3[] positions =
        {
            new Vector3(456f, 0f, 161f),
            new Vector3(474f, 0f, 164f),
            new Vector3(440f, 0f, 170f),
            new Vector3(486f, 0f, 146f),
        };

        var patrolAreas = Object.FindObjectsByType<PatrolArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene)
            .Take(positions.Length)
            .ToArray();

        for (int i = 0; i < patrolAreas.Length; i++)
        {
            PlaceOnGround(patrolAreas[i].transform, positions[i], 0f);
            var collider = patrolAreas[i].GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
                collider.size = new Vector3(18f, 4f, 18f);
                collider.center = new Vector3(0f, 2f, 0f);
            }
        }
    }

    private static void RepositionCoverPoints(Scene scene)
    {
        Vector3[] positions =
        {
            new Vector3(462f, 0f, 160f),
            new Vector3(450f, 0f, 168f),
            new Vector3(474f, 0f, 152f),
        };

        var covers = Object.FindObjectsByType<CoverPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene)
            .Take(positions.Length)
            .ToArray();

        for (int i = 0; i < covers.Length; i++)
        {
            PlaceOnGround(covers[i].transform, positions[i], 0f);
        }
    }

    private static void RewireHud(Scene scene, PlayerHealth playerHealth)
    {
        PlayerHUD hud = Object.FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        ThirdPersonShooter shooter = Object.FindObjectsByType<ThirdPersonShooter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);

        if (hud == null)
        {
            return;
        }

        var serializedHud = new SerializedObject(hud);
        serializedHud.FindProperty("playerHealth").objectReferenceValue = playerHealth;
        serializedHud.FindProperty("shooter").objectReferenceValue = shooter;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void ConfigureEnemyPerception(Scene scene)
    {
        foreach (EnemyAI enemy in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(x => x.gameObject.scene == scene))
        {
            enemy.loseSightTime = 9999f;
            enemy.repathInterval = 0.15f;

            EnemyPerception perception = enemy.GetComponent<EnemyPerception>();
            if (perception == null)
            {
                continue;
            }

            perception.viewDistance = 1200f;
            perception.rangedDetectDistance = 1200f;
            perception.viewAngleTotal = 360f;
            perception.useLineOfSight = true;

            if (enemy.player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    enemy.player = playerObject.transform;
                    perception.player = playerObject.transform;
                }
            }
        }
    }

    private static void ConfigureMapWidePatrol(Scene scene)
    {
        var patrolAreas = Object.FindObjectsByType<PatrolArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene)
            .ToArray();

        if (patrolAreas.Length == 0)
        {
            GameObject patrolObject = new GameObject("MapWidePatrolArea");
            SceneManager.MoveGameObjectToScene(patrolObject, scene);
            patrolObject.AddComponent<BoxCollider>();
            patrolAreas = new[] { patrolObject.AddComponent<PatrolArea>() };
        }

        Vector3 center = new Vector3(550f, 25f, 500f);
        Vector3 size = new Vector3(900f, 80f, 900f);

        for (int i = 0; i < patrolAreas.Length; i++)
        {
            PatrolArea patrol = patrolAreas[i];
            patrol.name = i == 0 ? "MapWidePatrolArea" : $"MapWidePatrolArea_{i + 1}";
            patrol.transform.SetPositionAndRotation(center, Quaternion.identity);

            BoxCollider box = patrol.GetComponent<BoxCollider>();
            if (box != null)
            {
                box.isTrigger = true;
                box.center = Vector3.zero;
                box.size = size;
            }
        }

        var enemies = Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene)
            .ToArray();

        foreach (EnemyAI enemy in enemies)
        {
            enemy.patrolAreas = patrolAreas;
        }
    }

    private static StageStatusUI EnsureStageStatusUI(Scene scene)
    {
        StageStatusUI existing = Object.FindObjectsByType<StageStatusUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing != null)
        {
            return existing;
        }

        Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject root = new GameObject("StageStatusUI", typeof(RectTransform), typeof(StageStatusUI));
        root.transform.SetParent(canvas.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-32f, -32f);
        rootRect.sizeDelta = new Vector2(260f, 92f);

        TextMeshProUGUI stageText = CreateStatusText("StageText", root.transform, new Vector2(0f, -4f), 30f, TextAlignmentOptions.TopRight);
        TextMeshProUGUI remainingText = CreateStatusText("RemainingEnemiesText", root.transform, new Vector2(0f, -42f), 24f, TextAlignmentOptions.TopRight);

        StageStatusUI statusUI = root.GetComponent<StageStatusUI>();
        SerializedObject serializedUi = new SerializedObject(statusUI);
        serializedUi.FindProperty("stageText").objectReferenceValue = stageText;
        serializedUi.FindProperty("remainingEnemiesText").objectReferenceValue = remainingText;
        serializedUi.ApplyModifiedPropertiesWithoutUndo();

        statusUI.SetStageStatus(1, 0, false);
        return statusUI;
    }

    private static TextMeshProUGUI CreateStatusText(string name, Transform parent, Vector2 anchoredPosition, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = new Vector2(0f, 34f);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void EnsureEnemySightIndicatorUI(Scene scene)
    {
        EnemySightIndicatorUI existing = Object.FindObjectsByType<EnemySightIndicatorUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing != null)
        {
            return;
        }

        Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (canvas == null)
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        GameObject root = new GameObject("EnemySightIndicatorUI", typeof(RectTransform), typeof(EnemySightIndicatorUI));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        SerializedObject serializedIndicator = new SerializedObject(root.GetComponent<EnemySightIndicatorUI>());
        serializedIndicator.FindProperty("targetCamera").objectReferenceValue = Camera.main;
        serializedIndicator.FindProperty("indicatorRoot").objectReferenceValue = rect;
        serializedIndicator.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureStageManager(Scene scene, StageStatusUI statusUI)
    {
        foreach (StageManager existing in Object.FindObjectsByType<StageManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(x => x.gameObject.scene == scene)
                     .ToArray())
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject managerObject = new GameObject("StageManager");
        SceneManager.MoveGameObjectToScene(managerObject, scene);
        StageManager stageManager = managerObject.AddComponent<StageManager>();

        EnemyAI meleePrefab = AssetDatabase.LoadAssetAtPath<EnemyAI>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/MeleeEnemy.prefab");
        EnemyAI rangedPrefab = AssetDatabase.LoadAssetAtPath<EnemyAI>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/RangedEnemy.prefab");
        Transform player = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene)?.transform;
        PatrolArea[] patrolAreas = Object.FindObjectsByType<PatrolArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene)
            .ToArray();

        SerializedObject serializedManager = new SerializedObject(stageManager);
        SerializedProperty enemyPrefabs = serializedManager.FindProperty("enemyPrefabs");
        enemyPrefabs.arraySize = 2;
        enemyPrefabs.GetArrayElementAtIndex(0).objectReferenceValue = meleePrefab;
        enemyPrefabs.GetArrayElementAtIndex(1).objectReferenceValue = rangedPrefab;
        serializedManager.FindProperty("player").objectReferenceValue = player;
        serializedManager.FindProperty("statusUI").objectReferenceValue = statusUI;

        SerializedProperty patrolAreaProperty = serializedManager.FindProperty("patrolAreas");
        patrolAreaProperty.arraySize = patrolAreas.Length;
        for (int i = 0; i < patrolAreas.Length; i++)
        {
            patrolAreaProperty.GetArrayElementAtIndex(i).objectReferenceValue = patrolAreas[i];
        }

        serializedManager.FindProperty("firstStageEnemyCount").intValue = 3;
        serializedManager.FindProperty("enemiesAddedPerStage").intValue = 2;
        serializedManager.FindProperty("nextStageDelay").floatValue = 3f;
        serializedManager.FindProperty("spawnBoundsCenter").vector3Value = new Vector3(550f, 25f, 500f);
        serializedManager.FindProperty("spawnBoundsSize").vector3Value = new Vector3(600f, 80f, 600f);
        serializedManager.FindProperty("spawnAreaEdgeInset").floatValue = 0.22f;
        serializedManager.FindProperty("spawnCenterBias").floatValue = 0.65f;
        serializedManager.FindProperty("minDistanceFromPlayer").floatValue = 35f;
        serializedManager.FindProperty("navMeshSampleRadius").floatValue = 8f;
        serializedManager.FindProperty("spawnAttemptsPerEnemy").intValue = 100;
        serializedManager.FindProperty("mapWideDetectDistance").floatValue = 1200f;
        serializedManager.FindProperty("rangedVisionDistance").floatValue = 2400f;
        serializedManager.FindProperty("rangedHorizontalVisionAngle").floatValue = 30f;
        serializedManager.FindProperty("rangedVerticalVisionUp").floatValue = 70f;
        serializedManager.FindProperty("rangedVerticalVisionDown").floatValue = 70f;
        serializedManager.FindProperty("ignoreLineOfSight").boolValue = false;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildSceneNavMesh(Scene scene)
    {
        NavMeshSurface surface = Object.FindObjectsByType<NavMeshSurface>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);

        if (surface == null)
        {
            GameObject surfaceObject = new GameObject("NavMesh Surface");
            SceneManager.MoveGameObjectToScene(surfaceObject, scene);
            surface = surfaceObject.AddComponent<NavMeshSurface>();
        }

        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
        surface.defaultArea = 0;
        surface.BuildNavMesh();
    }

    private static void PlaceOnGround(Transform transform, Vector3 xzPosition, float yaw)
    {
        Vector3 position = xzPosition;
        position.y = SampleGroundHeight(xzPosition) + 0.05f;
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
    }

    private static float SampleGroundHeight(Vector3 xzPosition)
    {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            return terrain.SampleHeight(xzPosition) + terrain.transform.position.y;
        }

        return xzPosition.y;
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform result = FindDeepChild(child, name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}
