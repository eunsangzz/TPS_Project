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
    public float stickDistance = 0.4f;
    public float rotateSpeed = 12f;

    [Header("Corner Peek")]
    public float cornerProbeOffset = 0.4f;
    public float cornerForawrdProbe = 0.8f;

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

    public float CoverMoveInputX { get; set; }

    private void Awake()
    {
        detector = GetComponent<CoverDetector>();
        cc = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();

        if (useCharacterController && cc == null)
        {
            Debug.LogWarning("[CoverController] useCharacterController=true인데 CharacterController가 없습니다. Rigidbody 모드로 바꾸거나 CharacterController를 추가하세요.");
        }
        if (!useCharacterController && rb == null)
        {
            Debug.LogWarning("[CoverController] Rigidbody 모드인데 Rigidbody가 없습니다. Rigidbody를 추가하거나 CharacterController 모드로 바꾸세요.");
        }
    }

    void Update()
    {
        detector.TickDetect(transform.forward);

        if(Input.GetKeyDown(toggleCoverKey))
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
            UpdateCoverMovement();
            UpdateCoverRotation();
            AutoExitIfNoWall();
        }
    }

    void EnterCover()
    {
        InCover = true;

        coverNormal = detector.CoverNormal;
        coverRight = detector.CoverRight;
        coverTargetPos = detector.TargetPosition;

        SnapToCover(Instantiate: false);
    }

    void ExitCover()
    {
        InCover = false;
        CoverMoveInputX = 0f;

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
            Vector3 newPos = Vector3.Lerp(pos, target, Time.deltaTime * snapSpeed);
            SetPosition();
        }
    }

    void UpdateCoverMovement()
    {
        float inputX = CoverMoveInputX;
        if (Mathf.Approximately(inputX, 0f))
            inputX = Input.GetAxisRaw("Horizontal");

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

    void UpdataCoverRotation()
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
        Vector3 origin = transform.position + Vector3.up * detector.chestHeight;
        bool hit = Physics.Raycast(origin, -coverNormal, out _, stickDistance, detector.coverMask, QueryTriggerInteraction.Ignore);

        if (!hit)
        {
            ExitCover();
        }
    }

    bool HasWallAt(Vector3 pos, Vector3 normal)
    {
        Vector3 origin = pos + Vector3.up * detector.chestHeight;
        return Physics.Raycast(origin, -normal, out _, maxSlideCheckDistance, detector.coverMask, QueryTriggerInteraction.Ignore);

    }

}
