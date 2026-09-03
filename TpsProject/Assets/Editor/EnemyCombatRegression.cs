using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
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
            Check("Melee attack telegraphs before dealing damage", () =>
            {
                combat.enemyType = EnemyType.Melee;
                combat.player.position = combat.transform.position + Vector3.forward;
                combat.TryAttack();
                Expect(combat.IsMeleeTelegraphActive && combat.IsAttackPending(), "Melee warning did not begin.");
                ExpectHealth(100f);
                Set(combat, "pendingMeleeHitTime", Time.time - 1f);
                Invoke(combat, "Update");
                ExpectHealth(90f);
                Expect(!combat.IsAttackPending(), "Melee warning remained active after impact.");
            });
            Check("Melee windup can be dodged", () =>
            {
                combat.enemyType = EnemyType.Melee;
                combat.player.position = combat.transform.position + Vector3.forward;
                combat.TryAttack();
                combat.player.position = combat.transform.position + Vector3.forward * 4f;
                Set(combat, "pendingMeleeHitTime", Time.time - 1f);
                Invoke(combat, "Update");
                ExpectHealth(100f);
            });
            ValidateMeleeMovementAndDeathAnimation();
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

    private static void ValidateMeleeMovementAndDeathAnimation()
    {
        const string controllerPath = "Assets/SciFiWarriorPBRHPPolyart/Animators/SciFiWarrior.controller";
        const string prefabPath = "Assets/SciFiWarriorPBRHPPolyart/Prefabs/MeleeEnemy.prefab";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        Expect(controller != null, "Enemy animator controller is missing.");
        AnimatorStateMachine baseLayer = controller.layers[0].stateMachine;
        AnimatorState locomotion = baseLayer.states.Select(value => value.state).FirstOrDefault(value => value.name == "Locomotion");
        AnimatorState die = baseLayer.states.Select(value => value.state).FirstOrDefault(value => value.name == "Die");
        Expect(locomotion != null && locomotion.speedParameterActive && locomotion.speedParameter == "MovementSpeedMultiplier",
            "Locomotion is not driven by the melee animation speed multiplier.");
        Expect(die != null && die.motion != null && die.motion.name == "Die", "Die clip is not connected to the base layer.");
        Expect(baseLayer.anyStateTransitions.Any(transition => transition.destinationState == die &&
            transition.conditions.Any(condition => condition.parameter == "Die")), "Die trigger does not transition to the death state.");
        Vector3 firstSlot = EnemyAI.CalculateSurroundOffset(0, 0.85f, 1f);
        Vector3 secondSlot = EnemyAI.CalculateSurroundOffset(1, 0.85f, 1f);
        Vector3 outerSlot = EnemyAI.CalculateSurroundOffset(6, 0.85f, 1f);
        Expect(Vector3.Angle(firstSlot, secondSlot) > 45f && outerSlot.magnitude > firstSlot.magnitude + 0.8f,
            "Melee surround slots do not distribute enemies across angles and rings.");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        try
        {
            EnemyHealth health = enemy.AddComponent<EnemyHealth>();
            health.destroyDelay = 100f;
            EnemyAI ai = enemy.AddComponent<EnemyAI>();
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            // Edit-mode AddComponent does not run Awake, so mirror the runtime setup order.
            Invoke(health, "Awake");
            Invoke(ai, "Awake");
            Expect(Mathf.Approximately(ai.meleeMoveSpeed, 5.4f) && Mathf.Approximately(agent.speed, 5.4f),
                $"Melee movement speed is not 1.5x the previous 3.6 value (configured={ai.meleeMoveSpeed}, agent={agent.speed}).");
            Animator animator = enemy.GetComponent<Animator>();
            Transform rifle = enemy.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == "AssaultRifle");
            Transform blade = enemy.GetComponentsInChildren<Transform>(true).FirstOrDefault(value => value.name == EnemyAI.MeleeWeaponName);
            Expect(rifle != null && !rifle.gameObject.activeSelf, "Melee enemy still displays the assault rifle.");
            Expect(blade != null && blade.gameObject.activeSelf && blade.Find("Blade") != null,
                "Melee enemy did not receive its melee blade.");
            ai.enemyType = EnemyType.Ranged;
            Invoke(ai, "ApplyTypeState");
            Expect(rifle.gameObject.activeSelf && !blade.gameObject.activeSelf,
                "Ranged presentation did not restore the rifle and hide the melee blade.");
            ai.enemyType = EnemyType.Melee;
            Invoke(ai, "ApplyTypeState");
            animator.Rebind();
            animator.Update(0f);
            Invoke(ai, "TriggerAttackAnimation");
            animator.Update(0.05f);
            int meleeLayer = animator.GetLayerIndex("PlayerMelee");
            Expect(meleeLayer >= 0 && animator.GetCurrentAnimatorStateInfo(meleeLayer).IsName("Attack"),
                "Melee attack did not enter the dedicated upper-body attack animation.");
            health.ApplyDamage(999f);
            animator.Update(0.1f);
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            Expect(current.IsName("Base Layer.Die") || next.IsName("Base Layer.Die"),
                "Enemy death did not enter the Die animation state.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(enemy);
        }
        passed++;
        Debug.Log("[EnemyCombatRegression] PASS: dodgeable melee telegraph, surround slots, blade/rifle separation, melee attack, speed, locomotion multiplier, and Die animation.");
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
