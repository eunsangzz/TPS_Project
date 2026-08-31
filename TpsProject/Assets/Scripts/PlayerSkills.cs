using System.Collections.Generic;
using UnityEngine;

public enum PlayerSkill { PowerRounds, PiercingRounds, HeavyStrike, WideSwing, AmmoRecovery, Toughness, FirstAid, Vitality, Supply }

[DisallowMultipleComponent]
public class PlayerSkills : MonoBehaviour
{
    private readonly int[] levels = new int[6];
    private readonly int[] bonusSelections = new int[3];
    public string RunId { get; } = System.Guid.NewGuid().ToString("N");
    public int SelectionCount { get; private set; }
    private int nextStageAmmo;
    public float GunDamageMultiplier => 1f + 0.2f * Level(PlayerSkill.PowerRounds);
    public float MeleeDamageMultiplier => 1f + 0.25f * Level(PlayerSkill.HeavyStrike);
    public float MeleeReachMultiplier => 1f + 0.2f * Level(PlayerSkill.WideSwing);
    public bool HasPiercing => Level(PlayerSkill.PiercingRounds) > 0;
    public int AmmoPerMeleeKill => 3 * Level(PlayerSkill.AmmoRecovery);

    public int Level(PlayerSkill skill) => (int)skill >= 0 && (int)skill < levels.Length ? levels[(int)skill] : 0;
    public static int MaxLevel(PlayerSkill skill) => skill == PlayerSkill.PiercingRounds ? 1 : (int)skill < 6 ? 3 : int.MaxValue;
    public bool CanAcquire(PlayerSkill skill) => (int)skill >= 0 && (int)skill <= (int)PlayerSkill.Supply && Level(skill) < MaxLevel(skill);

    public bool Acquire(PlayerSkill skill)
    {
        PlayerHealth health = GetComponent<PlayerHealth>();
        if (!CanAcquire(skill) || (health != null && health.IsDead)) return false;
        if ((int)skill < levels.Length) levels[(int)skill]++;
        else bonusSelections[(int)skill - levels.Length]++;
        SelectionCount++;
        switch (skill)
        {
            case PlayerSkill.Toughness: health?.IncreaseMaxHealth(20f); break;
            case PlayerSkill.FirstAid: health?.Heal(40f); break;
            case PlayerSkill.Vitality: health?.IncreaseMaxHealth(5f); break;
            case PlayerSkill.Supply: nextStageAmmo += 15; break;
        }
        return true;
    }

    public PlayerSkill[] RollOffers()
    {
        var available = new List<PlayerSkill>();
        for (int i = 0; i < levels.Length; i++)
            if (CanAcquire((PlayerSkill)i)) available.Add((PlayerSkill)i);
        Shuffle(available);
        // Repeatable supplies keep three distinct choices after permanent upgrades are capped.
        if (available.Count < 3)
        {
            var supplies = new List<PlayerSkill> { PlayerSkill.FirstAid, PlayerSkill.Vitality, PlayerSkill.Supply };
            Shuffle(supplies);
            foreach (PlayerSkill supply in supplies)
            {
                if (available.Count == 3) break;
                available.Add(supply);
            }
        }
        return available.GetRange(0, 3).ToArray();
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
        for (int i = 0; i < 9; i++)
        {
            int count = i < levels.Length ? levels[i] : bonusSelections[i - levels.Length];
            if (count > 0) snapshot.Add(new ScoreSkillRecord { id = ((PlayerSkill)i).ToString(), level = count });
        }
        return snapshot.ToArray();
    }

    public string ActiveSummary()
    {
        var names = new List<string>();
        for (int i = 0; i < levels.Length; i++)
            if (levels[i] > 0) names.Add($"{Title((PlayerSkill)i)} {levels[i]}");
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
        _ => "SUPPLY DROP"
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
        _ => "+15 ammo for the next stage only"
    };

    public static string Category(PlayerSkill skill) => skill switch
    {
        PlayerSkill.PowerRounds or PlayerSkill.PiercingRounds => "RIFLE",
        PlayerSkill.HeavyStrike or PlayerSkill.WideSwing => "MELEE",
        PlayerSkill.AmmoRecovery or PlayerSkill.Supply => "SUPPLY",
        _ => "SURVIVAL"
    };

    public static Color Accent(PlayerSkill skill) => Category(skill) switch
    {
        "RIFLE" => new Color(0.24f, 0.83f, 0.88f),
        "MELEE" => new Color(1f, 0.72f, 0.28f),
        "SUPPLY" => new Color(0.95f, 0.48f, 0.42f),
        _ => new Color(0.48f, 0.86f, 0.55f)
    };
}
