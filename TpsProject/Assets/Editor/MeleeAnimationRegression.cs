using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class MeleeAnimationRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Requires an isolated batch project.");
        try
        {
            MeleeAnimationSetup.Build();
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(MeleeAnimationSetup.AttackPath);
            Expect(clip.humanMotion, "Generated clip is not a humanoid animation.");
            Expect(Mathf.Abs(clip.length - PlayerMeleeAnimation.Duration) < 0.01f, "Incorrect attack duration.");
            Expect(clip.events.Length == 1 && Mathf.Approximately(clip.events[0].time, PlayerMeleeAnimation.ImpactTime), "Impact event is missing.");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MeleeAnimationSetup.ControllerPath);
            Expect(controller.layers.Length == 3 && controller.layers[2].defaultWeight == 0f, "Existing layers or enemy defaults changed.");
            AvatarMask mask = controller.layers[2].avatarMask;
            Expect(!mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg) && !mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), "Attack overrides locomotion/root motion.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            Camera camera = new GameObject("Animation Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(3.7f, 2.2f, 4.7f);
            camera.transform.LookAt(new Vector3(0f, 1.2f, 0f));
            camera.orthographic = true;
            camera.orthographicSize = 1.7f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.12f, 0.14f);
            Light light = new GameObject("Key Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2.5f;
            light.transform.rotation = Quaternion.Euler(35f, -140f, 0f);
            GameObject model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/HPCharacter.prefab"));
            Animator animator = model.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;
            animator.Rebind();
            animator.Update(0f);
            WeaponData data = ScriptableObject.CreateInstance<WeaponData>();
            model.AddComponent<PlayerHealth>();
            ThirdPersonShooter shooter = model.AddComponent<ThirdPersonShooter>();
            Set(shooter, "weaponData", data);
            Set(shooter, "shooterCamera", camera);
            Invoke(shooter, "Awake");
            PlayerLoadout loadout = model.GetComponent<PlayerLoadout>();
            loadout.TrySelectSlot(1);
            foreach (Canvas canvas in model.GetComponentsInChildren<Canvas>()) canvas.gameObject.SetActive(false);
            PlayerMelee melee = model.GetComponent<PlayerMelee>();
            PlayerMeleeAnimation driver = model.GetComponent<PlayerMeleeAnimation>();
            Expect(driver.Available, "Humanoid attack driver is unavailable on actual HPCharacter.");
            int layer = animator.GetLayerIndex(PlayerMeleeAnimation.LayerName);
            animator.SetLayerWeight(layer, 1f);
            Vector3[] hands = new Vector3[4];
            float[] times = { 0f, 0.18f, 0.28f, 0.4f };
            string[] poses = { "ready", "windup", "impact", "follow-through" };
            for (int i = 0; i < times.Length; i++)
            {
                animator.Play("PlayerMelee.Attack", layer, times[i] / clip.length);
                animator.Update(0f);
                Invoke(melee, "LateUpdate");
                hands[i] = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                Debug.Log($"[MeleeAnimationRegression] {poses[i]} hand={hands[i]}, head={animator.GetBoneTransform(HumanBodyBones.Head).position}");
                Capture(camera, "melee-animation-" + poses[i] + ".png");
            }
            Expect(Vector3.Distance(hands[0], hands[1]) > 0.2f && Vector3.Distance(hands[1], hands[2]) > 0.2f, "Arm is not moving through distinct attack poses.");
            Transform baton = model.GetComponentInChildren<PlayerMelee>().GetComponentsInChildren<Transform>(true)[0];
            foreach (Transform child in model.GetComponentsInChildren<Transform>(true))
                if (child.name == "MeleeBaton") baton = child;
            Expect(baton.lossyScale.x > 0.9f && baton.lossyScale.x < 1.1f, "Hand scale shrinks the weapon.");
            Quaternion localGrip = baton.localRotation;
            animator.Play("PlayerMelee.Attack", layer, 0.5f);
            animator.Update(0f);
            Invoke(melee, "LateUpdate");
            Expect(Quaternion.Angle(localGrip, baton.localRotation) < 0.01f, "Baton rotates independently of the hand.");
            loadout.TrySelectSlot(2);
            Invoke(driver, "OnDisable");
            Expect(animator.GetLayerWeight(layer) == 0f, "Upper-body layer is not released.");
            MeleeAnimationSetup.Build();
            Expect(controller.layers.Length == 3, "Rebuilding duplicates the melee layer.");
            Debug.Log("[MeleeAnimationRegression] PASS: humanoid clip, upper-body mask, actual avatar poses, hand attachment/scale, restoration, and idempotent setup.");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void Capture(Camera camera, string name)
    {
        // Edit-mode Camera.Render does not advance skinning; bake the sampled pose explicitly.
        var snapshots = new System.Collections.Generic.List<GameObject>();
        var sources = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);
        foreach (SkinnedMeshRenderer source in sources)
        {
            if (!source.enabled) continue;
            Mesh mesh = new Mesh();
            source.BakeMesh(mesh, true);
            GameObject snapshot = new GameObject("Pose snapshot", typeof(MeshFilter), typeof(MeshRenderer));
            snapshot.transform.SetParent(source.transform, false);
            snapshot.GetComponent<MeshFilter>().sharedMesh = mesh;
            snapshot.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            snapshots.Add(snapshot);
            source.enabled = false;
        }
        RenderTexture target = new RenderTexture(960, 960, 24);
        camera.targetTexture = target;
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(960, 960, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0f, 0f, 960f, 960f), 0, 0);
        image.Apply();
        int foreground = 0;
        int pink = 0;
        int armor = 0;
        foreach (Color32 pixel in image.GetPixels32())
        {
            if (pixel.r > 80 || pixel.g > 80 || pixel.b > 80) foreground++;
            if (pixel.r > 220 && pixel.b > 220 && pixel.g < 40) pink++;
            if (pixel.r > 160 && pixel.g > 160 && pixel.b > 160) armor++;
        }
        Expect(foreground > 2000 && armor > 10000 && pink < 100, "Avatar did not render, or has missing materials.");
        File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../../", name)), image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        foreach (GameObject snapshot in snapshots)
        {
            snapshot.transform.parent.GetComponent<SkinnedMeshRenderer>().enabled = true;
            UnityEngine.Object.DestroyImmediate(snapshot.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.DestroyImmediate(snapshot);
        }
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
