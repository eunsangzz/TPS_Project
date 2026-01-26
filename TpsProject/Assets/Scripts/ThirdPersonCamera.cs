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

    [Header("Zoom")]
    [SerializeField] private bool enableScopeZoom = true;
    [SerializeField] private float doubleClickTime = 0.25f;
    [SerializeField] private float zoomMutiplier = 4.0f; //배율
    [SerializeField] private float zoomSmooth = 12f;

    [Header("Tuning")]
    [SerializeField, Range(0.05f, 1f)] private float scopedSensitivityMultiplier = 0.35f;

    [SerializeField] private Vector3 scopeOffset = new Vector3(0f, 0.05f, 0f);

    private float lastRightClickTime = -999f;
    private bool isScoped;
    private bool scopeHoldActive;
    public bool IsScoped => isScoped;

    private float yaw;
    private float pitch;
    public float Yaw => yaw;

    private float currentDistance;
    private Vector3 currentOffset;

    private Transform cam;
    private Camera camComponent;

    private float defaltFov;
    private float targetFov;

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>().transform;
        camComponent = GetComponent<Camera>();

        currentDistance = defaultDistance;
        currentOffset = normalOffset;

        if (camComponent != null) 
        {
            defaltFov = camComponent.fieldOfView;
            targetFov = defaltFov;
        }
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

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) LockCursor(true);

        HandelScopeZoom_DoubleClick();
        UpdateScopeHoldRelease();
        UpdateCameraFov();

        HandleLook();
        FollowTarget();
        HandleCollision();
    }

    private void HandelScopeZoom_DoubleClick()
    {
        if (!enableScopeZoom) return;
        if (Mouse.current == null) return;

        if (!Mouse.current.rightButton.wasPressedThisFrame) return;

        float now = Time.deltaTime;

        if (now - lastRightClickTime <= doubleClickTime) 
        {
            scopeHoldActive = true;
            SetScope(true);

            lastRightClickTime = -999f; // 3번클릭방지
        }
        else
        {
            lastRightClickTime = now;
        }
    }

    private void UpdateScopeHoldRelease()
    {
        if (!scopeHoldActive) return;
        if (Mouse.current == null) return;

        if(!Mouse.current.rightButton.isPressed)
        {
            scopeHoldActive = false;
            SetScope(false);
        }
    }

    private void SetScope(bool on)
    {
        isScoped = !isScoped;

        if (camComponent == null) return;

        float scpedFov = defaltFov / Mathf.Max(zoomMutiplier, 1f);
        targetFov = isScoped ? scpedFov : defaltFov;
    }

    private void UpdateCameraFov()
    {
        if (camComponent == null) return;

        camComponent.fieldOfView = Mathf.Lerp(
            camComponent.fieldOfView,
            targetFov,
            Time.deltaTime * zoomSmooth
            );
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 look = (inputSource != null) ? inputSource.Look : Vector2.zero;

        float sen = mouseSensitivity * (isScoped ? scopedSensitivityMultiplier : 1f);

        yaw += look.x * mouseSensitivity;
        pitch -= look.y * mouseSensitivity;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void FollowTarget()
    {
        if (target == null) return;

        bool aim = inputSource != null && inputSource.AimHeld;

        Vector3 targetOffset = isScoped ? scopeOffset :
            (aim ? aimOffset : normalOffset);


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