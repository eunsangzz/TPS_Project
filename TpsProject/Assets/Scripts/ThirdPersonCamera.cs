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

    private enum AimState { Hip, Shoulder, Scope }

    [Header("Aim/Scope")]
    [SerializeField] private float scopeDoubleClickWindow = 0.25f; // 우클릭 더블클릭 판정 시간
    [SerializeField] private float zoomMultiplier = 4f;            // 4배 줌
    [SerializeField] private float zoomSmooth = 12f;               // 줌 전환 속도

    [Header("Scope Tuning")]
    [SerializeField, Range(0.05f, 1f)] private float scopedSensitivityMultiplier = 0.35f;
    [SerializeField] private Vector3 scopeOffset = new Vector3(0f, 0.05f, 0f);

    private AimState aimState = AimState.Hip;
    private float lastRmbPressTime = -999f;

    public bool IsAiming => aimState != AimState.Hip;
    public bool IsScoped => aimState == AimState.Scope;

    private float yaw;
    private float pitch;
    public float Yaw => yaw;

    private float currentDistance;
    private Vector3 currentOffset;

    private Transform cam;
    private Camera camComponent;

    private float defaultFov;
    private float targetFov;

    private void Awake()
    {
        var childCam = GetComponentInChildren<Camera>();
        camComponent = childCam;
        cam = childCam != null ? childCam.transform : null;

        currentDistance = defaultDistance;
        currentOffset = normalOffset;

        if (camComponent != null)
        {
            defaultFov = camComponent.fieldOfView;
            targetFov = defaultFov;
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
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            LockCursor(false);

        // 우클릭하면 커서 잠금(게임 조작용)
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            LockCursor(true);

        UpdateAimScopeState(); 
        UpdateCameraFov();

        HandleLook();
        FollowTarget();
        HandleCollision();
    }
    private void UpdateAimScopeState()
    {
        if (Mouse.current == null) return;

        bool rmbHeld = Mouse.current.rightButton.isPressed;

        
        if (!rmbHeld)
        {
            if (aimState != AimState.Hip)
            {
                aimState = AimState.Hip;
                SetFovScoped(false);
            }
            return;
        }

        if (aimState != AimState.Scope)
        {
            aimState = AimState.Shoulder;
            SetFovScoped(false);
        }

        if (Mouse.current.rightButton.wasPressedThisFrame)
        {
            float now = Time.time;

            if (now - lastRmbPressTime <= scopeDoubleClickWindow)
            {
                aimState = AimState.Scope;
                SetFovScoped(true);

                // 3연타 방지
                lastRmbPressTime = -999f;
            }
            else
            {
                lastRmbPressTime = now;
            }
        }
    }

    private void SetFovScoped(bool scoped)
    {
        if (camComponent == null) return;

        float scopedFov = defaultFov / Mathf.Max(zoomMultiplier, 1f);
        targetFov = scoped ? scopedFov : defaultFov;
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

        float sens = mouseSensitivity * (IsScoped ? scopedSensitivityMultiplier : 1f);

        yaw += look.x * sens;
        pitch -= look.y * sens;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void FollowTarget()
    {
        if (target == null) return;

        Vector3 targetOffset =
            IsScoped ? scopeOffset :
            (IsAiming ? aimOffset : normalOffset);

        currentOffset = Vector3.Lerp(
            currentOffset,
            targetOffset,
            Time.deltaTime * offsetSmooth
        );

        Vector3 basePos = target.position + Vector3.up * followHeight;

        transform.position = basePos
            + transform.right * currentOffset.x
            + transform.up * currentOffset.y;
    }

    private void HandleCollision()
    {
        if (cam == null) return;

        Vector3 origin = transform.position;
        Vector3 direction = -transform.forward;

        float targetDistance = defaultDistance;

        if (Physics.SphereCast(
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
            Time.deltaTime * distanceSmooth
        );

        cam.localPosition = new Vector3(0f, 0f, -currentDistance);
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}