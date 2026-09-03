using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

public static class EnemySpawnRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Requires an isolated batch project.");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            UnityEngine.Random.InitState(8173);
            NavMesh.RemoveAllNavMeshData();
            var sources = new List<NavMeshBuildSource>
            {
                Ground(Vector3.zero, new Vector3(80f, 1f, 80f)),
                Ground(new Vector3(100f, 0f, 0f), new Vector3(20f, 1f, 20f))
            };
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), sources,
                new Bounds(new Vector3(30f, 0f, 0f), new Vector3(180f, 20f, 100f)), Vector3.zero, Quaternion.identity);
            Expect(data != null, "Test NavMesh build failed.");
            NavMeshDataInstance instance = NavMesh.AddNavMeshData(data);
            StageManager manager = new GameObject("Spawner", typeof(StageManager)).GetComponent<StageManager>();
            Transform player = new GameObject("Player").transform;
            Set(manager, "player", player);
            Set(manager, "minDistanceFromPlayer", 12f);
            Set(manager, "spawnAttemptsPerEnemy", 100);
            Set(manager, "spawnBoundsCenter", new Vector3(-30f, 0f, -30f));
            Set(manager, "spawnBoundsSize", new Vector3(2f, 50f, 2f));
            Invoke(manager, "RebuildSpawnMesh");

            GameObject meleePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/MeleeEnemy.prefab");
            GameObject rangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/RangedEnemy.prefab");
            Expect(meleePrefab != null && rangedPrefab != null, "Enemy prefab fixtures are missing.");
            Set(manager, "enemyPrefabObjects", new[] { meleePrefab, rangedPrefab });
            Set(manager, "currentStage", 1);
            for (int i = 0; i < 32; i++)
                Expect(PickPrefab(manager).name.Contains("Melee"), "Stage 1 selected a ranged enemy.");
            Set(manager, "currentStage", 2);
            bool selectedRanged = false;
            for (int i = 0; i < 32; i++)
                selectedRanged |= PickPrefab(manager).name.Contains("Ranged");
            Expect(selectedRanged, "Ranged enemies never become available after stage 1.");

            EnemyAI[] surrounders = new EnemyAI[3];
            Vector3[] surroundDestinations = new Vector3[3];
            for (int i = 0; i < surrounders.Length; i++)
            {
                surrounders[i] = new GameObject($"Surrounder {i}").AddComponent<EnemyAI>();
                surrounders[i].enemyType = EnemyType.Melee;
                surrounders[i].player = player;
                Invoke(surrounders[i], "OnEnable");
            }
            for (int i = 0; i < surrounders.Length; i++)
                surroundDestinations[i] = (Vector3)typeof(EnemyAI).GetMethod("GetMeleeSurroundDestination", Private).Invoke(surrounders[i], null);
            for (int i = 0; i < surroundDestinations.Length; i++)
                for (int j = i + 1; j < surroundDestinations.Length; j++)
                    Expect(Vector3.Distance(surroundDestinations[i], surroundDestinations[j]) > 0.4f,
                        "Melee enemies were assigned overlapping surround destinations.");
            foreach (EnemyAI surrounder in surrounders) UnityEngine.Object.DestroyImmediate(surrounder.gameObject);

            int[] quadrants = new int[4];
            int corners = 0;
            for (int i = 0; i < 1000; i++)
            {
                Expect(TryPosition(manager, out Vector3 point), "Valid map failed to spawn.");
                Expect(point.x < 40f, "Spawned on disconnected island.");
                Expect(new Vector2(point.x, point.z).magnitude >= 12f, "Player exclusion was violated.");
                Expect(NavMesh.SamplePosition(point, out _, 0.05f, NavMesh.AllAreas), "Spawn is outside NavMesh.");
                quadrants[(point.x >= 0f ? 1 : 0) + (point.z >= 0f ? 2 : 0)]++;
                if (Mathf.Abs(point.x) > 27f && Mathf.Abs(point.z) > 27f) corners++;
            }
            foreach (int count in quadrants) Expect(count > 190 && count < 310, "Spawn distribution is biased.");
            Expect(corners > 50, "Map corners are excluded.");
            Debug.Log($"[EnemySpawnRegression] quadrants={string.Join(",", quadrants)}, corners={corners}");

            var alive = (List<EnemyHealth>)typeof(StageManager).GetField("aliveEnemies", Private).GetValue(manager);
            var occupied = new HashSet<int>();
            for (int i = 0; i < 12; i++)
            {
                Expect(TryPosition(manager, out Vector3 point), "Spread selection failed.");
                foreach (EnemyHealth other in alive)
                    Expect(Vector3.Distance(point, other.transform.position) > 8f, "Wave spawns are clustered.");
                EnemyHealth enemy = new GameObject("Spawn marker", typeof(EnemyHealth)).GetComponent<EnemyHealth>();
                enemy.transform.position = point;
                alive.Add(enemy);
                occupied.Add((point.x >= 0f ? 1 : 0) + (point.z >= 0f ? 2 : 0));
            }
            Expect(occupied.Count == 4, "Wave does not cover all quadrants.");
            Set(manager, "minDistanceFromPlayer", 1000f);
            Expect(!TryPosition(manager, out _), "Invalid forward fallback still exists.");
            Set(manager, "minDistanceFromPlayer", 0f);
            Set(manager, "useEntireNavMesh", false);
            Set(manager, "spawnBoundsCenter", Vector3.zero);
            Set(manager, "spawnBoundsSize", new Vector3(40f, 50f, 40f));
            Expect(TryPosition(manager, out Vector3 limited), "Manual bounds mode failed.");
            Expect(Mathf.Abs(limited.x) <= 11.21f && Mathf.Abs(limited.z) <= 11.21f, "Manual bounds were ignored.");
            instance.Remove();
            Invoke(manager, "RebuildSpawnMesh");
            Expect(!TryPosition(manager, out _), "Empty NavMesh should not spawn off-mesh.");
            foreach (EnemyHealth enemy in alive) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
            alive.Clear();

            NavMeshData sceneData = AssetDatabase.LoadAssetAtPath<NavMeshData>("Assets/SpawnTestNavMesh.asset");
            if (sceneData != null)
            {
                NavMesh.AddNavMeshData(sceneData);
                Set(manager, "useEntireNavMesh", true);
                Set(manager, "minDistanceFromPlayer", 12f);
                Expect(NavMesh.SamplePosition(new Vector3(3.38f, 1.96f, -12.46f), out NavMeshHit anchor, 100f, NavMesh.AllAreas), "Scene NavMesh missing.");
                player.position = anchor.position;
                Invoke(manager, "RebuildSpawnMesh");
                Bounds sampled = new Bounds();
                for (int i = 0; i < 300; i++)
                {
                    Expect(TryPosition(manager, out Vector3 point), "Actual scene NavMesh failed to spawn.");
                    if (i == 0) sampled = new Bounds(point, Vector3.zero);
                    else sampled.Encapsulate(point);
                }
                Debug.Log($"[EnemySpawnRegression] Actual scene sample bounds: center={sampled.center}, size={sampled.size}");
            }
            Debug.Log("[EnemySpawnRegression] PASS: distinct melee surround destinations, stage-1 melee-only roster, later ranged availability, map coverage, spacing, player distance, reachability, manual bounds, and no unsafe fallback.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static NavMeshBuildSource Ground(Vector3 center, Vector3 size) => new NavMeshBuildSource
    {
        shape = NavMeshBuildSourceShape.Box,
        transform = Matrix4x4.TRS(center - Vector3.up * 0.5f, Quaternion.identity, Vector3.one),
        size = size,
        area = 0
    };

    private static bool TryPosition(StageManager manager, out Vector3 position)
    {
        object[] args = { Vector3.zero };
        bool found = (bool)typeof(StageManager).GetMethod("TryFindSpawnPosition", Private).Invoke(manager, args);
        position = (Vector3)args[0];
        return found;
    }

    private static GameObject PickPrefab(StageManager manager) =>
        (GameObject)typeof(StageManager).GetMethod("PickEnemyPrefabObject", Private).Invoke(manager, new object[] { manager.CurrentStage == 1 || UnityEngine.Random.value < 0.5f ? EnemyType.Melee : EnemyType.Ranged });

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
