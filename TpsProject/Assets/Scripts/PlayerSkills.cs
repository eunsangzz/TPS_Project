using System.Collections.Generic;
using UnityEngine;

public enum PlayerSkill
{
    PowerRounds, PiercingRounds, HeavyStrike, WideSwing, AmmoRecovery, Toughness,
    FirstAid, Vitality, Supply, RifleUnlock, ShotgunUnlock, SniperUnlock
}

[DisallowMultipleComponent]
public class PlayerSkills : MonoBehaviour
{
    private readonly int[] levels = new int[6];
    private readonly int[] bonusSelections = new int[3];
    private bool rifleUnlocked;
    private bool shotgunUnlocked;
    private bool sniperUnlocked;
    public string RunId { get; } = System.Guid.NewGuid().ToString("N");
    public int SelectionCount { get; private set; }
    private int nextStageAmmo;
    public float GunDamageMultiplier => 1f + 0.2f * Level(PlayerSkill.PowerRounds);
    public float MeleeDamageMultiplier => 1f + 0.25f * Level(PlayerSkill.HeavyStrike);
    public float MeleeReachMultiplier => 1f + 0.2f * Level(PlayerSkill.WideSwing);
    public bool HasPiercing => Level(PlayerSkill.PiercingRounds) > 0;
    public int AmmoPerMeleeKill => 3 * Level(PlayerSkill.AmmoRecovery);

    public int Level(PlayerSkill skill) => skill switch
    {
        PlayerSkill.RifleUnlock => rifleUnlocked ? 1 : 0,
        PlayerSkill.ShotgunUnlock => shotgunUnlocked ? 1 : 0,
        PlayerSkill.SniperUnlock => sniperUnlocked ? 1 : 0,
        _ => (int)skill >= 0 && (int)skill < levels.Length ? levels[(int)skill] : 0
    };
    public static int MaxLevel(PlayerSkill skill) =>
        skill == PlayerSkill.PiercingRounds || IsWeaponUnlock(skill) ? 1 : (int)skill < 6 ? 3 : int.MaxValue;
    public bool CanAcquire(PlayerSkill skill) => EnumIsDefined(skill) && Level(skill) < MaxLevel(skill);

