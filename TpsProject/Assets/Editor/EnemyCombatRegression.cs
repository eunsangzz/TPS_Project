using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class EnemyCombatRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static GameObject fixture;
    private static EnemyCombat combat;
    private static EnemyHealth targetHealth;
    private static int passed;

    [MenuItem("Tools/Tests/Enemy Combat Regression")]
    public static void RunAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run these checks outside Play Mode.");

        Scene previousScene = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        UnityEngine.Random.State randomState = UnityEngine.Random.state;
        passed = 0;
        try
        {
            Check("Unobstructed shot damages the target", () =>
            {
                Fire();
                ExpectHealth(88f);
            });
            Check("Root fire point uses chest height", () =>
            {
                combat.firePoint = combat.transform;
                Box("Low obstacle", fixture.transform, new Vector3(0f, 0.25f, 2f), new Vector3(2f, 0.5f, 0.5f));
                Fire();
                ExpectHealth(88f);
            });
            Check("Explicit muzzle position is preserved", () =>
            {
                GameObject muzzle = new GameObject("Muzzle");
                muzzle.transform.SetParent(combat.transform, false);
                muzzle.transform.localPosition = new Vector3(0f, 1.8f, 0f);
                combat.firePoint = muzzle.transform;
                Box("Chest obstacle", fixture.transform, new Vector3(0f, 0.7f, 2f), new Vector3(2f, 1.4f, 0.5f));
                Fire();
                ExpectHealth(88f);
            });
            Check("Own child collider does not swallow the shot", () =>
            {
                Box("Own armor", combat.transform, new Vector3(0f, 1.2f, 1f), Vector3.one);
                Fire();
                ExpectHealth(88f);
            });
            Check("Wall still blocks the shot after own armor", () =>
            {
                Box("Own armor", combat.transform, new Vector3(0f, 1.2f, 1f), Vector3.one);
                Box("Wall", fixture.transform, new Vector3(0f, 1f, 5f), new Vector3(3f, 3f, 1f));
                Fire();
                ExpectHealth(100f);
            });
            Check("Range is respected", () =>
            {
                combat.rangedRange = 5f;
                Fire();
                ExpectHealth(100f);
            });
            Check("Windup fires once and observes cooldown", () =>
            {
                combat.MarkAttackUsed();
                combat.TryAttack();
                Expect(combat.IsAttackPending(), "Shot was not queued.");
                Expect(!combat.CanAttackNow(), "Cooldown was ignored.");
                Invoke(combat, "Update");
                ExpectHealth(100f);
                Set(combat, "pendingFireTime", Time.time - 1f);
                Physics.SyncTransforms();
                Invoke(combat, "Update");
                Invoke(combat, "Update");
                ExpectHealth(88f);
                Expect(!combat.IsAttackPending(), "Shot stayed queued after firing.");
            });
            Check("Taking damage does not cancel an in-range attack", () =>
            {
                EnemyAI ai = combat.gameObject.AddComponent<EnemyAI>();
                ai.enabled = false;
                ai.enemyType = EnemyType.Ranged;
                ai.player = combat.player;
                Invoke(ai, "Awake");
                FieldInfo state = typeof(EnemyAI).GetField("state", PrivateInstance);
                state.SetValue(ai, Enum.Parse(state.FieldType, "Attack"));
                combat.TryAttack();
                ai.health.ApplyDamage(1f);
                Expect(combat.IsAttackPending(), "Damage cancelled the pending shot.");
                Expect(state.GetValue(ai).ToString() == "Attack", "Damage forced a chase transition.");
            });
            Check("Dead shooter cannot finish a queued shot", () =>
            {
                combat.TryAttack();
                combat.GetComponent<EnemyHealth>().currentHealth = 0f;
                Set(combat, "pendingFireTime", Time.time - 1f);
                Invoke(combat, "Update");
                ExpectHealth(100f);
                Expect(!combat.IsAttackPending(), "Dead shooter kept a pending shot.");
            });
            Check("Disabling combat cancels the shot", () =>
            {
                combat.TryAttack();
                Invoke(combat, "OnDisable");
                Expect(!combat.IsAttackPending(), "Disabled shooter kept a pending shot.");
            });
            Check("Melee damage is unchanged", () =>
            {
                combat.enemyType = EnemyType.Melee;
                combat.player.position = combat.transform.position + Vector3.forward;
                combat.TryAttack();
                ExpectHealth(90f);
            });
            Debug.Log($"[EnemyCombatRegression] PASS: {passed} checks.");
        }
        finally
        {
            if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            UnityEngine.Random.state = randomState;
            EditorSceneManager.CloseScene(testScene, true);
            if (previousScene.IsValid()) SceneManager.SetActiveScene(previousScene);
        }
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("RunBatch is only for an isolated batch-mode project.");

        try
        {
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
                EditorSceneManager.SaveScene(scene, "Assets/EnemyCombatRegressionBootstrap.unity");
            RunAll();
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Check(string name, Action test)
    {
        fixture = new GameObject("EnemyCombatRegression");
        fixture.transform.position = new Vector3(10000f, 10000f, 10000f);
        try
        {
            GameObject shooter = new GameObject("Shooter");
            shooter.transform.SetParent(fixture.transform, false);
            shooter.AddComponent<EnemyHealth>();
            combat = shooter.AddComponent<EnemyCombat>();
            combat.self = shooter.transform;
            combat.enemyType = EnemyType.Ranged;
            combat.accuracy = 1f;
            combat.spreadAngle = 0f;
            combat.drawShotRay = false;
            Invoke(combat, "Awake");

            GameObject target = new GameObject("Target");
            target.transform.SetParent(fixture.transform, false);
            target.transform.localPosition = Vector3.forward * 10f;
            targetHealth = target.AddComponent<EnemyHealth>();
            CapsuleCollider collider = target.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up;
            collider.height = 2f;
            collider.radius = 0.5f;
            combat.player = target.transform;

            test();
            passed++;
            Debug.Log($"[EnemyCombatRegression] PASS: {name}");
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(name, exception);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    private static void Box(string name, Transform parent, Vector3 position, Vector3 size)
    {
        GameObject box = new GameObject(name);
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.AddComponent<BoxCollider>().size = size;
    }

    private static void Fire()
    {
        Physics.SyncTransforms();
        Invoke(combat, "FireRangedHitscan");
    }

    private static void ExpectHealth(float expected)
    {
        Expect(Mathf.Approximately(targetHealth.currentHealth, expected),
            $"Expected health {expected}, received {targetHealth.currentHealth}.");
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Invoke(object target, string name)
    {
        target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);
    }

    private static void Set(object target, string name, object value)
    {
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    }
}
