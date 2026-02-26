using UnityEngine;

public class CoverCameraOffset : MonoBehaviour
{
    [Header("References")]
    public CoverController cover;
    public Transform cameraRig;

    [Header("Offsets")]
    public Vector3 normalOffset = new Vector3(0f, 0f, 0f);
    public Vector3 coverOffset = new Vector3(0.25f, 0f, 0f);

    public float lerpSpeed = 8f;

    void Reset()
    {
        cameraRig = transform;
    }

    void LateUpdate()
    {
        if (cover == null || cameraRig == null) return;

        Vector3 targetLocal = normalOffset;

        if(cover.InCover)
        {
            targetLocal = coverOffset;
        }
        cameraRig.localPosition = Vector3.Lerp(cameraRig.localPosition, targetLocal, Time.deltaTime * lerpSpeed);
    }
}
