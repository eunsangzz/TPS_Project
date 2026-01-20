using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2.2f;
    [SerializeField] private float pitchMin = -35f;
    [SerializeField] private float pitchMax = 70f;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private ThirdPersonInput inputSource;

    private float yaw;
    private float pitch;

    private void Start()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;

        LockCursor(true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            LockCursor(false);

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            LockCursor(true);

        HandleLook();
        FollowTarget();
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 look = (inputSource != null) ? inputSource.Look : Vector2.zero;

        yaw += look.x * mouseSensitivity;
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void FollowTarget()
    {
        if (target == null) return;

        transform.position = target.position + Vector3.up * 1.6f;
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

}