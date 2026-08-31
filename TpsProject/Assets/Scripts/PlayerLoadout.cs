using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerLoadout : MonoBehaviour
{
    [SerializeField] private int selectedSlot = 2;
    [SerializeField] private Transform gunModel;
    private ThirdPersonInput input;
    private PlayerHealth health;
    private ThirdPersonShooter shooter;
    private PlayerMelee melee;
    private Renderer[] gunRenderers;
    private bool[] originalGunVisibility;

    public int SelectedSlot => selectedSlot;
    public bool IsGunEquipped => selectedSlot == 2;
    public bool IsMeleeEquipped => selectedSlot == 1;
    public PlayerMelee Melee => melee;
    public bool IsAlive => health == null || !health.IsDead;
    public bool CanAct => isActiveAndEnabled && Time.timeScale > 0f && IsAlive;

    public void Initialize(ThirdPersonShooter owner)
    {
        shooter = owner;
        input = GetComponent<ThirdPersonInput>();
        health = GetComponent<PlayerHealth>();
        selectedSlot = Mathf.Clamp(selectedSlot, 1, 2);
        if (gunModel == null)
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
                if (child.name == "AssaultRifle") { gunModel = child; break; }
        if (gunRenderers == null && gunModel != null)
        {
            gunRenderers = gunModel.GetComponentsInChildren<Renderer>(true);
            originalGunVisibility = new bool[gunRenderers.Length];
            for (int i = 0; i < gunRenderers.Length; i++) originalGunVisibility[i] = gunRenderers[i].enabled;
        }
        melee = GetComponent<PlayerMelee>();
        if (melee == null) melee = gameObject.AddComponent<PlayerMelee>();
        melee.Initialize(this, owner.ShooterCamera);
        PlayerWeaponBarUI bar = GetComponent<PlayerWeaponBarUI>();
        if (bar == null) bar = gameObject.AddComponent<PlayerWeaponBarUI>();
        bar.Initialize(this, owner, health);
        RefreshModel();
    }

    private void Update()
    {
        if (!CanAct || input == null) return;
        if (input.WeaponSlotPressed != 0) TrySelectSlot(input.WeaponSlotPressed);
        if (IsMeleeEquipped && input.FirePressed && melee != null) melee.TryAttack();
    }

    public bool TrySelectSlot(int slot)
    {
        // Empty slots retain the current weapon until actual weapons are assigned.
        if (!CanAct || slot < 1 || slot > 2) return false;
        if (selectedSlot == slot) return true;
        shooter?.CancelReload();
        melee?.CancelAttack();
        selectedSlot = slot;
        RefreshModel();
        return true;
    }

    private void RefreshModel()
    {
        if (gunRenderers == null) return;
        for (int i = 0; i < gunRenderers.Length; i++)
            if (gunRenderers[i] != null) gunRenderers[i].enabled = IsGunEquipped && originalGunVisibility[i];
    }

    private void OnDisable()
    {
        shooter?.CancelReload();
        melee?.CancelAttack();
    }
}
