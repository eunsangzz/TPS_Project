using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class SkillRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated test project.");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Time.timeScale = 1f;
            Camera camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 1f, -3f);
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline = QualitySettings.renderPipeline = pipeline;
            GameObject player = CreatePlayer(camera);
            ThirdPersonShooter shooter = player.GetComponent<ThirdPersonShooter>();
            PlayerSkills skills = player.GetComponent<PlayerSkills>();
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            WeaponRuntime runtime = (WeaponRuntime)Get(shooter, "runtime");
            Expect(skills != null, "Automatic skill setup failed.");
            for (int roll = 0; roll < 100; roll++)
            {
                PlayerSkill[] offers = skills.RollOffers();
                Expect(offers.Length == 3 && offers.Distinct().Count() == 3 && offers.All(skills.CanAcquire), "Invalid initial offers.");
            }
            for (int i = 0; i < 3; i++) Expect(skills.Acquire(PlayerSkill.PowerRounds), "Damage skill acquisition failed.");
            Expect(!skills.Acquire(PlayerSkill.PowerRounds) && Mathf.Approximately(skills.GunDamageMultiplier, 1.6f), "Stack cap or additive damage failed.");
            Expect(!skills.Acquire((PlayerSkill)(-1)) && !skills.Acquire((PlayerSkill)99), "Invalid skill was accepted.");
            EnemyHealth first = Target(new Vector3(0f, 1f, 4f));
            EnemyHealth second = Target(new Vector3(0f, 1f, 8f));
            EnemyHealth third = Target(new Vector3(0f, 1f, 12f));
            GameObject duplicate = new GameObject("Duplicate Collider", typeof(BoxCollider));
            duplicate.transform.SetParent(first.transform, false);
            GameObject self = new GameObject("Player Collider", typeof(BoxCollider));
            self.transform.SetParent(player.transform, false);
            self.transform.position = new Vector3(0f, 1f, 2f);
            Ray ray = new Ray(new Vector3(0f, 1f, 0f), Vector3.forward);
            Physics.SyncTransforms();
            Invoke(shooter, "TraceShot", ray);
            Expect(first.currentHealth == 136f && second.currentHealth == 200f, "Baseline rifle damage or self filtering failed.");
            first.currentHealth = 200f;
            skills.Acquire(PlayerSkill.PiercingRounds);
            Invoke(shooter, "TraceShot", ray);
            Expect(first.currentHealth == 136f && second.currentHealth == 168f && third.currentHealth == 200f, "Piercing attenuation, limit or collider deduplication failed.");
            first.currentHealth = second.currentHealth = 200f;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 6f);
            Physics.SyncTransforms();
            Vector3 endpoint = (Vector3)Invoke(shooter, "TraceShot", ray);
            Expect(first.currentHealth == 136f && second.currentHealth == 200f && endpoint.z < 6f, "Piercing crossed a wall.");
            foreach (GameObject item in new[] { first.gameObject, second.gameObject, third.gameObject, wall, self }) UnityEngine.Object.DestroyImmediate(item);

            for (int i = 0; i < 3; i++) { skills.Acquire(PlayerSkill.HeavyStrike); skills.Acquire(PlayerSkill.WideSwing); skills.Acquire(PlayerSkill.AmmoRecovery); }
            PlayerMelee melee = player.GetComponent<PlayerMelee>();
            Set(melee, "attackDirection", Vector3.forward);
            float angle = 68f * Mathf.Deg2Rad;
            EnemyHealth wide = Target(new Vector3(Mathf.Sin(angle) * 3.2f, 1f, Mathf.Cos(angle) * 3.2f));
            EnemyHealth behind = Target(new Vector3(0f, 1f, -2f));
            Physics.SyncTransforms();
            Invoke(melee, "ApplyHit");
            Expect(wide.currentHealth == 130f && behind.currentHealth == 200f, "Melee damage/range/angle upgrade failed.");
            for (int i = 0; i < 20; i++) runtime.ConsumeAmmo();
            skills.OnMeleeKill();
            Expect(shooter.AmmoInMag + shooter.ReserveAmmo == 79, "Melee recovery did not add 9 rounds.");
            shooter.RecoverAmmo(999);
            Expect(shooter.AmmoInMag + shooter.ReserveAmmo == 90, "Ammo recovery exceeded stage capacity.");
            health.TakeDamage(30f);
            for (int i = 0; i < 3; i++) skills.Acquire(PlayerSkill.Toughness);
            Expect(health.MaxHealth == 160f && health.CurrentHealth == 130f, "Health upgrade did not heal/update maximum.");
            for (int roll = 0; roll < 20; roll++)
            {
                var offers = skills.RollOffers();
                Expect(offers.Length == 3 && offers.Distinct().Count() == 3 && offers.All(s => (int)s >= 6), "Maxed pool does not offer three repeatable rewards.");
            }
            skills.Acquire(PlayerSkill.Supply);
            shooter.ResetAmmoForStage();
            Expect(shooter.AmmoInMag + shooter.ReserveAmmo == 105, "Supply bonus was not applied to next stage.");
            shooter.ResetAmmoForStage();
            Expect(shooter.AmmoInMag + shooter.ReserveAmmo == 90 && skills.GunDamageMultiplier > 1f, "Stage reset lost upgrades or kept temporary supply.");

            SkillSelectionUI ui = new GameObject("Skill Selection", typeof(RectTransform), typeof(SkillSelectionUI)).GetComponent<SkillSelectionUI>();
            int callbacks = 0;
            UnityEngine.Random.InitState(45);
            Expect(ui.Show(skills, 16, () => callbacks++), "UI did not open.");
            Expect(Time.timeScale == 0f && Cursor.visible && Cursor.lockState == CursorLockMode.None, "UI did not pause/release cursor.");
            Capture(ui, camera, 1280, 720, "skills-maxed.png");
            Capture(ui, camera, 390, 844, "skills-maxed-tall.png");
            Set(ui, "openedAt", Time.unscaledTime - 1f);
            Expect(ui.TryChoose(1) && !ui.TryChoose(2) && callbacks == 1 && Time.timeScale == 1f, "Selection repeated or failed to resume.");
            UnityEngine.Object.DestroyImmediate(player);
            player = CreatePlayer(camera);
            skills = player.GetComponent<PlayerSkills>();
            Expect(skills.GunDamageMultiplier == 1f && skills.Level(PlayerSkill.Toughness) == 0, "New run kept previous upgrades.");
            UnityEngine.Random.InitState(7);
            ui.Show(skills, 1, () => callbacks++);
            Capture(ui, camera, 1280, 720, "skills-wide.png");
            Capture(ui, camera, 390, 844, "skills-tall.png");
            Capture(ui, camera, 1920, 1080, "skills-large.png");
            Capture(ui, camera, 1024, 768, "skills-standard.png");
            ui.Cancel();
            Expect(callbacks == 1 && Time.timeScale == 1f, "Cancel advanced the stage or left time paused.");
            Time.timeScale = 0.5f;
            ui.Show(skills, 2, () => callbacks++);
            ui.Cancel();
            Expect(Time.timeScale == 0.5f, "Previous time scale was not restored.");
            Time.timeScale = 1f;
            ui.Show(skills, 2, () => callbacks++);
            player.GetComponent<PlayerHealth>().TakeDamage(999f);
            Expect(!ui.IsOpen && callbacks == 1 && Time.timeScale == 0f, "Death resumed gameplay or awarded a skill.");
            Time.timeScale = 1f;
            Debug.Log("[SkillRegression] PASS: six skills, caps, unique/fallback offers, piercing/dedup/walls, melee reach/damage, ammo cap, health, new-run reset, selection/cancel/death, and four UI screenshots.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); Time.timeScale = 1f; EditorApplication.Exit(1); }
    }

    private static GameObject CreatePlayer(Camera camera)
    {
        GameObject player = new GameObject("Player", typeof(PlayerHealth), typeof(ThirdPersonShooter));
        Invoke(player.GetComponent<PlayerHealth>(), "Awake");
        WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
        data.damage = 40f;
        ThirdPersonShooter shooter = player.GetComponent<ThirdPersonShooter>();
        Set(shooter, "weaponData", data);
        Set(shooter, "shooterCamera", camera);
        Invoke(shooter, "Awake");
        return player;
    }

    private static EnemyHealth Target(Vector3 position)
    {
        var target = new GameObject("Target", typeof(CapsuleCollider), typeof(EnemyHealth));
        target.transform.position = position;
        EnemyHealth health = target.GetComponent<EnemyHealth>();
        health.maxHealth = health.currentHealth = 200f;
        health.regenPerSecond = 0f;
        return health;
    }

    private static void Capture(SkillSelectionUI ui, Camera camera, int width, int height, string name)
    {
        Canvas canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 0.5f;
        RenderTexture target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        Invoke(ui, "Layout");
        Canvas.ForceUpdateCanvases();
        camera.Render();
        foreach (Text text in ui.GetComponentsInChildren<Text>())
        {
            Expect(text.preferredHeight <= text.rectTransform.rect.height + 1f, $"Text is clipped: {text.name}, {text.text}, needs {text.preferredHeight}, has {text.rectTransform.rect.height}, {width}x{height}");
            var corners = new Vector3[4];
            text.rectTransform.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 pixel = camera.WorldToScreenPoint(corner);
                Expect(pixel.x >= -1f && pixel.x <= width + 1f && pixel.y >= -1f && pixel.y <= height + 1f, "UI leaves the viewport.");
            }
        }
        foreach (SkillIcon icon in ui.GetComponentsInChildren<SkillIcon>())
            Expect(icon.canvasRenderer.GetMesh().vertexCount > 3, "Skill icon is blank.");
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        int foreground = image.GetPixels32().Count(p => p.r > 170 || p.g > 170 || p.b > 170);
        Expect(foreground > 1000, "Skill UI rendered blank.");
        File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../../", name)), image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
