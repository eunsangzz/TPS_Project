using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ShotVisualRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    // Runs only in a disposable batch project, not in the user's open scene.
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Requires an isolated batch project.");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            Expect(pipeline != null, "URP asset is missing.");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;

            GameObject cameraObject = new GameObject("Test Camera", typeof(Camera), typeof(ThirdPersonCamera));
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 1f, -10f);
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            ThirdPersonCamera controller = cameraObject.GetComponent<ThirdPersonCamera>();

            GameObject player = new GameObject("Test Player");
            ThirdPersonShooter shooter = player.AddComponent<ThirdPersonShooter>();
            PlayerHealth health = player.AddComponent<PlayerHealth>();
            WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
            Set(shooter, "weaponData", weapon);
            Set(shooter, "shooterCamera", camera);
            Invoke(shooter, "Awake");

            PlayerCrosshairUI crosshair = player.GetComponent<PlayerCrosshairUI>();
            Canvas canvas = player.GetComponentInChildren<Canvas>();
            WeaponVFX playerVfx = player.GetComponent<WeaponVFX>();
            Expect(crosshair != null && canvas != null && canvas.enabled, "Hip-fire crosshair was not created.");
            Expect(playerVfx != null, "Player tracer was not connected automatically.");
            Expect(canvas.GetComponentsInChildren<UnityEngine.UI.Image>().Length == 5, "Crosshair marks are missing.");
            foreach (var mark in canvas.GetComponentsInChildren<UnityEngine.UI.Image>())
                Expect(!mark.raycastTarget, "Crosshair intercepts UI clicks.");

            SetAim(controller, "Shoulder");
            Invoke(crosshair, "LateUpdate");
            Expect(canvas.enabled, "Shoulder crosshair was hidden.");
            SetAim(controller, "Scope");
            Invoke(crosshair, "LateUpdate");
            Expect(!canvas.enabled, "Crosshair overlaps the scope.");
            SetAim(controller, "Hip");
            Time.timeScale = 0f;
            Invoke(crosshair, "LateUpdate");
            Expect(!canvas.enabled, "Crosshair remains visible while paused.");
            Time.timeScale = 1f;
            Set(health, "<IsDead>k__BackingField", true);
            Invoke(crosshair, "LateUpdate");
            Expect(!canvas.enabled, "Crosshair remains visible after death.");
            Set(health, "<IsDead>k__BackingField", false);
            Invoke(crosshair, "LateUpdate");
            Expect(canvas.enabled, "Crosshair did not return to hip mode.");

            Invoke(shooter, "ShootOnce");
            LineRenderer playerLine = player.GetComponentInChildren<LineRenderer>();
            Expect(playerLine != null && playerLine.enabled, "Player firing did not display a tracer.");
            Expect(playerLine.sharedMaterial != null && playerLine.sharedMaterial.shader.isSupported,
                "Tracer material is missing or unsupported.");
            Expect(!ShaderUtil.ShaderHasError(playerLine.sharedMaterial.shader), "Tracer shader has errors.");

            GameObject muzzle = new GameObject("Test Muzzle");
            muzzle.transform.SetParent(player.transform, false);
            muzzle.transform.position = new Vector3(-2.5f, -0.5f, 2f);
            Set(playerVfx, "muzzle", muzzle.transform);
            playerVfx.PlayTracer(new Vector3(-0.5f, 1.4f, 3f));
            Expect(playerLine.GetPosition(0) == muzzle.transform.position, "Player tracer does not start at the muzzle.");
            Vector3 shotStart = playerLine.GetPosition(0);
            player.transform.position += Vector3.right;
            Expect(playerLine.GetPosition(0) == shotStart, "Existing tracer moves with the shooter.");
            player.transform.position -= Vector3.right;
            playerVfx.PlayTracer(new Vector3(-0.5f, 1.4f, 3f));
            Expect(player.GetComponentsInChildren<LineRenderer>().Length == 1, "Repeated shots allocate extra renderers.");

            GameObject enemy = new GameObject("Test Enemy");
            WeaponVFX enemyVfx = enemy.AddComponent<WeaponVFX>();
            EnemyCombat combat = enemy.AddComponent<EnemyCombat>();
            enemyVfx.PlayTracer(new Vector3(2.5f, -0.5f, 2f), new Vector3(0.5f, 1.4f, 3f), combat.tracerColor);
            LineRenderer enemyLine = enemy.GetComponentInChildren<LineRenderer>();
            Expect(enemyLine.startColor.r > enemyLine.startColor.b, "Enemy tracer color was not applied.");

            // Screen-space camera uses the same UI marks and anchors for offscreen capture.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Capture(camera, canvas, 1280, 720, "shot-visuals-wide.png");
            Capture(camera, canvas, 720, 1280, "shot-visuals-tall.png");

            Set(playerVfx, "tracerEndTime", Time.time - 1f);
            Invoke(playerVfx, "LateUpdate");
            Expect(!playerLine.enabled && player.activeSelf, "Expired tracer disables the shooter or stays visible.");
            playerVfx.PlayTracer(Vector3.forward);
            Invoke(playerVfx, "OnDisable");
            Expect(!playerLine.enabled, "Disabled VFX leaves a visible tracer.");

            Debug.Log("[ShotVisualRegression] PASS: firing, material, muzzle, reuse, expiry, crosshair modes, and two rendered viewports.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Capture(Camera camera, Canvas canvas, int width, int height, string filename)
    {
        RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            pixels.Apply();

            int cyan = 0;
            int orange = 0;
            int whiteAtCenter = 0;
            Color32[] colors = pixels.GetPixels32();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 color = colors[y * width + x];
                    if (color.g > 150 && color.b > 150 && color.r < 150) cyan++;
                    if (color.r > 180 && color.g < 180 && color.b < 130) orange++;
                    if (Mathf.Abs(x - width / 2) <= 3 && Mathf.Abs(y - height / 2) <= 3 &&
                        color.r > 210 && color.g > 210 && color.b > 210) whiteAtCenter++;
                }
            }

            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../", filename));
            File.WriteAllBytes(output, pixels.EncodeToPNG());
            Debug.Log($"[ShotVisualRegression] {width}x{height}: cyan={cyan}, orange={orange}, center={whiteAtCenter}; {output}");
            Expect(cyan > 50 && orange > 50, "Rendered tracers are blank or incorrectly colored.");
            Expect(whiteAtCenter > 0, "Crosshair is not visible at the center.");
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void SetAim(ThirdPersonCamera controller, string name)
    {
        FieldInfo field = typeof(ThirdPersonCamera).GetField("aimState", PrivateInstance);
        field.SetValue(controller, Enum.Parse(field.FieldType, name));
    }

    private static void Invoke(object target, string name) =>
        target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
