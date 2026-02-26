using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonInput : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool FreeLookHeld { get; private set; }
    public bool AimHeld { get; private set; } //ø°¿”
    public bool FireHeld { get; private set; }
    public bool FirePressed { get; private set; }
    public bool ReloadPressed { get; private set; }
    public bool ToggleFireModePressed { get; private set; }
    public bool CoverPressed { get; private set; }

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;
    private InputAction aimAction;
    private InputAction freeLookAction;
    private InputAction fireAction;
    private InputAction reloadAction;
    private InputAction toggleFireModeAction;
    private InputAction coverAction;

    private void Awake()
    {
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction("Look", InputActionType.Value);
        lookAction.AddBinding("<Mouse>/delta");

        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");

        sprintAction = new InputAction("Sprint", InputActionType.Button);
        sprintAction.AddBinding("<Keyboard>/leftShift");

        aimAction = new InputAction("Aim", InputActionType.Button);
        aimAction.AddBinding("<Mouse>/rightButton");

        freeLookAction = new InputAction("FreeLook", InputActionType.Button);
        freeLookAction.AddBinding("<Keyboard>/leftAlt");
        freeLookAction.AddBinding("<Keyboard>/rightAlt");

        fireAction = new InputAction("Fire", InputActionType.Button);
        fireAction.AddBinding("<Mouse>/leftButton");

        reloadAction = new InputAction("Reload", InputActionType.Button);
        reloadAction.AddBinding("<Keyboard>/r");

        toggleFireModeAction = new InputAction("ToggleFireMode", InputActionType.Button);
        toggleFireModeAction.AddBinding("<Keyboard>/v");

        coverAction = new InputAction("Cover", InputActionType.Button);
        coverAction.AddBinding("<Keyboard>/e");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
        aimAction.Enable();
        freeLookAction.Enable();
        fireAction.Enable();
        reloadAction.Enable();
        toggleFireModeAction.Enable();
        coverAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
        aimAction.Disable();
        freeLookAction.Disable();
        fireAction.Disable();
        reloadAction.Disable();
        toggleFireModeAction.Disable();
        coverAction.Disable();
    }

    private void Update()
    {
        Move = moveAction.ReadValue<Vector2>();
        Look = lookAction.ReadValue<Vector2>();
        JumpPressed = jumpAction.WasPressedThisFrame();
        SprintHeld = sprintAction.IsPressed();
        AimHeld = aimAction.IsPressed();
        FreeLookHeld = freeLookAction.IsPressed();
        FireHeld = fireAction.IsPressed();
        FirePressed = fireAction.WasPressedThisFrame();
        ReloadPressed = reloadAction.WasPressedThisFrame();
        ToggleFireModePressed = toggleFireModeAction.WasPressedThisFrame();
        CoverPressed = coverAction.WasPressedThisFrame();
    }
}
