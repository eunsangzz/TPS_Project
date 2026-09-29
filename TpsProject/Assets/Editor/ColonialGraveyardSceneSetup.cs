using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ColonialGraveyardSceneSetup
{
    private const string TargetScene = "Assets/Colonial City LittlePack – Church Graveyard Environment/Scenes/Colonial_Graveyard.unity";
    private const string SourceGameplayScene = "Assets/Scenes/MainScene.unity";
    private const string MeleeEnemyPrefabPath = "Assets/SciFiWarriorPBRHPPolyart/Prefabs/MeleeEnemy.prefab";
    private const string RangedEnemyPrefabPath = "Assets/SciFiWarriorPBRHPPolyart/Prefabs/RangedEnemy.prefab";

    [MenuItem("Tools/TPS/Configure Colonial Graveyard Scene")]
    public static void ConfigureColonialGraveyard()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);

        RemoveExistingGameplay(scene);

        Scene gameplayScene = EditorSceneManager.OpenScene(SourceGameplayScene, OpenSceneMode.Additive);
        MoveGameplayObjects(gameplayScene, scene);
        EditorSceneManager.CloseScene(gameplayScene, true);

        Bounds mapBounds = CalculateMapBounds(scene);
        EnsureMapWidePatrol(scene, mapBounds);
        BuildSceneNavMesh(scene);

        PositionGameplay(scene, mapBounds);
        StageStatusUI statusUI = EnsureStageStatusUI(scene);
        EnsureEnemySightIndicatorUI(scene);
        EnsureStageManager(scene, statusUI, mapBounds);
        RemoveSceneEnemies(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        VerifyColonialGraveyard();
        Debug.Log($"Configured Colonial Graveyard scene with player, stage spawning, UI, and NavMesh: {TargetScene}");
    }

    public static void VerifyColonialGraveyard()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        int players = CountInScene<PlayerHealth>(scene);
        int cameras = CountInScene<ThirdPersonCamera>(scene);
        int huds = CountInScene<PlayerHUD>(scene);
        int eventSystems = CountInScene<EventSystem>(scene);
        int enemies = CountInScene<EnemyAI>(scene);
        int navMeshSurfaces = CountInScene<NavMeshSurface>(scene);
        int stageManagers = CountInScene<StageManager>(scene);
        int stageStatusUis = CountInScene<StageStatusUI>(scene);
        int sightIndicatorUis = CountInScene<EnemySightIndicatorUI>(scene);
        Bounds bounds = CalculateMapBounds(scene);

        Debug.Log($"Colonial_Graveyard verify: players={players}, cameras={cameras}, huds={huds}, eventSystems={eventSystems}, enemies={enemies}, navMeshSurfaces={navMeshSurfaces}, stageManagers={stageManagers}, stageStatusUis={stageStatusUis}, sightIndicatorUis={sightIndicatorUis}, boundsCenter={bounds.center}, boundsSize={bounds.size}");
    }

    [MenuItem("Tools/TPS/Relink Colonial Graveyard Camera")]
    public static void RelinkColonialGraveyardCamera()
    {
        Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
        PlayerHealth playerHealth = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        GameObject player = playerHealth != null ? playerHealth.gameObject : GameObject.FindGameObjectWithTag("Player");
        ThirdPersonCamera cameraRig = Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);

        if (player == null)
        {
            throw new System.InvalidOperationException("Could not find PlayerHealth or a Player-tagged object in Colonial_Graveyard.");
        }

        if (cameraRig == null)
        {
            throw new System.InvalidOperationException("Could not find ThirdPersonCamera in Colonial_Graveyard.");
        }

        player.name = "Player";
        player.tag = "Player";

        ThirdPersonInput input = player.GetComponent<ThirdPersonInput>();
        Camera childCamera = cameraRig.GetComponentInChildren<Camera>(true);
        if (childCamera != null)
        {
            childCamera.tag = "MainCamera";
        }

        SerializedObject serializedCamera = new SerializedObject(cameraRig);
        serializedCamera.FindProperty("target").objectReferenceValue = player.transform;
        serializedCamera.FindProperty("inputSource").objectReferenceValue = input;
        serializedCamera.ApplyModifiedPropertiesWithoutUndo();

        if (cameraRig.GetComponent<ThirdPersonCameraReferenceBinder>() == null)
        {
            cameraRig.gameObject.AddComponent<ThirdPersonCameraReferenceBinder>();
        }

        foreach (ThirdPersonMotor motor in Object.FindObjectsByType<ThirdPersonMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(x => x.gameObject.scene == scene))
        {
            SerializedObject serializedMotor = new SerializedObject(motor);
            serializedMotor.FindProperty("cameraRoot").objectReferenceValue = cameraRig.transform;
            serializedMotor.FindProperty("input").objectReferenceValue = input;
            serializedMotor.FindProperty("cameraController").objectReferenceValue = cameraRig;
            serializedMotor.ApplyModifiedPropertiesWithoutUndo();
        }

        foreach (ThirdPersonShooter shooter in Object.FindObjectsByType<ThirdPersonShooter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(x => x.gameObject.scene == scene))
        {
            SerializedObject serializedShooter = new SerializedObject(shooter);
            serializedShooter.FindProperty("input").objectReferenceValue = input;
            serializedShooter.FindProperty("shooterCamera").objectReferenceValue = childCamera;
            serializedShooter.FindProperty("playerHealth").objectReferenceValue = playerHealth;
            serializedShooter.ApplyModifiedPropertiesWithoutUndo();
        }

        RewireHud(scene, playerHealth);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        Debug.Log($"Relinked Colonial_Graveyard camera: camera={cameraRig.name}, target={player.name}, childCamera={(childCamera != null ? childCamera.name : "null")}, input={(input != null ? input.name : "null")}");
    }

    private static void RemoveExistingGameplay(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects().ToArray())
        {
            if (root.GetComponentInChildren<PlayerHealth>(true) != null ||
                root.GetComponentInChildren<EnemyAI>(true) != null ||
                root.GetComponentInChildren<PlayerHUD>(true) != null ||
                root.GetComponentInChildren<EventSystem>(true) != null ||
                root.GetComponentInChildren<ThirdPersonCamera>(true) != null ||
                root.GetComponentInChildren<StageManager>(true) != null ||
                root.GetComponentInChildren<StageStatusUI>(true) != null ||
                root.GetComponentInChildren<EnemySightIndicatorUI>(true) != null ||
                root.name == "MapWidePatrolArea" ||
                root.name.StartsWith("MapWidePatrolArea_"))
            {
                Object.DestroyImmediate(root);
            }
        }
    }

    private static void RemoveSceneEnemies(Scene scene)
    {
        foreach (GameObject enemyObject in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(x => x.gameObject.scene == scene)
                     .Select(x => x.gameObject)
                     .Distinct()
                     .ToArray())
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

    private static Bounds CalculateMapBounds(Scene scene)
    {
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (IsGameplayRoot(root)) continue;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponentInParent<Canvas>() != null) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                Bounds terrainBounds = new Bounds(
                    terrain.transform.position + terrain.terrainData.size * 0.5f,
                    terrain.terrainData.size);
                if (!hasBounds)
                {
                    bounds = terrainBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(terrainBounds);
                }
            }
        }

        if (!hasBounds)
        {
            bounds = new Bounds(Vector3.zero, new Vector3(80f, 30f, 80f));
        }

        bounds.Expand(new Vector3(-bounds.size.x * 0.08f, 0f, -bounds.size.z * 0.08f));
        bounds.size = new Vector3(Mathf.Max(40f, bounds.size.x), Mathf.Max(30f, bounds.size.y), Mathf.Max(40f, bounds.size.z));
        return bounds;
    }

    private static bool IsGameplayRoot(GameObject root)
    {
        return root.GetComponentInChildren<PlayerHealth>(true) != null ||
               root.GetComponentInChildren<PlayerHUD>(true) != null ||
               root.GetComponentInChildren<EventSystem>(true) != null ||
               root.GetComponentInChildren<ThirdPersonCamera>(true) != null ||
               root.GetComponentInChildren<StageManager>(true) != null ||
               root.GetComponentInChildren<StageStatusUI>(true) != null ||
               root.GetComponentInChildren<EnemySightIndicatorUI>(true) != null ||
               root.GetComponentInChildren<PatrolArea>(true) != null ||
               root.GetComponentInChildren<CoverPoint>(true) != null ||
               root.GetComponentInChildren<NavMeshSurface>(true) != null;
    }

    private static void EnsureMapWidePatrol(Scene scene, Bounds mapBounds)
    {
        PatrolArea[] patrolAreas = Object.FindObjectsByType<PatrolArea>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(x => x.gameObject.scene == scene)
            .ToArray();

        if (patrolAreas.Length == 0)
        {
            GameObject patrolObject = new GameObject("MapWidePatrolArea", typeof(BoxCollider), typeof(PatrolArea));
            SceneManager.MoveGameObjectToScene(patrolObject, scene);
            patrolAreas = new[] { patrolObject.GetComponent<PatrolArea>() };
        }

        Vector3 patrolSize = new Vector3(mapBounds.size.x, Mathf.Max(20f, mapBounds.size.y + 20f), mapBounds.size.z);
        Vector3 patrolCenter = new Vector3(mapBounds.center.x, mapBounds.center.y, mapBounds.center.z);

        for (int i = 0; i < patrolAreas.Length; i++)
        {
            PatrolArea patrol = patrolAreas[i];
            patrol.name = i == 0 ? "MapWidePatrolArea" : $"MapWidePatrolArea_{i + 1}";
            patrol.transform.SetPositionAndRotation(patrolCenter, Quaternion.identity);

            BoxCollider box = patrol.GetComponent<BoxCollider>();
            if (box == null) box = patrol.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = patrolSize;
        }
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

    private static void PositionGameplay(Scene scene, Bounds mapBounds)
    {
        PlayerHealth playerHealth = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        GameObject player = playerHealth != null ? playerHealth.gameObject : GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            Vector3 playerPosition = FindNavMeshPosition(mapBounds.center, mapBounds.extents.magnitude);
            player.transform.SetPositionAndRotation(playerPosition, Quaternion.Euler(0f, 180f, 0f));
            player.name = "Player";
            player.tag = "Player";
        }

        ThirdPersonCamera cameraRig = Object.FindObjectsByType<ThirdPersonCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (cameraRig != null && player != null)
        {
            cameraRig.transform.SetPositionAndRotation(player.transform.position + new Vector3(0f, 2.1f, 0f), Quaternion.Euler(10f, 180f, 0f));
            cameraRig.name = "PlayerCameraRoot";

            SerializedObject serializedCamera = new SerializedObject(cameraRig);
            serializedCamera.FindProperty("target").objectReferenceValue = player.transform;
            serializedCamera.FindProperty("inputSource").objectReferenceValue = player.GetComponent<ThirdPersonInput>();
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();

            Camera childCamera = cameraRig.GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                childCamera.tag = "MainCamera";
            }
        }

        RewireHud(scene, playerHealth);
    }

    private static Vector3 FindNavMeshPosition(Vector3 desired, float maxDistance)
    {
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, Mathf.Max(8f, maxDistance), NavMesh.AllAreas))
        {
            return hit.position + Vector3.up * 0.05f;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            desired.y = terrain.SampleHeight(desired) + terrain.transform.position.y + 0.05f;
        }

        return desired;
    }

    private static void RewireHud(Scene scene, PlayerHealth playerHealth)
    {
        PlayerHUD hud = Object.FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        ThirdPersonShooter shooter = Object.FindObjectsByType<ThirdPersonShooter>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);

        if (hud == null) return;

        SerializedObject serializedHud = new SerializedObject(hud);
        serializedHud.FindProperty("playerHealth").objectReferenceValue = playerHealth;
        serializedHud.FindProperty("shooter").objectReferenceValue = shooter;
        serializedHud.ApplyModifiedPropertiesWithoutUndo();
    }

    private static StageStatusUI EnsureStageStatusUI(Scene scene)
    {
        StageStatusUI existing = Object.FindObjectsByType<StageStatusUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing != null) return existing;

        Canvas canvas = EnsureCanvas(scene);

        GameObject root = new GameObject("StageStatusUI", typeof(RectTransform), typeof(StageStatusUI));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = new Vector2(-32f, -32f);
        rootRect.sizeDelta = new Vector2(260f, 92f);

        TextMeshProUGUI stageText = CreateStatusText("StageText", root.transform, new Vector2(0f, -4f), 30f);
        TextMeshProUGUI remainingText = CreateStatusText("RemainingEnemiesText", root.transform, new Vector2(0f, -42f), 24f);

        StageStatusUI statusUI = root.GetComponent<StageStatusUI>();
        SerializedObject serializedUi = new SerializedObject(statusUI);
        serializedUi.FindProperty("stageText").objectReferenceValue = stageText;
        serializedUi.FindProperty("remainingEnemiesText").objectReferenceValue = remainingText;
        serializedUi.ApplyModifiedPropertiesWithoutUndo();
        statusUI.SetStageStatus(1, 0, false);
        return statusUI;
    }

    private static Canvas EnsureCanvas(Scene scene)
    {
        Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (canvas != null) return canvas;

        GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObject.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
        return canvas;
    }

    private static TextMeshProUGUI CreateStatusText(string name, Transform parent, Vector2 anchoredPosition, float fontSize)
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
        text.alignment = TextAlignmentOptions.TopRight;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static void EnsureEnemySightIndicatorUI(Scene scene)
    {
        EnemySightIndicatorUI existing = Object.FindObjectsByType<EnemySightIndicatorUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (existing != null) return;

        Canvas canvas = EnsureCanvas(scene);
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

    private static void EnsureStageManager(Scene scene, StageStatusUI statusUI, Bounds mapBounds)
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

        EnemyAI meleePrefab = AssetDatabase.LoadAssetAtPath<EnemyAI>(MeleeEnemyPrefabPath);
        EnemyAI rangedPrefab = AssetDatabase.LoadAssetAtPath<EnemyAI>(RangedEnemyPrefabPath);
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

        float largestHorizontalExtent = Mathf.Max(mapBounds.extents.x, mapBounds.extents.z);
        serializedManager.FindProperty("firstStageEnemyCount").intValue = 3;
        serializedManager.FindProperty("enemiesAddedPerStage").intValue = 2;
        serializedManager.FindProperty("nextStageDelay").floatValue = 3f;
        serializedManager.FindProperty("spawnBoundsCenter").vector3Value = mapBounds.center;
        serializedManager.FindProperty("spawnBoundsSize").vector3Value = new Vector3(mapBounds.size.x, Mathf.Max(30f, mapBounds.size.y + 20f), mapBounds.size.z);
        serializedManager.FindProperty("spawnAreaEdgeInset").floatValue = 0.22f;
        serializedManager.FindProperty("spawnCenterBias").floatValue = 0.65f;
        serializedManager.FindProperty("minDistanceFromPlayer").floatValue = Mathf.Clamp(largestHorizontalExtent * 0.2f, 12f, 35f);
        serializedManager.FindProperty("navMeshSampleRadius").floatValue = Mathf.Clamp(largestHorizontalExtent * 0.08f, 5f, 18f);
        serializedManager.FindProperty("spawnAttemptsPerEnemy").intValue = 100;
        serializedManager.FindProperty("mapWideDetectDistance").floatValue = Mathf.Max(1200f, largestHorizontalExtent * 4f);
        serializedManager.FindProperty("meleeHorizontalVisionAngle").floatValue = 240f;
        serializedManager.FindProperty("rangedVisionDistance").floatValue = Mathf.Max(2400f, largestHorizontalExtent * 8f);
        serializedManager.FindProperty("rangedHorizontalVisionAngle").floatValue = 30f;
        serializedManager.FindProperty("rangedVerticalVisionUp").floatValue = 70f;
        serializedManager.FindProperty("rangedVerticalVisionDown").floatValue = 70f;
        serializedManager.FindProperty("ignoreLineOfSight").boolValue = false;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int CountInScene<T>(Scene scene) where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Count(x => x is Component component && component.gameObject.scene == scene);
    }
}
