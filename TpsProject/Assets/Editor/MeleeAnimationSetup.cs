using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MeleeAnimationSetup
{
    public const string ControllerPath = "Assets/SciFiWarriorPBRHPPolyart/Animators/SciFiWarrior.controller";
    public const string AttackPath = "Assets/Resources/PlayerMeleeAttack.anim";
    public const string ReadyPath = "Assets/Resources/PlayerMeleeReady.anim";
    public const string MaskPath = "Assets/Resources/PlayerMeleeUpperBody.mask";

    [MenuItem("Tools/Animation/Rebuild Player Melee Clips")]
    public static void Build()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) throw new InvalidOperationException("Player Animator Controller is missing.");
        AnimationClip attack = Clip(AttackPath, "PlayerMeleeAttack");
        AnimationClip ready = Clip(ReadyPath, "PlayerMeleeReady");
        float[] times = { 0f, 0.18f, PlayerMeleeAnimation.ImpactTime, 0.40f, PlayerMeleeAnimation.Duration };
        void Muscle(string name, params float[] values)
        {
            if (Array.IndexOf(HumanTrait.MuscleName, name) < 0) throw new InvalidOperationException("Unknown humanoid muscle: " + name);
            Keyframe[] keys = times.Select((time, i) => new Keyframe(time, values[i])).ToArray();
            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            }
            string property = name;
            foreach (string side in new[] { "Left", "Right" })
                foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                    if (name.StartsWith(side + " " + finger + " ", StringComparison.Ordinal))
                        property = side + "Hand." + finger + "." + name.Substring((side + " " + finger + " ").Length);
            var binding = EditorCurveBinding.FloatCurve("", typeof(Animator), property);
            AnimationUtility.SetEditorCurve(attack, binding, curve);
            AnimationUtility.SetEditorCurve(ready, binding, AnimationCurve.Constant(0f, 1f, values[0]));
        }
        Muscle("Spine Front-Back", 0.04f, -0.12f, 0.12f, 0.18f, 0.04f);
        Muscle("Spine Left-Right", 0f, -0.06f, 0.06f, 0.1f, 0f);
        Muscle("Spine Twist Left-Right", -0.05f, -0.25f, 0.15f, 0.22f, -0.05f);
        Muscle("Chest Front-Back", 0f, -0.08f, 0.08f, 0.12f, 0f);
        Muscle("Chest Twist Left-Right", -0.04f, -0.24f, 0.14f, 0.22f, -0.04f);
        Muscle("Head Turn Left-Right", 0f, 0.08f, -0.06f, -0.08f, 0f);
        Muscle("Right Shoulder Down-Up", -0.25f, 0.35f, -0.35f, -0.35f, -0.25f);
        Muscle("Right Shoulder Front-Back", 0.075f, 0.35f, -0.35f, -0.35f, 0.075f);
        Muscle("Right Arm Down-Up", -0.5f, -0.125f, -0.3f, -0.75f, -0.5f);
        Muscle("Right Arm Front-Back", 0.25f, 0.975f, -0.475f, -0.25f, 0.25f);
        Muscle("Right Arm Twist In-Out", 0.125f, 1f, -0.28f, -0.42f, 0.125f);
        Muscle("Right Forearm Stretch", -0.175f, -0.1f, 0.9f, 0.8f, -0.175f);
        Muscle("Right Forearm Twist In-Out", 0f, 0.15f, -0.12f, -0.05f, 0f);
        Muscle("Right Hand Down-Up", 0.05f, -0.15f, 0.1f, 0.2f, 0.05f);
        Muscle("Right Hand In-Out", 0f, 0.05f, -0.05f, 0f, 0f);
        Muscle("Left Shoulder Down-Up", -0.1f, 0f, 0.04f, 0f, -0.1f);
        Muscle("Left Arm Down-Up", -0.7f, -0.5f, -0.45f, -0.6f, -0.7f);
        Muscle("Left Arm Front-Back", 0.1f, 0.18f, -0.08f, -0.05f, 0.1f);
        Muscle("Left Forearm Stretch", -0.2f, -0.45f, -0.2f, -0.1f, -0.2f);
        foreach (string side in new[] { "Left", "Right" })
            foreach (string finger in new[] { "Thumb", "Index", "Middle", "Ring", "Little" })
                for (int joint = 1; joint <= 3; joint++)
                    Muscle($"{side} {finger} {joint} Stretched", -0.65f, -0.65f, -0.65f, -0.65f, -0.65f);
        AnimationUtility.SetAnimationEvents(attack, new[] { new AnimationEvent
        {
            time = PlayerMeleeAnimation.ImpactTime,
            functionName = nameof(PlayerMeleeAnimation.OnMeleeImpact),
            messageOptions = SendMessageOptions.DontRequireReceiver
        } });
        Settings(attack, false);
        Settings(ready, true);

        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
        if (mask == null) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, MaskPath); }
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
        foreach (AvatarMaskBodyPart part in new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head,
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers })
            mask.SetHumanoidBodyPartActive(part, true);

        AnimatorControllerLayer[] layers = controller.layers;
        int index = Array.FindIndex(layers, layer => layer.name == PlayerMeleeAnimation.LayerName);
        if (index < 0)
        {
            controller.AddLayer(PlayerMeleeAnimation.LayerName);
            layers = controller.layers;
            index = layers.Length - 1;
        }
        AnimatorControllerLayer layer = layers[index];
        layer.avatarMask = mask;
        layer.defaultWeight = 0f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        AnimatorState readyState = State(layer.stateMachine, "Ready", ready, new Vector3(250f, 60f));
        AnimatorState attackState = State(layer.stateMachine, "Attack", attack, new Vector3(250f, 160f));
        layer.stateMachine.defaultState = readyState;
        if (!attackState.transitions.Any(t => t.destinationState == readyState))
        {
            AnimatorStateTransition transition = attackState.AddTransition(readyState);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.hasFixedDuration = true;
            transition.duration = 0.04f;
        }
        layers[index] = layer;
        controller.layers = layers;
        EditorUtility.SetDirty(mask);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("[MeleeAnimationSetup] Created editable humanoid ready/attack clips and a masked upper-body layer.");
    }

    private static AnimationClip Clip(string path, string name)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves();
        clip.name = name;
        clip.frameRate = 60f;
        return clip;
    }

    private static void Settings(AnimationClip clip, bool loop)
    {
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        settings.keepOriginalOrientation = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static AnimatorState State(AnimatorStateMachine machine, string name, AnimationClip clip, Vector3 position)
    {
        AnimatorState state = machine.states.Select(child => child.state).FirstOrDefault(candidate => candidate.name == name);
        if (state == null) state = machine.AddState(name, position);
        state.motion = clip;
        state.writeDefaultValues = false;
        return state;
    }
}