    public bool Acquire(PlayerSkill skill)
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (!CanAcquire(skill) || (health != null && health.IsDead)) return false;
        if ((int)skill < levels.Length) levels[(int)skill]++;
        else if ((int)skill <= (int)PlayerSkill.Supply) bonusSelections[(int)skill - levels.Length]++;
        else if (skill == PlayerSkill.RifleUnlock) rifleUnlocked = true;
        else if (skill == PlayerSkill.ShotgunUnlock) shotgunUnlocked = true;
        else if (skill == PlayerSkill.SniperUnlock) sniperUnlocked = true;
        SelectionCount++;
        switch (skill)
        {
            case PlayerSkill.Toughness: health?.IncreaseMaxHealth(20f); break;
            case PlayerSkill.FirstAid: health?.Heal(40f); break;
            case PlayerSkill.Vitality: health?.IncreaseMaxHealth(5f); break;
            case PlayerSkill.Supply: nextStageAmmo += 15; break;
            case PlayerSkill.RifleUnlock: GetComponent<PlayerLoadout>()?.UnlockWeapon(2); break;
            case PlayerSkill.ShotgunUnlock: GetComponent<PlayerLoadout>()?.UnlockWeapon(3); break;
            case PlayerSkill.SniperUnlock: GetComponent<PlayerLoadout>()?.UnlockWeapon(4); break;
        }
        return true;
    }

    public PlayerSkill[] RollOffers()
    {
        var offers = new List<PlayerSkill>();
        // Offer one random locked weapon, leaving the other choices for upgrades/supplies.
        var lockedWeapons = new List<PlayerSkill>();
        if (!rifleUnlocked) lockedWeapons.Add(PlayerSkill.RifleUnlock);
        if (!shotgunUnlocked) lockedWeapons.Add(PlayerSkill.ShotgunUnlock);
        if (!sniperUnlocked) lockedWeapons.Add(PlayerSkill.SniperUnlock);
        if (lockedWeapons.Count > 0)
            offers.Add(lockedWeapons[Random.Range(0, lockedWeapons.Count)]);

        var available = new List<PlayerSkill>();
        for (int i = 0; i < levels.Length; i++)
            if (CanAcquire((PlayerSkill)i)) available.Add((PlayerSkill)i);
        Shuffle(available);
        foreach (PlayerSkill skill in available)
        {
            if (offers.Count == 3) break;
            offers.Add(skill);
        }
        // Repeatable supplies keep three distinct choices after permanent upgrades are capped.
        if (offers.Count < 3)
        {
            var supplies = new List<PlayerSkill> { PlayerSkill.FirstAid, PlayerSkill.Vitality, PlayerSkill.Supply };
            Shuffle(supplies);
            foreach (PlayerSkill supply in supplies)
            {
                if (offers.Count == 3) break;
                if (!offers.Contains(supply)) offers.Add(supply);
            }
        }
        Shuffle(offers);
        return offers.ToArray();
    }

    private static void Shuffle(List<PlayerSkill> values)
    {
        for (int i = values.Count - 1; i > 0; i--)
        {
            int other = Random.Range(0, i + 1);
            (values[i], values[other]) = (values[other], values[i]);
        }
    }

    public int ConsumeStageAmmoBonus()
    {
        int amount = nextStageAmmo;
        nextStageAmmo = 0;
        return amount;
    }

    public void OnMeleeKill() => GetComponent<ThirdPersonShooter>()?.RecoverAmmo(AmmoPerMeleeKill);

    public ScoreSkillRecord[] CreateScoreSnapshot()
    {
        var snapshot = new List<ScoreSkillRecord>();
        for (int i = 0; i <= (int)PlayerSkill.SniperUnlock; i++)
        {
            int count = i < levels.Length ? levels[i] : i <= (int)PlayerSkill.Supply ? bonusSelections[i - levels.Length] : Level((PlayerSkill)i);
            if (count > 0) snapshot.Add(new ScoreSkillRecord { id = ((PlayerSkill)i).ToString(), level = count });
        }
        return snapshot.ToArray();
    }

    public string ActiveSummary()
    {
        var names = new List<string>();
        for (int i = 0; i < levels.Length; i++)
            if (levels[i] > 0) names.Add($"{Title((PlayerSkill)i)} {levels[i]}");
        if (rifleUnlocked) names.Add(Title(PlayerSkill.RifleUnlock));
        if (shotgunUnlocked) names.Add(Title(PlayerSkill.ShotgunUnlock));
        if (sniperUnlocked) names.Add(Title(PlayerSkill.SniperUnlock));
        return names.Count == 0 ? "NO UPGRADES YET" : string.Join("   /   ", names);
    }

    public static string Title(PlayerSkill skill) => skill switch
    {
        PlayerSkill.PowerRounds => "POWER ROUNDS",
        PlayerSkill.PiercingRounds => "PIERCING ROUNDS",
        PlayerSkill.HeavyStrike => "HEAVY STRIKE",
        PlayerSkill.WideSwing => "WIDE SWING",
        PlayerSkill.AmmoRecovery => "AMMO RECOVERY",
        PlayerSkill.Toughness => "TOUGHNESS",
        PlayerSkill.FirstAid => "FIRST AID",
        PlayerSkill.Vitality => "VITALITY",
        PlayerSkill.Supply => "SUPPLY DROP",
        PlayerSkill.RifleUnlock => "UNLOCK RIFLE",
        PlayerSkill.ShotgunUnlock => "UNLOCK SHOTGUN",
        PlayerSkill.SniperUnlock => "UNLOCK SNIPER",
        _ => "UNKNOWN"
    };

    public static string Description(PlayerSkill skill) => skill switch
    {
        PlayerSkill.PowerRounds => "+20% rifle damage",
        PlayerSkill.PiercingRounds => "Hit one extra enemy\nSecond target takes 50% damage",
        PlayerSkill.HeavyStrike => "+25% melee damage",
        PlayerSkill.WideSwing => "+20% melee range\n+20% attack angle",
        PlayerSkill.AmmoRecovery => "+3 reserve rounds per melee kill\nLimited by ammo capacity",
        PlayerSkill.Toughness => "+20 max health\nRestore 20 health",
        PlayerSkill.FirstAid => "Restore 40 health",
        PlayerSkill.Vitality => "+5 max health\nRestore 5 health",
        PlayerSkill.Supply => "+15 ammo for the next stage only",
        PlayerSkill.RifleUnlock => "Unlock slot 2\nAutomatic rifle / 90 rounds",
        PlayerSkill.ShotgunUnlock => "Unlock slot 3\n4 pellets per blast / 32 shells",
        PlayerSkill.SniperUnlock => "Unlock slot 4 / 6x scope\n100 damage / 1-shot magazine / 10 rounds",
        _ => ""
    };

    public static string Category(PlayerSkill skill) => skill switch
    {
        PlayerSkill.PowerRounds or PlayerSkill.PiercingRounds => "RIFLE",
        PlayerSkill.HeavyStrike or PlayerSkill.WideSwing => "MELEE",
        PlayerSkill.AmmoRecovery or PlayerSkill.Supply => "SUPPLY",
        PlayerSkill.RifleUnlock or PlayerSkill.ShotgunUnlock or PlayerSkill.SniperUnlock => "WEAPON",
        _ => "SURVIVAL"
    };

    public static Color Accent(PlayerSkill skill) => Category(skill) switch
    {
        "RIFLE" => new Color(0.24f, 0.83f, 0.88f),
        "MELEE" => new Color(1f, 0.72f, 0.28f),
        "SUPPLY" => new Color(0.95f, 0.48f, 0.42f),
        "WEAPON" => new Color(0.38f, 0.66f, 1f),
        _ => new Color(0.48f, 0.86f, 0.55f)
    };

    public static bool IsWeaponUnlock(PlayerSkill skill) =>
        skill == PlayerSkill.RifleUnlock || skill == PlayerSkill.ShotgunUnlock || skill == PlayerSkill.SniperUnlock;

    private static bool EnumIsDefined(PlayerSkill skill) =>
        (int)skill >= 0 && (int)skill <= (int)PlayerSkill.SniperUnlock;
}
