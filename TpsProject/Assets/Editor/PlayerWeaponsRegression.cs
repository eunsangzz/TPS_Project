using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class PlayerWeaponsRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static ThirdPersonShooter shooter;
    private static PlayerLoadout loadout;
    private static PlayerMelee melee;
    private static WeaponRuntime runtime;
    private static WeaponData data;
    private static Camera camera;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Requires an isolated batch project.");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Time.timeScale = 1f;
            var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            camera = new GameObject("Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(4f, 3f, -5f);
            camera.transform.LookAt(new Vector3(0f, 1f, 1f));
            camera.backgroundColor = new Color(0.055f, 0.065f, 0.08f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            Light light = new GameObject("Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

            GameObject player = new GameObject("Player", typeof(ThirdPersonInput), typeof(PlayerHealth), typeof(ThirdPersonShooter));
            ThirdPersonInput input = player.GetComponent<ThirdPersonInput>();
            Invoke(input, "Awake");
            GameObject gun = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gun.name = "AssaultRifle";
            gun.transform.SetParent(player.transform, false);
            gun.transform.localPosition = new Vector3(0.4f, 1.2f, 0.5f);
            gun.transform.localScale = new Vector3(0.1f, 0.15f, 0.7f);
            UnityEngine.Object.DestroyImmediate(gun.GetComponent<Collider>());
            data = ScriptableObject.CreateInstance<WeaponData>();
            data.magazineSize = 30;
            data.autoReloadWhenEmpty = false;
            shooter = player.GetComponent<ThirdPersonShooter>();
            Set(shooter, "weaponData", data);
            Set(shooter, "shooterCamera", camera);
            Invoke(shooter, "Awake");
            loadout = player.GetComponent<PlayerLoadout>();
            melee = player.GetComponent<PlayerMelee>();
            runtime = (WeaponRuntime)Get(shooter, "runtime");
            Expect(loadout != null && melee != null && loadout.SelectedSlot == 2, "Automatic loadout setup failed.");
            Expect(shooter.AmmoInMag == 30 && shooter.ReserveAmmo == 60 && !shooter.InfiniteReserveAmmo, "Starting ammo is not 90 total.");
            int shots = 0;
            while (runtime.CanFire(999f))
            {
                runtime.ConsumeAmmo();
                shots++;
                if (runtime.AmmoInMag == 0 && runtime.CanReload(data))
                {
                    runtime.StartReload();
                    runtime.FinishReload(data);
                }
                Expect(shots <= 90, "Infinite ammo remains enabled.");
            }
            Expect(shots == 90 && !runtime.CanReload(data), "Ammo exhaustion failed.");
            shooter.ResetAmmoForStage();
            runtime.ConsumeAmmo();
            runtime.StartReload();
            loadout.TrySelectSlot(1);
            Expect(!runtime.IsReloading && shooter.AmmoInMag == 29 && shooter.ReserveAmmo == 60, "Weapon switch did not cancel reload cleanly.");
            Expect(!gun.GetComponent<Renderer>().enabled, "Rifle remains visible in melee mode.");
            Expect(!loadout.TrySelectSlot(3) && !loadout.TrySelectSlot(4) && loadout.SelectedSlot == 1, "Empty slots changed weapons.");
            Invoke(shooter, "TryShoot");
            Expect(shooter.AmmoInMag == 29, "Gun fires while melee is selected.");
            shooter.ResetAmmoForStage();
            Expect(shooter.AmmoInMag + shooter.ReserveAmmo == 90 && loadout.SelectedSlot == 1, "Stage reset changes weapon or adds extra ammo.");
            runtime.ResetAmmo(data, 12);
            Expect(runtime.AmmoInMag == 12 && runtime.ReserveAmmo == 0, "Small ammo budget creates extra rounds.");
            shooter.ResetAmmoForStage();

            var actions = (InputAction[])Get(input, "weaponSlotActions");
            Expect(actions.Length == 4, "Four weapon actions were not created.");
            for (int i = 0; i < 4; i++)
                Expect(actions[i].bindings[0].path == $"<Keyboard>/{i + 1}" && actions[i].bindings[1].path == $"<Keyboard>/numpad{i + 1}", "Wrong numeric key binding.");

            EnemyHealth front = Enemy("Front", new Vector3(0f, 1f, 1.8f));
            GameObject duplicate = new GameObject("Extra collider", typeof(BoxCollider));
            duplicate.transform.SetParent(front.transform, false);
            EnemyHealth behind = Enemy("Behind", new Vector3(0f, 1f, -1.8f));
            EnemyHealth far = Enemy("Far", new Vector3(0f, 1f, 5f));
            Set(melee, "view", null);
            Physics.SyncTransforms();
            Expect(melee.TryAttack(), "Melee did not start.");
            Expect(!melee.TryAttack(), "Melee cooldown ignored.");
            Set(melee, "attackStartTime", Time.time - PlayerMeleeAnimation.ImpactTime - 0.01f);
            Invoke(melee, "Update");
            Invoke(melee, "Update");
            Expect(front.currentHealth == 60f, "Melee missed or hit multiple colliders twice.");
            Expect(behind.currentHealth == 100f && far.currentHealth == 100f, "Melee hits behind or out of range.");
            Expect(shooter.AmmoInMag + shooter.ReserveAmmo == 90, "Melee consumes ammo.");

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1f, 0.8f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            Physics.SyncTransforms();
            Invoke(melee, "ApplyHit");
            Expect(front.currentHealth == 60f, "Melee hits through a wall.");
            UnityEngine.Object.DestroyImmediate(wall);
            Set(melee, "nextAttackTime", -1f);
            melee.TryAttack();
            loadout.TrySelectSlot(2);
            Set(melee, "attackStartTime", Time.time - PlayerMeleeAnimation.ImpactTime - 0.01f);
            Invoke(melee, "Update");
            Expect(front.currentHealth == 60f && gun.GetComponent<Renderer>().enabled, "Switch did not cancel pending melee or restore rifle.");
            Time.timeScale = 0f;
            Expect(!loadout.TrySelectSlot(1) && !melee.TryAttack(), "Paused player can act.");
            Invoke(shooter, "TryShoot");
            Expect(shooter.AmmoInMag == 30, "Paused player can fire.");
            Time.timeScale = 1f;

            Capture(1280, 720, "weapons-rifle-wide.png");
            loadout.TrySelectSlot(1);
            Invoke(melee, "LateUpdate");
            Capture(390, 844, "weapons-melee-tall.png");
            Capture(1920, 1080, "weapons-melee-wide.png");
            Set(player.GetComponent<PlayerHealth>(), "<IsDead>k__BackingField", true);
            Expect(!loadout.TrySelectSlot(2) && !melee.TryAttack(), "Dead player can attack or switch.");
            Invoke(player.GetComponent<PlayerWeaponBarUI>(), "LateUpdate");
            Expect(!player.transform.Find("WeaponBarCanvas").GetComponent<Canvas>().enabled, "Dead player HUD stays visible.");
            Debug.Log("[PlayerWeaponsRegression] PASS: finite 90 rounds, refill, reload cancellation, switching, numeric bindings, melee arc/range/walls/deduplication/cooldown, pause/death, and three rendered viewports.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static EnemyHealth Enemy(string name, Vector3 position)
    {
        GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        enemy.name = name;
        enemy.transform.position = position;
        return enemy.AddComponent<EnemyHealth>();
    }

    private static void Capture(int width, int height, string name)
    {
        PlayerWeaponBarUI ui = loadout.GetComponent<PlayerWeaponBarUI>();
        Invoke(ui, "LateUpdate");
        Canvas canvas = loadout.transform.Find("WeaponBarCanvas").GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 0.5f;
        RenderTexture target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        foreach (PlayerWeaponIcon icon in canvas.GetComponentsInChildren<PlayerWeaponIcon>())
        {
            Mesh iconMesh = icon.canvasRenderer.GetMesh();
            Expect(iconMesh != null && iconMesh.vertexCount > 0, "Weapon icon geometry is missing.");
        }
        foreach (Graphic graphic in canvas.GetComponentsInChildren<Graphic>())
        {
            Expect(!graphic.raycastTarget, "Weapon HUD intercepts input.");
            Vector3[] corners = new Vector3[4];
            graphic.rectTransform.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector3 pixel = camera.WorldToScreenPoint(corner);
                Expect(pixel.x >= -1f && pixel.x <= width + 1f && pixel.y >= -1f && pixel.y <= height + 1f, $"HUD clipping at {width}x{height}: {graphic.name} {pixel}");
            }
        }
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
        image.Apply();
        int accentPixels = 0;
        foreach (Color32 pixel in image.GetPixels32())
            if ((pixel.g > 170 && pixel.b > 160 && pixel.r < 120) || (pixel.r > 200 && pixel.g > 120 && pixel.b < 100)) accentPixels++;
        Expect(accentPixels > 50, "Selected weapon indicator did not render.");
        File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../../", name)), image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
    }

    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Expect(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
