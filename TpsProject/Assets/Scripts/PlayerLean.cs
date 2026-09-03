using UnityEngine;

// Runs after roll posing (-10), before weapon attachments follow the hands (0).
[DefaultExecutionOrder(-5)]
[DisallowMultipleComponent]
public sealed class PlayerLean : MonoBehaviour
{
    [SerializeField, Range(0f, 40f)] private float leanAngle = 24f;
    [SerializeField, Min(0.1f)] private float leanSpeed = 6f;
    private Animator animator;
    private ThirdPersonInput input;
    private PlayerDodge dodge;
    private PlayerHealth health;
    private Transform spine;
    private Transform chest;
    private Quaternion spinePose;
    private Quaternion chestPose;
    private bool poseApplied;
    private float currentLean;

    public float CurrentLean => currentLean;
    public bool Available => spine != null;

    private void Awake()
    {
        input = GetComponent<ThirdPersonInput>();
        dodge = GetComponent<PlayerDodge>();
        health = GetComponent<PlayerHealth>();
        Initialize(GetComponentInChildren<Animator>());
    }

    public void Initialize(Animator target)
    {
        if (animator == target && spine != null) return;
        Cancel();
        animator = target;
        spine = chest = null;
        if (animator == null || !animator.isHuman) return;
        spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        chest = animator.GetBoneTransform(HumanBodyBones.Chest);
    }

    // Restore the unmodified local bone pose before Animator evaluates a new frame.
    // Never change hips, legs, root position, or the CharacterController.
    private void Update() => RestorePose();

    private void LateUpdate()
    {
        if (dodge == null) dodge = GetComponent<PlayerDodge>();
        if (!Available || animator == null || !animator.isActiveAndEnabled ||
            (health != null && health.IsDead) || (dodge != null && dodge.IsDodging))
        {
            Cancel();
            return;
        }
        float target = input != null && input.isActiveAndEnabled ? input.Lean : 0f;
        currentLean = Mathf.MoveTowards(currentLean, target, leanSpeed * Time.deltaTime);
        if (Mathf.Abs(currentLean) < 0.001f) return;

        spinePose = spine.localRotation;
        if (chest != null) chestPose = chest.localRotation;
        Vector3 axis = animator.transform.forward;
        float angle = -currentLean * leanAngle;
        spine.rotation = Quaternion.AngleAxis(angle * (chest != null ? 0.6f : 1f), axis) * spine.rotation;
        if (chest != null)
            chest.rotation = Quaternion.AngleAxis(angle * 0.4f, axis) * chest.rotation;
        poseApplied = true;
    }

    private void RestorePose()
    {
        if (!poseApplied) return;
        if (spine != null) spine.localRotation = spinePose;
        if (chest != null) chest.localRotation = chestPose;
        poseApplied = false;
    }

    public void Cancel()
    {
        RestorePose();
        currentLean = 0f;
    }

    private void OnDisable() => Cancel();
}
