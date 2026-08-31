using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public static class EnemyHealthBarRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Requires an isolated batch project.");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            Camera camera = new GameObject("Health Bar Test Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 2f, -8f);
            camera.orthographic = true;
            camera.orthographicSize = 2.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.1f);

            EnemyHealth left = CreateEnemy("Full Health", new Vector3(-1.2f, 1f, 3f));
            EnemyHealth right = CreateEnemy("Low Health", new Vector3(1.2f, 1f, 3f));
            EnemyHealthBar bar = right.GetComponent<EnemyHealthBar>();
            Canvas canvas = right.GetComponentInChildren<Canvas>();
            Text label = right.GetComponentInChildren<Text>();
            RectTransform fill = right.transform.Find("EnemyHealthCanvas/Background/Track/Health").GetComponent<RectTransform>();
            Expect(!canvas.enabled && !right.HasTakenDamage, "Unhit enemy shows a health bar.");
            right.ApplyDamage(0f);
            right.ApplyDamage(-10f);
            Invoke(bar, "LateUpdate");
            Expect(!canvas.enabled && !right.HasTakenDamage, "Zero or negative damage reveals the bar.");
            Invoke(right, "Start");
            Expect(right.GetComponentsInChildren<Canvas>().Length == 1, "Repeated initialization creates duplicate bars.");
            foreach (Graphic graphic in right.GetComponentsInChildren<Graphic>())
                Expect(!graphic.raycastTarget, "Health bar intercepts input.");

            right.ApplyDamage(60f);
            Invoke(bar, "LateUpdate");
            Expect(canvas.enabled && right.HasTakenDamage, "First hit does not reveal the bar.");
            Expect(canvas.transform.position.y > right.GetComponent<Collider>().bounds.max.y, "Health bar is not above the enemy.");
            Expect(label.text == "40 / 100" && Mathf.Approximately(fill.anchorMax.x, 0.4f), "Damage is not reflected in the bar.");
            right.currentHealth = 80f;
            Invoke(bar, "LateUpdate");
            Expect(label.text == "80 / 100" && Mathf.Approximately(fill.anchorMax.x, 0.8f), "Health recovery is not reflected.");
            right.currentHealth = 100f;
            Invoke(bar, "LateUpdate");
            Expect(canvas.enabled && label.text == "100 / 100", "Full recovery hides a previously hit enemy.");
            right.currentHealth = 20f;
            Invoke(bar, "LateUpdate");
            left.ApplyDamage(1f);
            left.currentHealth = 100f;
            Invoke(left.GetComponent<EnemyHealthBar>(), "LateUpdate");
            EnemyHealth earlyHit = new GameObject("Hit before bar creation", typeof(EnemyHealth)).GetComponent<EnemyHealth>();
            earlyHit.ApplyDamage(10f);
            Invoke(earlyHit, "Start");
            Expect(earlyHit.GetComponentInChildren<Canvas>().enabled, "Damage before Start was lost.");
            UnityEngine.Object.DestroyImmediate(earlyHit.gameObject);
            Capture(camera);

            camera.transform.rotation = Quaternion.Euler(10f, 20f, 0f);
            Invoke(bar, "LateUpdate");
            Expect(Quaternion.Angle(canvas.transform.rotation, camera.transform.rotation) < 0.01f, "Bar does not face the camera.");
            right.currentHealth = 0f;
            Invoke(bar, "LateUpdate");
            Expect(!canvas.enabled, "Dead enemy still shows a health bar.");
            right.currentHealth = 100f;
            Invoke(bar, "LateUpdate");
            Invoke(bar, "OnDisable");
            Expect(!canvas.enabled, "Disabled health bar stays visible.");
            Debug.Log("[EnemyHealthBarRegression] PASS: hidden until first hit, zero damage, early hit, reuse, placement, recovery, death, camera facing, and rendered health bars.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static EnemyHealth CreateEnemy(string name, Vector3 position)
    {
        GameObject enemy = new GameObject(name, typeof(CapsuleCollider), typeof(EnemyHealth));
        enemy.transform.position = position;
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        Invoke(health, "Start");
        return health;
    }

    private static void Capture(Camera camera)
    {
        RenderTexture target = new RenderTexture(1280, 720, 24);
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0f, 0f, 1280f, 720f), 0, 0);
        pixels.Apply();
        int green = 0;
        int red = 0;
        int white = 0;
        foreach (Color32 color in pixels.GetPixels32())
        {
            if (color.g > 170 && color.r < 170 && color.b < 200) green++;
            if (color.r > 180 && color.g < 170 && color.b < 170) red++;
            if (color.r > 220 && color.g > 220 && color.b > 220) white++;
        }
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../enemy-health-bars.png"));
        File.WriteAllBytes(path, pixels.EncodeToPNG());
        Debug.Log($"[EnemyHealthBarRegression] green={green}, red={red}, text={white}; {path}");
        Expect(green > 50 && red > 10 && white > 30, "Health bars or numbers did not render.");
        camera.targetTexture = null;
        RenderTexture.active = previous;
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(pixels);
    }

    private static void Invoke(object target, string name) =>
        target.GetType().GetMethod(name, PrivateInstance).Invoke(target, null);

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
