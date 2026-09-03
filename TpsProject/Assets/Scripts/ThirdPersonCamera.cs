using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    //카占쌨띰옙 占쏙옙占쏙옙
    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.05f;
    [SerializeField] private float pitchMin = -35f;
    [SerializeField] private float pitchMax = 70f;

    [Header("Follow")]
    [SerializeField] private float followHeight = 2.1f;

    //카占쌨띰옙 占싱듸옙占쏙옙 占썸돌 占쏙옙占쏙옙
    [Header("Distance")]
    [SerializeField] private float defaultDistance = 3.0f;
    [SerializeField] private float minDistance = 0.5f;
    [SerializeField] private float collisionRadius = 0.25f; //占쏙옙占쏙옙占쏙옙
    [SerializeField] private float distanceSmooth = 12f; //占신몌옙 占쏙옙占쏙옙占쏙옙 占쏙옙占쌈쇽옙
    [SerializeField] private LayerMask collisionMask;

    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private ThirdPersonInput inputSource;
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Offset")]
    [SerializeField] private Vector3 normalOffset = new Vector3(0f, 0f, 0f);
    [SerializeField] private Vector3 aimOffset = new Vector3(0.45f, 0.1f, 0f);
    [SerializeField] private float offsetSmooth = 10f;

    [Header("Lean / Peek")]
    [Tooltip("Horizontal camera travel at full Q/E lean, in metres. The camera stays level.")]
    [SerializeField, Min(0f)] private float leanDistance = 0.25f;
    [Tooltip("Smoothing time for peek movement and return, in seconds. Higher values move more gradually.")]
    [SerializeField, Min(0.01f)] private float leanSmoothTime = 0.25f;

    private enum AimState { Hip, Shoulder, Scope }

    [Header("Aim/Scope")]
    [SerializeField] private float scopeDoubleClickWindow = 0.25f; // 占쏙옙클占쏙옙 占쏙옙占쏙옙클占쏙옙 占쏙옙占쏙옙 占시곤옙
    [SerializeField] private float zoomMultiplier = 2f; // Rifle scope zoom
    [SerializeField, Min(1f)] private float sniperZoomMultiplier = 6f;
    [SerializeField] private float zoomSmooth = 12f;               // 占쏙옙 占쏙옙환 占쌈듸옙

    [Header("Aim Sens")]
    [SerializeField, Range(0.05f, 1f)] private float scopedSensitivityMultiplier = 0.35f;
    [SerializeField, Range(0.05f, 1f)] private float shoulderSensitivityMultiplier = 0.7f;

    [SerializeField] private Vector3 scopeOffset = new Vector3(0f, 0.05f, 0f);

    [Header("Recoil")]
    [SerializeField] private float recoilReturn = 18f;
    [SerializeField] private float recoilSnappiness = 35f;

    [SerializeField] private Vector2 hipRecoil = new Vector2(2.0f, 0.6f);
    [SerializeField] private Vector2 shoulderRecoil = new Vector2(1.2f, 0.4f);
    [SerializeField] private Vector2 scopeRecoil = new Vector2(0.5f, 0.2f);


    private AimState aimState = AimState.Hip;
    private float lastRmbPressTime = -999f;
    private bool suppressAimUntilRelease;

    public bool IsAiming => aimState != AimState.Hip;
    public bool IsScoped => aimState == AimState.Scope && CanUseScope;
    public bool CanUseScope
    {
        get
        {
            PlayerLoadout loadout = target != null ? target.GetComponent<PlayerLoadout>() : null;
            return loadout == null || loadout.IsRifleEquipped || loadout.IsSniperEquipped;
        }
    }
    public bool IsAimSuppressed => suppressAimUntilRelease;

    private float yaw;
    private float pitch;
    public float Yaw => yaw;

    private float currentDistance;
    private Vector3 currentOffset;
    private float currentLeanOffset;
    private float leanOffsetVelocity;

    private Transform cam;
    private Camera camComponent;

    private float defaultFov;
    private float targetFov;

    private float recoilPitch;
    private float recoilYaw;

    private float recoilPitchVel;
    private float recoilYawVel;

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

        ResolveReferences();
    }

    private void Start()
    {
        ResolveReferences();

        Vector3 euler = transform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;

        LockCursor(true);
    }

    private void Update()
    {
        ResolveReferences();

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            LockCursor(false);

        // 占쏙옙클占쏙옙占싹몌옙 커占쏙옙 占쏙옙占?占쏙옙占쏙옙 占쏙옙占쌜울옙)
        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
            LockCursor(true);

        UpdateAimScopeState(); 
        UpdateCameraFov();

        HandleLook();
    }

    private void LateUpdate()
    {
        ResolveReferences();

        ApplyRecoil();
        FollowTarget();
        ApplyCameraLean(Time.deltaTime);
        HandleCollision();
    }

    private void ApplyCameraLean(float deltaTime)
    {
        if (target == null)
        {
            currentLeanOffset = leanOffsetVelocity = 0f;
            return;
        }
        PlayerLean lean = target.GetComponent<PlayerLean>();
        float amount = lean != null && lean.isActiveAndEnabled ? lean.CurrentLean : 0f;
        // FollowTarget and ApplyRecoil have restored the unrolled camera frame.
        // Its right axis is horizontal even when looking up/down; do not orbit or roll.
        Vector3 right = transform.right;
        float targetOffset = amount * leanDistance;
        targetOffset *= LimitCameraLean(right * targetOffset);
        if (deltaTime > 0f)
            currentLeanOffset = Mathf.SmoothDamp(currentLeanOffset, targetOffset, ref leanOffsetVelocity,
                leanSmoothTime, Mathf.Infinity, deltaTime);

        // Clip the actual smoothed position as well if a wall moves into the path.
        // Retain the clipped position so clearing the wall resumes smoothly, without a jump.
        float fraction = LimitCameraLean(right * currentLeanOffset);
        if (fraction < 1f)
        {
            currentLeanOffset *= fraction;
            leanOffsetVelocity = 0f;
        }
        transform.position += right * currentLeanOffset;
    }

    private float LimitCameraLean(Vector3 displacement)
    {
        float fraction = LimitLeanDisplacement(transform.position, displacement);
        if (cam == null) return fraction;
        Vector3 neutralCameraPosition = transform.position - transform.forward * currentDistance;
        return Mathf.Min(fraction, LimitLeanDisplacement(neutralCameraPosition, displacement));
    }

    private float LimitLeanDisplacement(Vector3 origin, Vector3 displacement)
    {
        float distance = displacement.magnitude;
        if (distance < 0.0001f) return 1f;
        if (Physics.SphereCast(origin, collisionRadius, displacement / distance, out RaycastHit hit,
            distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            return Mathf.Clamp01((hit.distance - 0.02f) / distance);
        }
        return 1f;
    }

    private void ResolveReferences()
    {
        if (!autoFindPlayer) return;

        Transform player = FindPlayerTransform();
        if (player != null && target != player)
        {
            target = player;
        }

        if (target == null) return;

        if (inputSource == null || inputSource.gameObject != target.gameObject)
        {
            inputSource = target.GetComponent<ThirdPersonInput>();
        }

        if (transform.IsChildOf(target))
        {
            transform.SetParent(null, true);
        }
    }

    private Transform FindPlayerTransform()
    {
        GameObject playerObject = null;

        try
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            playerObject = null;
        }

        if (playerObject != null && playerObject.transform != transform)
        {
            return playerObject.transform;
        }

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null && playerHealth.transform != transform)
        {
            return playerHealth.transform;
        }

        ThirdPersonInput playerInput = FindFirstObjectByType<ThirdPersonInput>();
        if (playerInput != null && playerInput.transform != transform)
        {
            return playerInput.transform;
        }

        return null;
    }
    private void UpdateAimScopeState()
    {
        PlayerDodge dodge = target != null ? target.GetComponent<PlayerDodge>() : null;
        if (dodge != null && dodge.IsDodging)
        {
            CancelAimForDodge();
            return;
        }
        if (suppressAimUntilRelease)
        {
            if (Mouse.current == null || !Mouse.current.rightButton.isPressed)
                suppressAimUntilRelease = false;
            return;
        }
        PlayerLoadout loadout = target != null ? target.GetComponent<PlayerLoadout>() : null;
        if (Time.timeScale <= 0f || (loadout != null && !loadout.IsGunEquipped))
        {
            aimState = AimState.Hip;
            lastRmbPressTime = -999f;
            SetFovScoped(false);
            return;
        }
        if (!CanUseScope)
        {
            // Shotguns retain shoulder aim, including when switched from a scoped rifle.
            aimState = Mouse.current != null && Mouse.current.rightButton.isPressed
                ? AimState.Shoulder : AimState.Hip;
            lastRmbPressTime = -999f;
            SetFovScoped(false);
            return;
        }
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

                // 3占쏙옙타 占쏙옙占쏙옙
                lastRmbPressTime = -999f;
            }
            else
            {
                lastRmbPressTime = now;
            }
        }
    }

    public void CancelAimForDodge()
    {
        aimState = AimState.Hip;
        lastRmbPressTime = -999f;
        suppressAimUntilRelease = true;
        SetFovScoped(false);
        if (camComponent != null) camComponent.fieldOfView = defaultFov;
        currentOffset = normalOffset;
    }

    private void SetFovScoped(bool scoped)
    {
        if (camComponent == null) return;

        PlayerLoadout loadout = target != null ? target.GetComponent<PlayerLoadout>() : null;
        float multiplier = loadout != null && loadout.IsSniperEquipped ? sniperZoomMultiplier : zoomMultiplier;
        float scopedFov = defaultFov / Mathf.Max(multiplier, 1f);
        targetFov = scoped ? scopedFov : defaultFov;
    }

    private void UpdateCameraFov()
    {
        if (camComponent == null) return;

        // Refresh even while RMB is held so rifle/sniper swaps use the equipped scope.
        SetFovScoped(IsScoped);

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

        float multiplier = 
            IsScoped ? scopedSensitivityMultiplier :
            (IsAiming ? shoulderSensitivityMultiplier : 1f);

        float sens = mouseSensitivity * GameSettings.MouseSensitivity * multiplier;

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

        // Pull in immediately when the peek brings a wall into the boom's path.
        // Smooth only the return outward, otherwise the camera can briefly remain inside the wall.
        currentDistance = targetDistance < currentDistance ? targetDistance : Mathf.Lerp(
            currentDistance, targetDistance, Time.deltaTime * distanceSmooth);

        cam.localPosition = new Vector3(0f, 0f, -currentDistance);
    }

    private void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    public void AddRecoil()
    {
        Vector2 rec =
            IsScoped ? scopeRecoil :
            (IsAiming ? shoulderRecoil : hipRecoil);

        recoilPitch += rec.x;
        recoilYaw += Random.Range(-rec.y, rec.y);
    }

    private void ApplyRecoil()
    {
        float targetPitch = recoilPitch;
        float targetYaw = recoilYaw;

        recoilPitch = Mathf.SmoothDamp(recoilPitch, 0f, ref recoilPitchVel, 1f / recoilReturn);
        recoilYaw = Mathf.SmoothDamp(recoilYaw, 0f, ref recoilYawVel, 1f / recoilReturn);

        pitch -= targetPitch * Time.deltaTime * recoilSnappiness;
        yaw += targetYaw * Time.deltaTime * recoilSnappiness;

        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
}
