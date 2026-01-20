using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class ThirdPersonMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5.5f;
    [SerializeField] private float sprintSpeed = 8.0f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float rotationSpeed = 14f;

    [Header("Jump")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedStickForce = -3f;

    [Header("References")]
    [SerializeField] private Transform cameraRoot;
    [SerializeField] private ThirdPersonInput input;
    [SerializeField] private ThirdPersonCamera cameraController;

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
            
    }

    private void Update()
    {
        HandleMovement();
        HandleJumpAndGravity();
        ApplyMove();
    }

    private void HandleMovement()
    {
        Vector2 raw = (input != null) ? input.Move : Vector2.zero;
        Vector2 moveInput = Vector2.ClampMagnitude(raw, 1f);

        bool aim = input != null && input.AimHeld;
        bool freeLook = input != null && input.FreeLookHeld;

        bool sprint = (input != null) && input.SprintHeld && !aim;

        float targetSpeed = sprint ? sprintSpeed : moveSpeed;
        float targetMagnitude = moveInput.magnitude * targetSpeed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetMagnitude, acceleration * Time.deltaTime);

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

        if(freeLook)
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

    private void HandleJumpAndGravity()
    {
        bool grounded = controller.isGrounded;

        if (grounded && velocity.y < 0f) velocity.y = groundedStickForce;
       

        if(grounded && input.JumpPressed) velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
    }

    private void ApplyMove()
    {
        Vector3 move = currentMove;

        move.y = velocity.y;

        controller.Move(move * Time.deltaTime);
    }
}
