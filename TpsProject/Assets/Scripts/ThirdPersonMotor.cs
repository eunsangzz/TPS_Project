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
    private Vector3 currnetMove;
    private float currentSpeed;

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

        bool aim = (input != null) && input.AimHeld;
        bool sprint = (input != null) && input.SprintHeld && !aim;

        float targetSpeed = sprint ? sprintSpeed : moveSpeed;
        float targetMagnitude = moveInput.magnitude * targetSpeed;

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetMagnitude, acceleration * Time.deltaTime);
        
        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;

        if (cameraRoot != null)
        {
            camForward = cameraRoot.forward; camForward.y = 0f; camForward.Normalize();
            camRight = cameraRoot.right; camRight.y = 0f; camRight.Normalize();
        }

        Vector3 desiredMove = (camForward * moveInput.y + camRight * moveInput.x);
        if (desiredMove.sqrMagnitude > 0.0001f) desiredMove.Normalize();
        desiredMove *= currentSpeed;

        currnetMove = desiredMove;

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
        if (grounded && velocity.y < 0f)
        {
            velocity.y = groundedStickForce;
        }

        if(grounded && input.JumpPressed)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
    }

    private void ApplyMove()
    {
        Vector3 move = currnetMove;
        move.y = velocity.y;

        controller.Move(move * Time.deltaTime);
    }
}
