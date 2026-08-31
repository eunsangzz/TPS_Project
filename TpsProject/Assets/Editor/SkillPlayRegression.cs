using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

[InitializeOnLoad]
public static class SkillPlayRegression
{
    private const string ActiveKey = "SkillPlayRegression.Active";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator flow;
    private static int lastFrame = -1;
    private static double deadline;

    static SkillPlayRegression()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 50f;
            flow = Checks();
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated test project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/SkillPlayBootstrap.unity");
        SessionState.SetBool(ActiveKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Skill gameplay test timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (flow.MoveNext()) return;
            Debug.Log("[SkillPlayRegression] PASS: real stage clear, mouse card selection, pause/input blocking, one stage advancement, refill, actual melee-kill recovery, no recovery from rifle kills, and death cancellation.");
            Finish(0);
        }
        catch (Exception error) { Debug.LogException(error); Finish(1); }
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(ActiveKey, false);
        Time.timeScale = 1f;
        EditorApplication.Exit(code);
    }

    private static IEnumerator Checks()
    {
        Time.timeScale = 1f;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Mouse mouse = InputSystem.AddDevice<Mouse>("SkillTestMouse");
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>("SkillTestKeyboard");
        Camera camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 1f, -3f);
        GameObject player = new GameObject("Player");
        player.SetActive(false);
        player.tag = "Player";
        ThirdPersonInput input = player.AddComponent<ThirdPersonInput>();
        PlayerHealth health = player.AddComponent<PlayerHealth>();
        ThirdPersonShooter shooter = player.AddComponent<ThirdPersonShooter>();
        WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
        weapon.autoReloadWhenEmpty = false;
        Set(shooter, "weaponData", weapon);
        Set(shooter, "shooterCamera", camera);
        player.SetActive(true);
        yield return null;
        PlayerSkills skills = player.GetComponent<PlayerSkills>();
        PlayerLoadout loadout = player.GetComponent<PlayerLoadout>();
        WeaponRuntime runtime = (WeaponRuntime)Get(shooter, "runtime");
        NavMesh.RemoveAllNavMeshData();
        NavMeshData mesh = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(new Vector3(0f, -0.5f, 0f), Quaternion.identity, Vector3.one), size = new Vector3(100f, 1f, 100f), area = 0 }
        }, new Bounds(Vector3.zero, new Vector3(110f, 20f, 110f)), Vector3.zero, Quaternion.identity);
        NavMeshDataInstance nav = NavMesh.AddNavMeshData(mesh);
        GameObject template = new GameObject("Melee Test Prefab", typeof(CapsuleCollider), typeof(EnemyHealth));
        template.transform.position = new Vector3(200f, 0f, 200f);
        GameObject stageObject = new GameObject("Stage");
        stageObject.SetActive(false);
        StageManager stage = stageObject.AddComponent<StageManager>();
        Set(stage, "startOnAwake", false);
        Set(stage, "player", player.transform);
        Set(stage, "firstStageEnemyCount", 1);
        Set(stage, "enemiesAddedPerStage", 0);
        Set(stage, "nextStageDelay", 0.05f);
        Set(stage, "minDistanceFromPlayer", 15f);
        Set(stage, "enemyPrefabObjects", new[] { template });
        stageObject.SetActive(true);
        Invoke(stage, "BeginNextStage");
        var alive = (List<EnemyHealth>)Get(stage, "aliveEnemies");
        Expect(alive.Count == 1 && stage.CurrentStage == 1, "Stage fixture did not spawn on its NavMesh.");
        runtime.ConsumeAmmo();
        alive[0].TakeDamage(999f, Vector3.zero, Vector3.forward);
        while (!stage.IsChoosingSkill) yield return null;
        SkillSelectionUI ui = stage.GetComponentInChildren<SkillSelectionUI>();
        Expect(ui.Offers.Count == 3 && stage.CurrentStage == 1 && Time.timeScale == 0f, "Stage advanced before choosing a skill.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Digit1));
        InputSystem.Update();
        Invoke(input, "Update");
        Expect(input.Move == Vector2.zero && input.WeaponSlotPressed == 0 && !loadout.TrySelectSlot(1), "Gameplay input leaks through the menu.");
        float ready = Time.realtimeSinceStartup + 0.3f;
        while (Time.realtimeSinceStartup < ready) yield return null;
        Button choice = ui.GetComponentsInChildren<Button>()[0];
        PlayerSkill selected = ui.Offers[0];
        Vector2 position = RectTransformUtility.WorldToScreenPoint(null, ((RectTransform)choice.transform).TransformPoint(((RectTransform)choice.transform).rect.center));
        var raycasts = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, raycasts);
        Expect(raycasts.Count > 0 && raycasts[0].gameObject == choice.gameObject, "Skill card is not the top UI raycast target.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        InputSystem.Update();
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
        InputSystem.Update();
        yield return null;
        Expect(shooter.AmmoInMag == 29 && stage.CurrentStage == 1, "Mouse down fired or selected before release.");
        InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
        InputSystem.Update();
        yield return null;
        yield return null;
        Expect(!ui.IsOpen && stage.CurrentStage == 2 && skills.Level(selected) == 1, "Real mouse selection did not apply one skill/advance one stage.");
        Expect(shooter.AmmoInMag == 30 && shooter.ReserveAmmo == 60 && !input.FireHeld, "Selection leaked gunfire or failed to refill.");
        Expect(!ui.TryChoose(1) && stage.CurrentStage == 2, "Double selection granted an extra stage.");

        skills.Acquire(PlayerSkill.AmmoRecovery);
        for (int i = 0; i < 10; i++) runtime.ConsumeAmmo();
        loadout.TrySelectSlot(1);
        EnemyHealth meleeTarget = new GameObject("Melee target", typeof(CapsuleCollider), typeof(EnemyHealth)).GetComponent<EnemyHealth>();
        meleeTarget.transform.position = new Vector3(0f, 1f, 1.6f);
        meleeTarget.currentHealth = 1f;
        meleeTarget.regenPerSecond = 0f;
        GameObject extraCollider = new GameObject("Second collider", typeof(BoxCollider));
        extraCollider.transform.SetParent(meleeTarget.transform, false);
        Physics.SyncTransforms();
        Expect(loadout.Melee.TryAttack(), "Melee recovery attack did not start.");
        float until = Time.time + 0.45f;
        while (Time.time < until) yield return null;
        Expect(meleeTarget.IsDead && shooter.AmmoInMag + shooter.ReserveAmmo == 80 + skills.AmmoPerMeleeKill, "Real melee kill did not grant exactly one ammo reward.");
        int ammo = shooter.AmmoInMag + shooter.ReserveAmmo;
        EnemyHealth gunTarget = new GameObject("Gun target", typeof(CapsuleCollider), typeof(EnemyHealth)).GetComponent<EnemyHealth>();
        gunTarget.transform.position = new Vector3(0f, 1f, 4f);
        gunTarget.currentHealth = 1f;
        gunTarget.regenPerSecond = 0f;
        Physics.SyncTransforms();
        Invoke(shooter, "TraceShot", new Ray(new Vector3(0f, 1f, 0f), Vector3.forward));
        Expect(gunTarget.IsDead && shooter.AmmoInMag + shooter.ReserveAmmo == ammo, "Rifle kill incorrectly granted melee ammo.");
        alive[0].TakeDamage(999f, Vector3.zero, Vector3.forward);
        while (!stage.IsChoosingSkill) yield return null;
        health.TakeDamage(999f);
        yield return null;
        Expect(!ui.IsOpen && Time.timeScale == 0f && stage.CurrentStage == 2 && !ui.TryChoose(0), "Death during selection resumed the run.");
        nav.Remove();
        InputSystem.RemoveDevice(mouse);
        InputSystem.RemoveDevice(keyboard);
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
