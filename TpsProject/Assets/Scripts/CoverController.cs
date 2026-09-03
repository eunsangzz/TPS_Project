using UnityEngine;

[RequireComponent(typeof(CoverDetector))]
public class CoverController : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleCoverKey = KeyCode.C;

    [Header("Movement Mode")]
    public bool useCharacterController = true;
    public float slideSpeed = 3.5f;
    public float snapSpeed = 14f;
    public float maxSlideCheckDistance = 1.2f;

    [Header("Stick To Wall")]
    public float stickDistance = 0.8f;
    public float rotateSpeed = 12f;

    [Header("Corner Peek")]
    public float cornerProbeOffset = 0.4f;
    public float cornerForwrdProbe = 0.8f;

    [Header("References")]
    public ThirdPersonInput input;
    public Transform cameraRoot;

    [Header("Debug")]
    public bool drawDebug = true;

    public bool InCover { get; private set; }
    public bool CanEnterCover => detector.HasCover;

    public Vector3 CoverNormal => coverNormal;
    public Vector3 CoverRight => coverRight;

    CoverDetector detector;
    CharacterController cc;
    Rigidbody rb;

    Vector3 coverNormal;
    Vector3 coverRight;
    Vector3 coverTargetPos;
    Vector3 coverPointAtEnter;

    Vector3 slideRight;

    public float CoverMoveInputX { get; set; }

    float autoExitBlockUntil;

    private void Awake()
    {
        detector = GetComponent<CoverDetector>();
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        if (input == null) input = GetComponent<ThirdPersonInput>();
        if (cameraRoot == null && Camera.main != null) cameraRoot = Camera.main.transform;
    }

    void Update()
    {
        if (TryGetComponent<PlayerDodge>(out var dodge) && dodge.IsDodging) return;
        if (!InCover)
        {
            Vector3 dir = GetDetectDirection();
            detector.TickDetect(dir);
        }


        if(input != null && input.CoverPressed)
        {
            if(!InCover)
            {
                if (detector.HasCover) EnterCover();
            }
            else
            {
                ExitCover();
            }
        }

        if(InCover)
        {
            SnapToCover(false);
            UpdateCoverMovement();
            UpdateCoverRotation();
            AutoExitIfNoWall();
        }
    }

    Vector3 GetDetectDirection()
    {
        Vector3 dir = transform.forward;

        if (cameraRoot != null)
        {
            dir = cameraRoot.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f) dir.Normalize();
            else dir = transform.forward;
        }

        return dir;
    }

    void EnterCover()
    {
        InCover = true;

        coverNormal = detector.CoverNormal;
        coverRight = detector.CoverRight;

        Vector3 tA = coverRight.normalized;
        Vector3 tB = (-coverRight).normalized;

        Vector3 camRight = cameraRoot != null ? cameraRoot.right : transform.right;
        camRight.y = 0f;
        if (camRight.sqrMagnitude > 0.0001f) camRight.Normalize();

        slideRight = (Vector3.Dot(tA, camRight) >= Vector3.Dot(tB, camRight)) ? tA : tB;

        Vector3 toPlayer = (transform.position - detector.CoverPoint);
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            toPlayer.Normalize();
            if (Vector3.Dot(coverNormal, toPlayer) < 0f)
                coverNormal = -coverNormal;
        }

        coverPointAtEnter = detector.CoverPoint;
        coverTargetPos = coverPointAtEnter + coverNormal * detector.coverOffset;

        Vector3 refForward = transform.forward;
        if (cameraRoot != null)
        {
            refForward = cameraRoot.forward;
            refForward.y = 0f;
            if (refForward.sqrMagnitude > 0.0001f) refForward.Normalize();
        }

        if (Vector3.Dot(coverRight, refForward) < 0f)
            coverRight = -coverRight;

        autoExitBlockUntil = Time.time + 0.25f;

        SnapToCover(true);

        Debug.Log("ENTER COVER");

    }

    void ExitCover()
    {
        Debug.Log("EXIT COVER");
        InCover = false;

    }

    void SnapToCover(bool instant)
    {
        Vector3 pos = GetPosition();

        Vector3 toWall = coverPointAtEnter - pos; // 플레이어위치 부터 벽까지
        toWall.y = 0f;

        float alongWallNormal = Vector3.Dot(toWall, -coverNormal); // 벽파고듬 확인

        float desired = detector.coverOffset + 0.05f; //벽에서 떨어질거리

        float delta = (alongWallNormal - desired); // 현재거리 이동량

        Vector3 correction = (-coverNormal) * delta; // delta값에 따리 벽으로 당기고 밀어냄

        correction.y = 0f;

        if (instant)
        {
            SetPosition(pos + correction);
        }
        else
        {
            Vector3 newPos = pos + correction;
            SetPosition(Vector3.Lerp(pos, newPos, Time.deltaTime * snapSpeed));
        }
    }

    void UpdateCoverMovement()
    {
        float inputX = 0f;

        if(input != null)
        {
            inputX = input.Move.x;
        }

        if (Mathf.Abs(inputX) < 0.001f) return;

        Vector3 desired = slideRight * inputX;

        Vector3 delta = desired.normalized * (slideSpeed * Time.deltaTime);

        Vector3 nextPos = GetPosition() + new Vector3(delta.x, 0f, delta.z);

        if(!HasWallAt(nextPos, coverNormal))
        {
            return;
        }

        Move(delta);

        if(drawDebug)
        {
            Debug.DrawRay(GetPosition() + Vector3.up * detector.chestHeight, -coverNormal * stickDistance, Color.magenta);
        }
    }

    void UpdateCoverRotation()
    {
        Vector3 targetForward = slideRight;
        targetForward.y = 0f;

        if (targetForward.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(targetForward, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotateSpeed);

    }

    void AutoExitIfNoWall()
    {
        if (Time.time < autoExitBlockUntil) return;

        Vector3 origin = transform.position + Vector3.up * detector.chestHeight;

        Vector3 dir = -coverNormal;

        float r = 0.20f;

        float checkDistance = detector.coverOffset + 0.5f;
        if (cc != null)
        {
            checkDistance += cc.radius;
        }

        bool hit = Physics.SphereCast(origin, r, dir, out _, checkDistance, detector.coverMask, QueryTriggerInteraction.Ignore);
        
        if (!hit)
            ExitCover();
    }

    bool HasWallAt(Vector3 pos, Vector3 normal)
    {
        Vector3 origin = pos + Vector3.up * detector.chestHeight;
        return Physics.Raycast(origin, -normal, out _, maxSlideCheckDistance, detector.coverMask, QueryTriggerInteraction.Ignore);

    }

    public bool CanPeekLeft()
    {
        if (!InCover) return false;
        Vector3 pos = GetPosition();
        Vector3 probeOrigin = pos + Vector3.up * detector.chestHeight + (-slideRight);
        return !Physics.Raycast(probeOrigin, transform.forward, cornerForwrdProbe, detector.coverMask, QueryTriggerInteraction.Ignore);

    }

    public bool CanPeekRight()
    {
        if (!InCover) return false;
        Vector3 pos = GetPosition();
        Vector3 probeOrigin = pos + Vector3.up * detector.chestHeight + (slideRight);
        return !Physics.Raycast(probeOrigin, transform.forward, cornerForwrdProbe, detector.coverMask, QueryTriggerInteraction.Ignore);
    }

    Vector3 GetPosition()
    {
        if (useCharacterController && cc != null) return transform.position;
        if (rb != null) return rb.position;
        return transform.position;
    }

    void SetPosition(Vector3 p)
    {
        if (useCharacterController && cc != null)
        {
            transform.position = p;
        }
        else if (rb != null)
        {
            rb.MovePosition(p);
        }
        else
        {
            transform.position = p;
        }
    }

    void Move(Vector3 delta)
    {
        if (useCharacterController && cc != null)
        {
            cc.Move(delta);
        }
        else if (rb != null)
        {
            rb.MovePosition(rb.position + delta);
        }
        else
        {
            transform.position += delta;
        }
    }

}
