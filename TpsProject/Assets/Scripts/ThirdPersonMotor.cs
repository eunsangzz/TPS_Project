using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float sprintSpeed = 8.0f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float rotationSpeed = 14f;

    [Header("Grounding")]
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -3f;

    [Header("References")]
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private ThirdPersonInput input;
    [SerializeField] private ThirdPersonCamera cameraController;
    [SerializeField] private Animator animator;
    [SerializeField] private CoverController cover;
    [SerializeField] private PlayerDodge dodge;

    private CharacterController controller;

    private Vector3 velocity;
    private Vector3 currentMove;
    private float currentSpeed;

    private bool moveYawLocked;
    private float lockedMoveYaw;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (input == null) input = GetComponent<ThirdPersonInput>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (cover == null) cover = GetComponent<CoverController>();
        if (dodge == null) dodge = GetComponent<PlayerDodge>();
        if (dodge == null) dodge = gameObject.AddComponent<PlayerDodge>();
        dodge.Initialize(cameraRoot, animator);
        PlayerLean lean = GetComponent<PlayerLean>();
        if (lean == null) lean = gameObject.AddComponent<PlayerLean>();
        lean.Initialize(animator);
    }

    private void Update()
    {
        HandleMovement();
        HandleGravity();
        ApplyMove();
        UpdataAnimator();
    }

    private void HandleMovement()
    {
        if (dodge != null && dodge.IsDodging)
        {
            currentMove = dodge.MovementVelocity;
            currentSpeed = currentMove.magnitude;
            if (currentMove.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(currentMove, Vector3.up);
            return;
        }

        Vector2 raw = (input != null) ? input.Move : Vector2.zero;
        Vector2 moveInput = Vector2.ClampMagnitude(raw, 1f);

        bool aim = (cameraController != null) && cameraController.IsAiming;
        bool freeLook = input != null && input.FreeLookHeld;

        bool sprint = (input != null) && input.SprintHeld && !aim;

        bool inCover = (cover != null && cover.InCover); 

        float targetSpeed = sprint ? sprintSpeed : moveSpeed;
        float targetMagnitude = moveInput.magnitude * targetSpeed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetMagnitude, acceleration * Time.deltaTime);

        if(inCover)
        {
            currentMove = Vector3.zero;
            currentSpeed = 0f;
            return;
        }

        if (moveInput.sqrMagnitude < 0.0001f)
        {
            moveYawLocked = false;
        }
        else if (freeLook)
        {
            if (!moveYawLocked) 
            {
                moveYawLocked = true;
                lockedMoveYaw = transform.eulerAngles.y;
            }
        }
        else
        {
            moveYawLocked = false;
        }

        Vector3 basicForward;
        Vector3 basicRight;

        if(freeLook && moveYawLocked)
        {
            Quaternion basicRot = Quaternion.Euler(0f, lockedMoveYaw, 0f);
            basicForward = basicRot * Vector3.forward;
            basicRight = basicRot * Vector3.right;
        }
        else
        {
            basicForward = Vector3.forward;
            basicRight = Vector3.right;

            if (cameraRoot != null)
            {
                basicForward = cameraRoot.forward; basicForward.y = 0f; basicForward.Normalize();
                basicRight = cameraRoot.right; basicRight.y = 0f; basicRight.Normalize();
            }
        }

        Vector3 desiredMove = (basicForward * moveInput.y + basicRight * moveInput.x);
        if (desiredMove.sqrMagnitude > 0.0001f) desiredMove.Normalize();
        desiredMove *= currentSpeed;

        currentMove = desiredMove;

        // Keep the upper-body strike aligned with its damage cone while the legs can move.
        if(freeLook || (TryGetComponent<PlayerMelee>(out var melee) && melee.IsAttacking))
        {
            return;
        }

        if (aim)
        {
            float targetYaw = (cameraController != null) ? cameraController.Yaw : transform.eulerAngles.y;
            Quaternion targetRot = Quaternion.Euler(0f, targetYaw, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
        else
        {
            if (desiredMove.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(desiredMove, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }
    }

    private void HandleGravity()
    {
        bool grounded = controller.isGrounded;

        if (grounded && velocity.y < 0f) velocity.y = groundedStickForce;
        velocity.y += gravity * Time.deltaTime;
    }

    private void ApplyMove()
    {
        Vector3 move = currentMove;

        move.y = velocity.y;

        controller.Move(move * Time.deltaTime);
    }

    private void UpdataAnimator()
    {
        if (animator == null) return;

        float speedPercent = Mathf.InverseLerp(0f, sprintSpeed, currentSpeed);

        animator.SetFloat("MoveSpeed", speedPercent, 0.1f, Time.deltaTime);
        bool isDodging = dodge != null && dodge.IsDodging;
        animator.SetBool("IsSprint", !isDodging && input != null && input.SprintHeld);
        animator.SetBool("IsAim", !isDodging && cameraController != null && cameraController.IsAiming);
        animator.SetBool("IsGrounded", controller.isGrounded);
    }
}
