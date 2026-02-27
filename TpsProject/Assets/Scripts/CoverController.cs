using UnityEngine;

[RequireComponent(typeof(CoverDetector))]
public class CoverController : MonoBehaviour
{
    [Header("Input")]
    public KeyCode toggleCoverKey = KeyCode.E;

    [Header("Movermonet Mode")]
    public bool useCharacterController = true;
    public float slideSpeed = 3.5f;
    public float snapSpeed = 14f;
    public float maxSlideCheckDistance = 1.2f;

    [Header("Stick To Wall")]
    public float stickDistance = 0.8f;
    public float rotateSpeed = 12f;

    [Header("Corner Peek")]
    public float cornerProbeOffset = 0.4f;
    public float cornerForawrdProbe = 0.8f;

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

        Vector3 toPlayer = (transform.position - detector.CoverPoint);
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.0001f)
        {
            toPlayer.Normalize();
            if (Vector3.Dot(coverNormal, toPlayer) < 0f)
                coverNormal = -coverNormal;
        }

        coverTargetPos = detector.CoverPoint + coverNormal * detector.coverOffset;

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
        Vector3 target = new Vector3(coverTargetPos.x, pos.y, coverTargetPos.z);

        if(instant)
        {
            SetPosition(target);
        }
        else
        {
            SetPosition(Vector3.Lerp(pos, target, Time.deltaTime * snapSpeed));
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

        Vector3 desired = coverRight * inputX;

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
        //엄폐시 벽향하지 않게 forward 벽 접선 방향 설정 현재 바로보는 쪽 가까운곳으로
        Vector3 tangentA = coverRight;
        Vector3 tangentB = -coverRight;

        Vector3 currentForward = transform.forward;
        Vector3 targetForward = (Vector3.Dot(currentForward, tangentA) > Vector3.Dot(currentForward, tangentB)) ? tangentA : tangentB;

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
        bool hit = Physics.SphereCast(origin, r, dir, out _, stickDistance + 0.2f, detector.coverMask, QueryTriggerInteraction.Ignore);

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
        Vector3 probeOrigin = pos + Vector3.up * detector.chestHeight + (-coverRight * cornerProbeOffset);
        return !Physics.Raycast(probeOrigin, transform.forward, cornerForawrdProbe, detector.coverMask, QueryTriggerInteraction.Ignore);

    }

    public bool CanPeekRight()
    {
        if (!InCover) return false;
        Vector3 pos = GetPosition();
        Vector3 probeOrigin = pos + Vector3.up * detector.chestHeight + (coverRight * cornerProbeOffset);
        return !Physics.Raycast(probeOrigin, transform.forward, cornerForawrdProbe, detector.coverMask, QueryTriggerInteraction.Ignore);
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
