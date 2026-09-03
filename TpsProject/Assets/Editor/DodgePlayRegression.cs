using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

[InitializeOnLoad]
public static class DodgePlayRegression
{
    private const string ActiveKey = "DodgePlayRegression.Active";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static IEnumerator flow;
    private static double deadline;
    private static int lastFrame = -1;
    private static int passed;
    private static readonly List<string> runtimeErrors = new List<string>();

    static DodgePlayRegression()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(ActiveKey, false)) return;
            deadline = EditorApplication.timeSinceStartup + 60f;
            runtimeErrors.Clear();
            passed = 0;
            Application.logMessageReceived += TrackRuntimeError;
            flow = Checks();
            EditorApplication.update += Tick;
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Requires an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/DodgePlayBootstrap.unity");
        SessionState.SetBool(ActiveKey, true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Dodge gameplay test timed out.");
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            if (flow.MoveNext()) return;
            if (runtimeErrors.Count > 0) throw new InvalidOperationException(string.Join("\n", runtimeErrors));
            Debug.Log($"[DodgePlayRegression] PASS: {passed} checks; player input, motion, recovery, combat locks, invulnerability, cooldown, pause, death, enemy melee/aim evasion, and safe paths.");
            Finish(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish(1);
        }
    }

    private static void Finish(int code)
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= TrackRuntimeError;
        SessionState.SetBool(ActiveKey, false);
        Time.timeScale = 1f;
        EditorApplication.Exit(code);
    }

    private static IEnumerator Checks()
    {
        Time.timeScale = 1f;
        Time.maximumDeltaTime = 0.05f;
        Time.captureDeltaTime = 1f / 60f;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>("DodgeTestKeyboard");
        Mouse mouse = InputSystem.AddDevice<Mouse>("DodgeTestMouse");

        Camera camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 1.4f, -4f);
        camera.transform.rotation = Quaternion.identity;
        RenderSettings.ambientLight = Color.white;
        GameObject lightObject = new GameObject("PreviewLight", typeof(Light));
        lightObject.GetComponent<Light>().type = LightType.Directional;
        lightObject.GetComponent<Light>().intensity = 2f;
        lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "DodgeTestGround";
        ground.transform.position = new Vector3(0f, -0.5f, 2f);
        ground.transform.localScale = new Vector3(30f, 1f, 30f);
        Physics.SyncTransforms();

        NavMeshData navData = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0), new List<NavMeshBuildSource>
        {
            new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.TRS(new Vector3(0f, -0.5f, 2f), Quaternion.identity, Vector3.one),
                size = new Vector3(30f, 1f, 30f),
                area = 0
            }
        }, new Bounds(new Vector3(0f, 0f, 2f), new Vector3(40f, 10f, 40f)), Vector3.zero, Quaternion.identity);
        NavMeshDataInstance nav = NavMesh.AddNavMeshData(navData);

        GameObject player = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/HPCharacter.prefab"));
        player.name = "Player";
        player.SetActive(false);
        player.tag = "Player";
        CharacterController controller = player.AddComponent<CharacterController>();
        controller.center = Vector3.up;
        controller.height = 2f;
        ThirdPersonInput input = player.AddComponent<ThirdPersonInput>();
        PlayerHealth health = player.AddComponent<PlayerHealth>();
        ThirdPersonMotor motor = player.AddComponent<ThirdPersonMotor>();
        Set(motor, "cameraRoot", camera.transform);
        ThirdPersonShooter shooter = player.AddComponent<ThirdPersonShooter>();
        WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
        weapon.magazineSize = 30;
        weapon.reloadTime = 2f;
        Set(shooter, "weaponData", weapon);
        Set(shooter, "shooterCamera", camera);
        player.SetActive(true);
        yield return null;
        yield return null;

        PlayerDodge dodge = player.GetComponent<PlayerDodge>();
        DodgeRollAnimation rollAnimation = player.GetComponent<DodgeRollAnimation>();
        Expect(dodge != null && rollAnimation != null && rollAnimation.Available, "Player dodge animation was not initialized on the humanoid avatar.");
        IEnumerator leanChecks = PlayerLeanPlayChecks.Run(player, keyboard);
        while (leanChecks.MoveNext()) yield return leanChecks.Current;
        Animator animator = player.GetComponent<Animator>();
        HumanPoseHandler poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        HumanPose before = new HumanPose();
        poseHandler.GetHumanPose(ref before);
        CapturePose(player, "roll-before");
        Vector3 start = player.transform.position;
        PlayerLoadout loadout = player.GetComponent<PlayerLoadout>();
        PlayerMelee melee = player.GetComponent<PlayerMelee>();
        Expect(melee.TryAttack(), "Initial melee attack could not start.");

        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space, Key.Q));
        InputSystem.Update();
        yield return null;
        Expect(input.DodgePressed && input.JumpPressed && dodge.IsDodging, "Space did not start the dodge roll.");
        Expect(player.GetComponent<PlayerLean>().CurrentLean == 0f, "Lean remained active during a roll.");
        Expect(!melee.IsAttacking && !melee.TryAttack() && !loadout.CanAct, "Roll did not cancel/block melee combat.");
        Expect(!loadout.TrySelectSlot(1), "Weapon switching remained enabled during the roll.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();

        float midTime = Time.time + 0.22f;
        while (Time.time < midTime) yield return null;
        HumanPose middle = new HumanPose();
        poseHandler.GetHumanPose(ref middle);
        CapturePose(player, "roll-middle");
        Expect(Vector3.Distance(start, player.transform.position) > 0.5f, "Player roll did not move forward.");
        Expect(Vector3.Angle(player.transform.up, Vector3.up) < 1f, "Player collider root tilted during the visual roll.");
        Expect(Quaternion.Angle(before.bodyRotation, middle.bodyRotation) > 25f, "Humanoid body did not rotate into the roll pose.");
        float healthBefore = health.CurrentHealth;
        health.TakeDamage(10f);
        Expect(Mathf.Approximately(health.CurrentHealth, healthBefore), "Timed dodge invulnerability did not block damage.");

        while (dodge.IsDodging) yield return null;
        yield return null;
        HumanPose recovered = new HumanPose();
        poseHandler.GetHumanPose(ref recovered);
        CapturePose(player, "roll-recovered");
        Debug.Log($"[DodgePose] before={before.bodyRotation.eulerAngles}, middle={middle.bodyRotation.eulerAngles}, recovered={recovered.bodyRotation.eulerAngles}, delta={Quaternion.Angle(before.bodyRotation, recovered.bodyRotation)}, root={player.transform.eulerAngles}");
        Expect(Vector3.Angle(Vector3.up, recovered.bodyRotation * Vector3.up) < 25f, "Player did not recover an upright pose after rolling.");
        Expect(Mathf.Abs(player.transform.position.y - start.y) < 0.2f, "Space still caused a jump or the player left the floor.");
        float rollDistance = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude;
        Expect(rollDistance > 4.3f && rollDistance < 5f, $"Extended roll distance was unexpected: {rollDistance:F2}m.");
        Debug.Log($"[DodgeDistance] {rollDistance:F2}m (previous nominal distance 4.01m)");
        health.TakeDamage(10f);
        Expect(Mathf.Approximately(health.CurrentHealth, healthBefore - 10f), "Damage stayed blocked after the dodge ended.");
        Expect(!dodge.TryDodge(), "Dodge cooldown was ignored.");

        loadout.UnlockWeapon(2);
        WeaponRuntime runtime = (WeaponRuntime)Get(shooter, "runtime");
        runtime.ConsumeAmmo();
        Invoke(shooter, "TryStartReload");
        Expect(shooter.IsReloading, "Reload test setup failed.");
        Set(dodge, "nextDodgeTime", 0f);
        camera.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
        InputSystem.Update();
        Invoke(input, "Update");
        Vector3 sideStart = player.transform.position;
        Expect(dodge.TryDodge(), "Second camera-relative roll could not start.");
        Expect(!shooter.IsReloading, "Roll did not interrupt reload.");
        int ammoBeforeRoll = shooter.AmmoInMag;
        Invoke(shooter, "TryShoot");
        Invoke(shooter, "TryStartReload");
        Expect(shooter.AmmoInMag == ammoBeforeRoll && !shooter.IsReloading, "Shooting/reloading was allowed while rolling.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        float pauseAt = Time.time + 0.15f;
        while (Time.time < pauseAt) yield return null;
        Vector3 pausedPosition = player.transform.position;
        Time.timeScale = 0f;
        yield return null;
        yield return null;
        Expect(dodge.IsDodging && Vector3.Distance(pausedPosition, player.transform.position) < 0.01f, "Pause did not freeze the roll.");
        Time.timeScale = 1f;
        while (dodge.IsDodging) yield return null;
        Expect(player.transform.position.x > sideStart.x + 1f && Mathf.Abs(player.transform.position.z - sideStart.z) < 0.3f,
            "Roll direction was not camera-relative.");
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Expect(Vector3.ProjectOnPlane(hips.position - player.transform.position, Vector3.up).magnitude < 0.75f,
            "Visual skeleton drifted away from its moving/rotated collider.");

        GameObject rollWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Vector3 wallRollStart = player.transform.position;
        rollWall.transform.position = wallRollStart + Vector3.right * 1.2f + Vector3.up;
        rollWall.transform.localScale = new Vector3(0.3f, 4f, 4f);
        Physics.SyncTransforms();
        Set(dodge, "nextDodgeTime", 0f);
        Expect(dodge.TryDodge(), "Wall-collision roll could not start.");
        while (dodge.IsDodging) yield return null;
        Expect(player.transform.position.x - wallRollStart.x < 0.9f, "Player rolled through a solid wall.");
        rollWall.SetActive(false);
        Vector3 groundedPosition = player.transform.position;
        controller.enabled = false;
        player.transform.position += Vector3.up * 3f;
        controller.enabled = true;
        controller.Move(Vector3.down * 0.01f);
        Set(dodge, "nextDodgeTime", 0f);
        Expect(!dodge.TryDodge(), "Player could start a roll in mid-air.");
        controller.enabled = false;
        player.transform.position = groundedPosition;
        controller.enabled = true;
        controller.Move(Vector3.down * 0.1f);

        IEnumerator scopeHearingChecks = ScopeHearingPlayChecks.Run(player, camera, mouse);
        while (scopeHearingChecks.MoveNext()) yield return scopeHearingChecks.Current;

        GameObject enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/MeleeEnemy.prefab"), player.transform.position + Vector3.forward * 6f, Quaternion.identity);
        enemy.SetActive(false);
        EnemyHealth enemyHealth = enemy.AddComponent<EnemyHealth>();
        EnemyAI enemyAI = enemy.AddComponent<EnemyAI>();
        enemyAI.enemyType = EnemyType.Melee;
        enemyAI.player = player.transform;
        enemyAI.health = enemyHealth;
        Set(enemyAI, "dodgeChance", 1f);
        enemyHealth.regenPerSecond = 0f;
        enemy.SetActive(true);
        camera.transform.LookAt(enemy.transform.position + Vector3.up);
        yield return null;

        Vector3 enemyStart = enemy.transform.position;
        float accelerationBefore = enemy.GetComponent<NavMeshAgent>().acceleration;

        InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
        InputSystem.Update();
        yield return null;
        Expect((bool)Get(enemyAI, "isDodging"), "Enemy did not roll when deliberately aimed at with guaranteed dodge chance.");
        DodgeRollAnimation enemyRoll = enemy.GetComponent<DodgeRollAnimation>();
        Expect(enemyRoll != null && enemyRoll.IsPlaying, "Enemy dodge did not use the shared roll motion.");
        while ((bool)Get(enemyAI, "isDodging")) yield return null;
        Expect(Vector3.Distance(enemyStart, enemy.transform.position) > 1f, "Enemy dodge did not actually move.");
        Expect(Mathf.Approximately(enemy.GetComponent<NavMeshAgent>().acceleration, accelerationBefore), "Enemy navigation acceleration was not restored.");
        Expect(!enemyRoll.IsPlaying, "Enemy roll animation did not finish.");
        Set(enemyAI, "evaluatedCurrentAim", false);
        camera.transform.LookAt(enemy.transform.position + Vector3.up * 1.2f);
        Expect(!(bool)Invoke(enemyAI, "TryStartDodgeFromPlayerThreat"), "Enemy ignored dodge cooldown.");
        Set(enemyAI, "nextDodgeTime", 0f);
        Set(enemyAI, "evaluatedCurrentAim", false);
        Set(enemyAI, "dodgeChance", 0f);
        Expect(!(bool)Invoke(enemyAI, "TryStartDodgeFromPlayerThreat"), "Zero dodge chance still triggered evasion.");
        Set(enemyAI, "dodgeChance", 1f);
        Expect(!(bool)Invoke(enemyAI, "TryStartDodgeFromPlayerThreat"), "Held aim rerolled the chance every frame.");

        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = Vector3.Lerp(camera.transform.position, enemy.transform.position + Vector3.up * 1.2f, 0.5f);
        wall.transform.localScale = new Vector3(3f, 4f, 0.5f);
        Physics.SyncTransforms();
        Expect(!(bool)Invoke(enemyAI, "IsPlayerAimingAtThisEnemy"), "Enemy detected aim through a wall.");
        wall.SetActive(false);
        Set(enemyAI, "dodgeDistance", 1000f);
        Expect(!(bool)Invoke(enemyAI, "StartDodge"), "Enemy accepted a dodge destination outside the navigation mesh.");
        Set(enemyAI, "dodgeDistance", 3f);

        InputSystem.QueueStateEvent(mouse, new MouseState());
        InputSystem.Update();
        Invoke(input, "Update");
        loadout.TrySelectSlot(1);
        enemy.GetComponent<NavMeshAgent>().Warp(player.transform.position + Vector3.forward * 2.5f);
        camera.transform.rotation = Quaternion.identity;
        Set(enemyAI, "nextDodgeTime", 0f);
        Expect(melee.TryAttack(), "Melee threat could not start.");
        Expect((bool)Invoke(enemyAI, "TryStartDodgeFromPlayerThreat"), "Enemy did not evade a close frontal melee attack.");
        enemyHealth.ApplyDamage(1000f);
        Expect(enemyHealth.IsDead && !enemyRoll.IsPlaying, "Enemy death did not interrupt roll animation.");
        enemy.SetActive(false);

        Set(dodge, "nextDodgeTime", 0f);
        Expect(dodge.TryDodge(), "Death-interruption roll could not start.");
        health.TakeDamage(1000f);
        Expect(health.IsDead && !dodge.IsDodging && !rollAnimation.IsPlaying, "Player death did not cancel the roll.");
        Expect(!dodge.TryDodge(), "Dead player could roll.");

        InputSystem.RemoveDevice(keyboard);
        InputSystem.RemoveDevice(mouse);
        poseHandler.Dispose();
        nav.Remove();
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static object Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void TrackRuntimeError(string message, string stack, LogType type)
    {
        if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
        // The isolated project's editor search-index startup issue is unrelated to gameplay.
        if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
        runtimeErrors.Add(message);
    }
    private static void CapturePose(GameObject subject, string filename)
    {
        GameObject previewObject = new GameObject("PosePreviewCamera", typeof(Camera));
        Camera preview = previewObject.GetComponent<Camera>();
        preview.transform.position = subject.transform.position + new Vector3(4f, 2f, -4f);
        preview.transform.LookAt(subject.transform.position + Vector3.up);
        preview.clearFlags = CameraClearFlags.SolidColor;
        preview.backgroundColor = new Color(0.12f, 0.15f, 0.2f);
        RenderTexture render = new RenderTexture(640, 640, 24);
        RenderTexture previous = RenderTexture.active;
        preview.targetTexture = render;
        preview.Render();
        RenderTexture.active = render;
        Texture2D texture = new Texture2D(640, 640, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 640, 640), 0, 0);
        texture.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), filename + ".png"), texture.EncodeToPNG());
        RenderTexture.active = previous;
        preview.targetTexture = null;
        UnityEngine.Object.Destroy(render);
        UnityEngine.Object.Destroy(texture);
        UnityEngine.Object.Destroy(previewObject);
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed++;
    }
}
