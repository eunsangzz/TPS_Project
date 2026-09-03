using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class PlayerLeanPlayChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static IEnumerator Run(GameObject player, Keyboard keyboard)
    {
        PlayerLean lean = player.GetComponent<PlayerLean>();
        ThirdPersonInput input = player.GetComponent<ThirdPersonInput>();
        Animator animator = player.GetComponent<Animator>();
        Expect(lean != null && lean.Available, "Player lean was not initialized on the humanoid.");
        foreach (Key key in new[] { Key.Q, Key.E })
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            float until = Time.time + 0.4f;
            while (Time.time < until) yield return null;
            float sign = key == Key.Q ? -1f : 1f;
            Expect(input.Lean == sign && lean.CurrentLean * sign > 0.9f, "Q/E did not select the expected lean direction.");
            Expect(!input.CoverPressed && input.Move == Vector2.zero, "Lean key also triggered cover or movement.");
            VerifyUpperBodyOnly(player, lean, animator, sign);
            typeof(DodgePlayRegression).GetMethod("CapturePose", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { player, key == Key.Q ? "lean-left" : "lean-right" });
        }
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.E));
        InputSystem.Update();
        float settle = Time.time + 0.25f;
        while (Time.time < settle) yield return null;
        Expect(Mathf.Abs(input.Lean) < 0.001f && Mathf.Abs(lean.CurrentLean) < 0.001f, "Q+E should cancel each other.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.C));
        InputSystem.Update();
        Invoke(input, "Update");
        Expect(input.CoverPressed && input.Lean == 0f, "C did not replace E as the cover key.");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
        InputSystem.Update();
        settle = Time.time + 0.2f;
        while (Time.time < settle) yield return null;
        Quaternion orientation = player.transform.rotation;
        player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        VerifyUpperBodyOnly(player, lean, animator, 1f);
        player.transform.rotation = orientation;
        lean.enabled = false;
        Expect(lean.CurrentLean == 0f, "Disabling lean did not restore the pose.");
        lean.enabled = true;
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        settle = Time.time + 0.2f;
        while (Time.time < settle) yield return null;
        Expect(lean.CurrentLean == 0f, "Releasing lean keys did not return to neutral.");
        Debug.Log("[PlayerLeanPlayChecks] PASS: Q/E/C input, both-key cancellation, upper-body-only posing, rotated character, disable and release recovery.");
    }

    private static void VerifyUpperBodyOnly(GameObject player, PlayerLean lean, Animator animator, float sign)
    {
        Invoke(lean, "Update");
        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
        Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
        Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
        Vector3 headBefore = head.position;
        Vector3 hipsBefore = hips.position;
        Quaternion hipsRotation = hips.rotation;
        Vector3 leftBefore = left.position, rightBefore = right.position, rootBefore = player.transform.position;
        Quaternion rootRotation = player.transform.rotation;
        Invoke(lean, "LateUpdate");
        Expect(Vector3.Dot(head.position - headBefore, player.transform.right) * sign > 0.03f,
            "Upper body did not visibly lean in the requested character-relative direction.");
        Expect(Vector3.Distance(hips.position, hipsBefore) < 0.0001f && Quaternion.Angle(hips.rotation, hipsRotation) < 0.01f,
            "Lean moved or rotated the pelvis.");
        Expect(Vector3.Distance(left.position, leftBefore) < 0.0001f && Vector3.Distance(right.position, rightBefore) < 0.0001f,
            "Lean changed the feet.");
        Expect(player.transform.position == rootBefore && Quaternion.Angle(player.transform.rotation, rootRotation) < 0.01f,
            "Lean moved the character/collider root.");
    }
    private static void Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
