using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class PlayerWeaponPlayRegression
{
    private const string ActiveKey = "PlayerWeaponPlayRegression.Active";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static IEnumerator flow;
    private static int lastFrame = -1;
    private static double deadline;

    static PlayerWeaponPlayRegression()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 25f;
            flow = RunChecks();
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Requires an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/WeaponPlayBootstrap.unity");
        SessionState.SetBool(ActiveKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Gameplay test timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (flow.MoveNext()) return;
            Debug.Log("[PlayerWeaponPlayRegression] PASS: real numeric input, full-magazine replacement reload, reload/switch timing, repeated stage refill, and delayed melee/cancellation.");
            Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void Finish(int exit)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.Exit(exit);
    }

    private static IEnumerator RunChecks()
    {
        Camera camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 1f, -3f);
        GameObject player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/HPCharacter.prefab"));
        player.name = "Player";
        player.SetActive(false);
        player.tag = "Player";
        ThirdPersonInput input = player.AddComponent<ThirdPersonInput>();
        player.AddComponent<PlayerHealth>();
        ThirdPersonShooter shooter = player.AddComponent<ThirdPersonShooter>();
        WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
        data.magazineSize = 30;
        data.reloadTime = 0.2f;
        Set(shooter, "weaponData", data);
        Set(shooter, "shooterCamera", camera);
        player.SetActive(true);
        yield return null;
        PlayerLoadout loadout = player.GetComponent<PlayerLoadout>();
        PlayerMelee melee = player.GetComponent<PlayerMelee>();
        PlayerMeleeAnimation animation = player.GetComponent<PlayerMeleeAnimation>();
        Expect(animation != null && animation.Available, "Actual avatar animation is unavailable in Play Mode.");
        WeaponRuntime runtime = (WeaponRuntime)typeof(ThirdPersonShooter).GetField("runtime", Private).GetValue(shooter);
        Expect(loadout.SelectedSlot == 1 && !loadout.IsSlotUnlocked(2) && !loadout.IsSlotUnlocked(3),
            "Play Mode did not start with melee-only progression.");
        Expect(shooter.AmmoInMag == 30 && shooter.ReserveAmmo == 60, "Play Mode initial ammo failed.");
        loadout.UnlockWeapon(2);
        loadout.UnlockWeapon(3);

        Keyboard keyboard = InputSystem.AddDevice<Keyboard>("WeaponTestKeyboard");
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Key[] keys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4 };
        for (int i = 0; i < keys.Length; i++)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys[i]));
            InputSystem.Update();
            Invoke(input, "Update");
            Invoke(loadout, "Update");
            int expectedSlot = i == 0 ? 1 : i == 1 ? 2 : 3;
            Expect(loadout.SelectedSlot == expectedSlot, $"Key {i + 1}: requested={input.WeaponSlotPressed}, selected={loadout.SelectedSlot}.");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            yield return null;
        }
        InputSystem.RemoveDevice(keyboard);
        input.enabled = false;
        loadout.TrySelectSlot(2);

        for (int i = 0; i < 5; i++) runtime.ConsumeAmmo();
        Invoke(shooter, "TryStartReload");
        Expect(shooter.IsReloading, "Coroutine reload did not start.");
        loadout.TrySelectSlot(1);
        float waitUntil = Time.time + 0.35f;
        while (Time.time < waitUntil) yield return null;
        Expect(!shooter.IsReloading && shooter.AmmoInMag == 25 && shooter.ReserveAmmo == 60, "Cancelled reload completed later.");

        loadout.TrySelectSlot(2);
        Invoke(shooter, "TryStartReload");
        waitUntil = Time.time + 0.35f;
        while (Time.time < waitUntil) yield return null;
        Expect(!shooter.IsReloading && shooter.AmmoInMag == 30 && shooter.ReserveAmmo == 30,
            "Completed reload topped up rounds instead of replacing the partial magazine.");
        runtime.ConsumeAmmo();
        Invoke(shooter, "TryStartReload");
        GameObject stageObject = new GameObject("StageManager");
        stageObject.SetActive(false);
        StageManager stage = stageObject.AddComponent<StageManager>();
        Set(stage, "startOnAwake", false);
        Set(stage, "firstStageEnemyCount", 1);
        Set(stage, "enemiesAddedPerStage", 0);
        Set(stage, "enemyPrefabObjects", new[] { new GameObject("TestEnemyPrefab") });
        stageObject.SetActive(true);
        Invoke(stage, "BeginNextStage");
        Expect(shooter.AmmoInMag == 30 && shooter.ReserveAmmo == 60 && !shooter.IsReloading, "Stage did not refill/cancel reload.");
        runtime.ConsumeAmmo();
        waitUntil = Time.time + 0.35f;
        while (Time.time < waitUntil) yield return null;
        Expect(shooter.AmmoInMag == 29 && shooter.ReserveAmmo == 60, "Previous-stage reload modified the new ammo.");
        Invoke(stage, "BeginNextStage");
        Expect(shooter.AmmoInMag == 30 && shooter.ReserveAmmo == 60, "Second stage did not reset ammo.");

        GameObject target = new GameObject("Target", typeof(CapsuleCollider), typeof(EnemyHealth));
        target.transform.position = new Vector3(0f, 1f, 1.8f);
        EnemyHealth enemy = target.GetComponent<EnemyHealth>();
        enemy.regenPerSecond = 0f;
        loadout.TrySelectSlot(1);
        Physics.SyncTransforms();
        Expect(melee.TryAttack(), "Real melee attack did not start.");
        loadout.TrySelectSlot(2);
        waitUntil = Time.time + 0.75f;
        while (Time.time < waitUntil) yield return null;
        Expect(enemy.currentHealth == 100f, "Cancelled melee hit after switching.");
        loadout.TrySelectSlot(1);
        Expect(melee.TryAttack(), "Melee did not recover after cooldown.");
        float attackTime = Time.time;
        waitUntil = attackTime + 0.15f;
        while (Time.time < waitUntil) yield return null;
        Expect(enemy.currentHealth == 100f, "Animation hits before its impact event.");
        waitUntil = attackTime + PlayerMeleeAnimation.ImpactTime + 0.12f;
        while (Time.time < waitUntil) yield return null;
        Expect(enemy.currentHealth == 60f && enemy.HasTakenDamage, "Delayed melee hit or health-bar trigger failed.");
        Expect(shooter.AmmoInMag == 30 && shooter.ReserveAmmo == 60, "Melee spent gun ammo in Play Mode.");
        waitUntil = attackTime + 0.8f;
        while (Time.time < waitUntil) yield return null;
        Expect(enemy.currentHealth == 60f, "Animation delivered duplicate damage.");
        Expect(melee.TryAttack(), "Second animated attack did not start.");
        Time.timeScale = 0f;
        float resumeAt = Time.realtimeSinceStartup + 0.2f;
        while (Time.realtimeSinceStartup < resumeAt) yield return null;
        Expect(enemy.currentHealth == 60f, "Paused animation delivered damage.");
        Time.timeScale = 1f;
        waitUntil = Time.time + 0.8f;
        while (Time.time < waitUntil) yield return null;
        Expect(enemy.currentHealth == 20f, "Paused attack did not resume at its impact event.");
        Expect(melee.TryAttack(), "Death cancellation attack did not start.");
        Set(player.GetComponent<PlayerHealth>(), "<IsDead>k__BackingField", true);
        waitUntil = Time.time + 0.8f;
        while (Time.time < waitUntil) yield return null;
        Expect(enemy.currentHealth == 20f, "Dead player delivered pending animation damage.");
    }

    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
