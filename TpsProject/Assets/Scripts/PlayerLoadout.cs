using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class PlayerLoadout : MonoBehaviour
{
    [SerializeField] private int selectedSlot = 1;
    [SerializeField] private bool rifleUnlocked;
    [SerializeField] private bool shotgunUnlocked;
    [SerializeField] private bool sniperUnlocked;
    [SerializeField] private Transform gunModel;
    private ThirdPersonInput input;
    private PlayerHealth health;
    private ThirdPersonShooter shooter;
    private PlayerMelee melee;
    private PlayerDodge dodge;
    private Renderer[] gunRenderers;
    private bool[] originalGunVisibility;

    public int SelectedSlot => selectedSlot;
    public bool IsGunEquipped => selectedSlot >= 2 && selectedSlot <= 4;
    public bool IsRifleEquipped => selectedSlot == 2;
    public bool IsShotgunEquipped => selectedSlot == 3;
    public bool IsSniperEquipped => selectedSlot == 4;
    public bool IsMeleeEquipped => selectedSlot == 1;
    public PlayerMelee Melee => melee;
    public bool IsAlive => health == null || !health.IsDead;
    public bool CanAct
    {
        get
        {
            if (dodge == null) dodge = GetComponent<PlayerDodge>();
            return isActiveAndEnabled && Time.timeScale > 0f && IsAlive && (dodge == null || !dodge.IsDodging);
        }
    }

    public void Initialize(ThirdPersonShooter owner)
    {
        shooter = owner;
        input = GetComponent<ThirdPersonInput>();
        health = GetComponent<PlayerHealth>();
        dodge = GetComponent<PlayerDodge>();
        // Every new run starts with melee only. Ranged weapons are stage rewards.
        selectedSlot = 1;
        rifleUnlocked = false;
        shotgunUnlocked = false;
        sniperUnlocked = false;
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
        if (!CanAct || !IsSlotUnlocked(slot)) return false;
        if (selectedSlot == slot) return true;
        shooter?.CancelReload();
        melee?.CancelAttack();
        selectedSlot = slot;
        RefreshModel();
        return true;
    }

    public bool IsSlotUnlocked(int slot) => slot == 1 || (slot == 2 && rifleUnlocked) ||
        (slot == 3 && shotgunUnlocked) || (slot == 4 && sniperUnlocked);

    public bool UnlockWeapon(int slot)
    {
        if (slot < 2 || slot > 4) return false;
        if (slot == 2) rifleUnlocked = true;
        else if (slot == 3) shotgunUnlocked = true;
        else sniperUnlocked = true;
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
