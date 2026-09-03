using System;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-10)]
public sealed class DodgeRollAnimation : MonoBehaviour
{
    private Animator animator;
    private HumanPoseHandler poseHandler;
    private HumanPose pose;
    private HumanPose animatedPose;
    private bool poseApplied;
    private float startedAt;
    private float duration;
    private bool playing;

    private int spineFrontBack, chestFrontBack;
    private int leftUpperLegFrontBack, rightUpperLegFrontBack;
    private int leftLowerLegStretch, rightLowerLegStretch;
    private int leftArmDownUp, rightArmDownUp;
    private int leftForearmStretch, rightForearmStretch;

    public bool Available => poseHandler != null;
    public bool IsPlaying => playing;

    public void Initialize(Animator target)
    {
        if (animator == target && poseHandler != null) return;
        Cancel();
        poseHandler?.Dispose();
        animator = target;
        poseHandler = null;
        if (animator == null || animator.avatar == null || !animator.avatar.isHuman) return;
        poseHandler = new HumanPoseHandler(animator.avatar, animator.transform);
        pose = new HumanPose();
        animatedPose = new HumanPose();
        // Unity avatar APIs must run here, never in static/field initializers.
        spineFrontBack = Muscle("Spine Front-Back");
        chestFrontBack = Muscle("Chest Front-Back");
        leftUpperLegFrontBack = Muscle("Left Upper Leg Front-Back");
        rightUpperLegFrontBack = Muscle("Right Upper Leg Front-Back");
        leftLowerLegStretch = Muscle("Left Lower Leg Stretch");
        rightLowerLegStretch = Muscle("Right Lower Leg Stretch");
        leftArmDownUp = Muscle("Left Arm Down-Up");
        rightArmDownUp = Muscle("Right Arm Down-Up");
        leftForearmStretch = Muscle("Left Forearm Stretch");
        rightForearmStretch = Muscle("Right Forearm Stretch");
    }

    public bool Play(float rollDuration)
    {
        if (poseHandler == null) Initialize(GetComponent<Animator>());
        if (!Available) return false;
        duration = Mathf.Max(0.1f, rollDuration);
        startedAt = Time.time;
        playing = true;
        return true;
    }

    public void Cancel()
    {
        playing = false;
        RestoreAnimatedPose();
    }

    // Remove last frame's procedural pose before Animator evaluates this frame.
    // This also prevents cumulative rotation when an Animator is culled.
    private void Update() => RestoreAnimatedPose();

    private void RestoreAnimatedPose()
    {
        if (!poseApplied || poseHandler == null) return;
        poseHandler.SetHumanPose(ref animatedPose);
        poseApplied = false;
    }

    private void OnDisable() => Cancel();

    private void OnDestroy()
    {
        poseHandler?.Dispose();
        poseHandler = null;
    }

    private void LateUpdate()
    {
        if (!playing || poseHandler == null) return;
        float normalized = Mathf.Clamp01((Time.time - startedAt) / duration);
        if (normalized >= 1f)
        {
            Cancel();
            return;
        }

        GetLocalPose(ref pose);
        GetLocalPose(ref animatedPose);
        float smooth = normalized * normalized * (3f - 2f * normalized);
        float tuck = Mathf.Sin(normalized * Mathf.PI);
        pose.bodyRotation = pose.bodyRotation * Quaternion.AngleAxis(360f * smooth, Vector3.right);
        pose.bodyPosition += Vector3.up * (Mathf.Sin(normalized * Mathf.PI) * 0.08f);
        AddMuscle(spineFrontBack, 0.75f * tuck);
        AddMuscle(chestFrontBack, 0.55f * tuck);
        AddMuscle(leftUpperLegFrontBack, 0.8f * tuck);
        AddMuscle(rightUpperLegFrontBack, 0.8f * tuck);
        AddMuscle(leftLowerLegStretch, -0.75f * tuck);
        AddMuscle(rightLowerLegStretch, -0.75f * tuck);
        AddMuscle(leftArmDownUp, -0.45f * tuck);
        AddMuscle(rightArmDownUp, -0.45f * tuck);
        AddMuscle(leftForearmStretch, -0.6f * tuck);
        AddMuscle(rightForearmStretch, -0.6f * tuck);
        poseHandler.SetHumanPose(ref pose);
        poseApplied = true;
    }

    private void AddMuscle(int index, float value)
    {
        if (index < 0 || pose.muscles == null || index >= pose.muscles.Length) return;
        pose.muscles[index] = Mathf.Clamp(pose.muscles[index] + value, -1f, 1f);
    }

    private void GetLocalPose(ref HumanPose target)
    {
        poseHandler.GetHumanPose(ref target);
        // GetHumanPose returns world-space COM; SetHumanPose expects root-relative COM.
        // Without this conversion the visual rig drifts away from a moving collider.
        Quaternion inverseRoot = Quaternion.Inverse(animator.transform.rotation);
        target.bodyPosition = inverseRoot * (target.bodyPosition - animator.transform.position / animator.humanScale);
        target.bodyRotation = inverseRoot * target.bodyRotation;
    }

    private static int Muscle(string name) => Array.IndexOf(HumanTrait.MuscleName, name);
}
