using System.Reflection;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(ThirdPersonCamera))]
public class ThirdPersonCameraReferenceBinder : MonoBehaviour
{
    private static readonly FieldInfo TargetField = typeof(ThirdPersonCamera).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo InputSourceField = typeof(ThirdPersonCamera).GetField("inputSource", BindingFlags.Instance | BindingFlags.NonPublic);

    private ThirdPersonCamera cameraController;

    private void Awake()
    {
        cameraController = GetComponent<ThirdPersonCamera>();
        Rebind();
    }

    private void Update()
    {
        Rebind();
    }

    private void Rebind()
    {
        if (cameraController == null) return;

        Transform target = TargetField?.GetValue(cameraController) as Transform;
        if (target == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject == null)
            {
                PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
                playerObject = playerHealth != null ? playerHealth.gameObject : null;
            }

            if (playerObject != null && playerObject.transform != transform)
            {
                target = playerObject.transform;
                TargetField?.SetValue(cameraController, target);
            }
        }

        if (target != null)
        {
            ThirdPersonInput input = InputSourceField?.GetValue(cameraController) as ThirdPersonInput;
            if (input == null)
            {
                InputSourceField?.SetValue(cameraController, target.GetComponent<ThirdPersonInput>());
            }
        }

        Camera childCamera = GetComponentInChildren<Camera>(true);
        if (childCamera != null && !childCamera.CompareTag("MainCamera"))
        {
            childCamera.tag = "MainCamera";
        }
    }
}
