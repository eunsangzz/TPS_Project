using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    //카메라 시점
    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.05f;
    [SerializeField] private float pitchMin = -35f;
    [SerializeField] private float pitchMax = 70f;

    [Header("Follow")]
    [SerializeField] private float followHeight = 2.1f;

    //카메라 이동시 충돌 방지
    [Header("Distance")]
    [SerializeField] private float defaultDistance = 3.0f;
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float collisionRadius = 0.25f; //반지름
    [SerializeField] private float distanceSmooth = 12f; //거리 보정시 연속성
    [SerializeField] private LayerMask collisionMask;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private ThirdPersonInput inputSource;

    [Header("Offset")]
    [SerializeField] private Vector3 normalOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 aimOffset = new Vector3(0.45f, 0.1f, 0f);
    [SerializeField] private float offsetSmooth = 10f;

    private float yaw;
    private float pitch;
    public float Yaw => yaw;

    private float currentDistance;
    private Vector3 currentOffset;

    private Transform cam;

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>().transform;
        currentDistance = defaultDistance;
        currentOffset = normalOffset;
    }

    private void Start()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;

        LockCursor(true);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LockCursor(false);

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) LockCursor(true);

        HandleLook();
        FollowTarget();
        HandleCollision();
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

        bool aim = inputSource != null && inputSource.AimHeld;

        Vector3 targetOffset = aim ? aimOffset : normalOffset;
        currentOffset = Vector3.Lerp(
            currentOffset,
            targetOffset,
            Time.deltaTime * offsetSmooth);

        Vector3 basePos = target.position + Vector3.up * followHeight;

        transform.position = basePos
            + transform.right * currentOffset.x
            + transform.up * currentOffset.y;
    }

    private void HandleCollision()
    {
        Vector3 origin = transform.position;
        Vector3 direction = -transform.forward;

        float targetDistance = defaultDistance;

        if(Physics.SphereCast(
            origin,
            collisionRadius,
            direction,
            out RaycastHit hit,
            defaultDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            targetDistance = Mathf.Max(hit.distance - 0.05f, minDistance);
        }

        currentDistance = Mathf.Lerp(
            currentDistance,
            targetDistance,
            Time.deltaTime * distanceSmooth);

        cam.localPosition = new Vector3(0f, 0f, -currentDistance);
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

}