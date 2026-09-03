using UnityEngine;

[DefaultExecutionOrder(-25)]
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(ThirdPersonInput))]
public sealed class PlayerDodge : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float duration = 0.65f;
    [SerializeField, Min(0.1f)] private float cooldown = 1.0f;
    [SerializeField, Min(0.1f)] private float speed = 8.5f;
    [SerializeField, Min(0.1f)] private float distanceMultiplier = 1.15f;
    [SerializeField, Range(0f, 1f)] private float invulnerableStart = 0.08f;
    [SerializeField, Range(0f, 1f)] private float invulnerableEnd = 0.78f;

    private ThirdPersonInput input;
    private CharacterController controller;
    private PlayerHealth health;
    private Transform cameraRoot;
    private DodgeRollAnimation rollAnimation;
    private Vector3 direction;
    private float startedAt = -100f;
    private float nextDodgeTime;

    public bool IsDodging => Time.time < startedAt + duration;
    public float NormalizedTime => IsDodging ? Mathf.Clamp01((Time.time - startedAt) / duration) : 1f;
    public bool IsInvulnerable => IsDodging && NormalizedTime >= invulnerableStart && NormalizedTime <= invulnerableEnd;
    public Vector3 MovementVelocity => IsDodging ? direction * speed * distanceMultiplier * Mathf.Lerp(1f, 0.45f, NormalizedTime) : Vector3.zero;

    public void Initialize(Transform movementCamera, Animator animator)
    {
        if (movementCamera != null) cameraRoot = movementCamera;
        if (rollAnimation == null && animator != null)
        {
            rollAnimation = animator.GetComponent<DodgeRollAnimation>();
            if (rollAnimation == null) rollAnimation = animator.gameObject.AddComponent<DodgeRollAnimation>();
            rollAnimation.Initialize(animator);
        }
    }

    private void Awake()
    {
        input = GetComponent<ThirdPersonInput>();
        controller = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealth>();
        Animator animator = GetComponentInChildren<Animator>();
        Initialize(Camera.main != null ? Camera.main.transform : null, animator);
    }

    private void Update()
    {
        if (health != null && health.IsDead)
        {
            Cancel();
            return;
        }
        if (Time.timeScale <= 0f || input == null || !input.DodgePressed) return;
        TryDodge();
    }

    public bool TryDodge()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f || IsDodging || Time.time < nextDodgeTime) return false;
        if (health != null && health.IsDead) return false;
        if (controller == null || !controller.enabled || !controller.isGrounded) return false;
        CoverController cover = GetComponent<CoverController>();
        if (cover != null && cover.InCover) return false;

        Vector2 move = Vector2.ClampMagnitude(input != null ? input.Move : Vector2.zero, 1f);
        Vector3 forward = cameraRoot != null ? Vector3.ProjectOnPlane(cameraRoot.forward, Vector3.up).normalized : transform.forward;
        Vector3 right = cameraRoot != null ? Vector3.ProjectOnPlane(cameraRoot.right, Vector3.up).normalized : transform.right;
        direction = forward * move.y + right * move.x;
        if (direction.sqrMagnitude < 0.001f) direction = transform.forward;
        direction.Normalize();

        startedAt = Time.time;
        GetComponent<PlayerLean>()?.Cancel();
        nextDodgeTime = Time.time + cooldown;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        ThirdPersonCamera aimCamera = cameraRoot != null ? cameraRoot.GetComponentInParent<ThirdPersonCamera>() : null;
        if (aimCamera == null && Camera.main != null) aimCamera = Camera.main.GetComponentInParent<ThirdPersonCamera>();
        aimCamera?.CancelAimForDodge();
        GetComponent<PlayerMelee>()?.CancelAttack();
        GetComponent<ThirdPersonShooter>()?.CancelReload();
        rollAnimation?.Play(duration);
        return true;
    }

    public void Cancel()
    {
        startedAt = -100f;
        rollAnimation?.Cancel();
    }

    private void OnDisable() => Cancel();
}
