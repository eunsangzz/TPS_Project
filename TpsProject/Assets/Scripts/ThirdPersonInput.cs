using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class ThirdPersonInput : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public Vector2 Look { get; private set; }
    public bool DodgePressed { get; private set; }
    public bool JumpPressed => DodgePressed;
    public bool SprintHeld { get; private set; }
    public bool FreeLookHeld { get; private set; }
    public bool AimHeld { get; private set; } //에임
    public bool FireHeld { get; private set; }
    public bool FirePressed { get; private set; }
    public bool ReloadPressed { get; private set; }
    public bool ToggleFireModePressed { get; private set; }
    public bool CoverPressed { get; private set; }
    public float Lean { get; private set; }
    public int WeaponSlotPressed { get; private set; }

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction dodgeAction;
    private InputAction sprintAction;
    private InputAction aimAction;
    private InputAction freeLookAction;
    private InputAction fireAction;
    private InputAction reloadAction;
    private InputAction toggleFireModeAction;
    private InputAction coverAction;
    private InputAction leanAction;
    private InputAction[] weaponSlotActions;
    private bool suppressFireUntilReleased;

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

        dodgeAction = new InputAction("Dodge", InputActionType.Button);
        dodgeAction.AddBinding("<Keyboard>/space");

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
        coverAction.AddBinding("<Keyboard>/c");
        leanAction = new InputAction("Lean", InputActionType.Value);
        leanAction.AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/q")
            .With("Positive", "<Keyboard>/e");
        weaponSlotActions = new InputAction[4];
        for (int i = 0; i < weaponSlotActions.Length; i++)
        {
            weaponSlotActions[i] = new InputAction($"WeaponSlot{i + 1}", InputActionType.Button);
            weaponSlotActions[i].AddBinding($"<Keyboard>/{i + 1}");
            weaponSlotActions[i].AddBinding($"<Keyboard>/numpad{i + 1}");
        }
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        dodgeAction.Enable();
        sprintAction.Enable();
        aimAction.Enable();
        freeLookAction.Enable();
        fireAction.Enable();
        reloadAction.Enable();
        toggleFireModeAction.Enable();
        coverAction.Enable();
        leanAction.Enable();
        foreach (InputAction slot in weaponSlotActions) slot.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        dodgeAction.Disable();
        sprintAction.Disable();
        aimAction.Disable();
        freeLookAction.Disable();
        fireAction.Disable();
        reloadAction.Disable();
        toggleFireModeAction.Disable();
        coverAction.Disable();
        leanAction.Disable();
        Lean = 0f;
        foreach (InputAction slot in weaponSlotActions) slot.Disable();
        WeaponSlotPressed = 0;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f)
        {
            Move = Look = Vector2.zero;
            Lean = 0f;
            DodgePressed = SprintHeld = FreeLookHeld = AimHeld = ReloadPressed = ToggleFireModePressed = CoverPressed = false;
            SuppressCombatInput();
            return;
        }
        Move = moveAction.ReadValue<Vector2>();
        Look = lookAction.ReadValue<Vector2>();
        DodgePressed = dodgeAction.WasPressedThisFrame();
        SprintHeld = sprintAction.IsPressed();
        AimHeld = aimAction.IsPressed();
        FreeLookHeld = freeLookAction.IsPressed();
        FireHeld = fireAction.IsPressed();
        FirePressed = fireAction.WasPressedThisFrame();
        ReloadPressed = reloadAction.WasPressedThisFrame();
        ToggleFireModePressed = toggleFireModeAction.WasPressedThisFrame();
        CoverPressed = coverAction.WasPressedThisFrame();
        Lean = leanAction.ReadValue<float>();
        WeaponSlotPressed = 0;
        for (int i = 0; i < weaponSlotActions.Length; i++)
            if (weaponSlotActions[i].WasPressedThisFrame()) WeaponSlotPressed = i + 1;
        if (suppressFireUntilReleased)
        {
            FireHeld = FirePressed = false;
            if (!fireAction.IsPressed()) suppressFireUntilReleased = false;
        }
    }

    public void SuppressCombatInput()
    {
        suppressFireUntilReleased = true;
        FireHeld = FirePressed = false;
        WeaponSlotPressed = 0;
    }

    private void OnDestroy()
    {
        moveAction?.Dispose();
        lookAction?.Dispose();
        dodgeAction?.Dispose();
        sprintAction?.Dispose();
        aimAction?.Dispose();
        freeLookAction?.Dispose();
        fireAction?.Dispose();
        reloadAction?.Dispose();
        toggleFireModeAction?.Dispose();
        coverAction?.Dispose();
        leanAction?.Dispose();
        if (weaponSlotActions != null)
            foreach (InputAction slot in weaponSlotActions) slot.Dispose();
    }
}
