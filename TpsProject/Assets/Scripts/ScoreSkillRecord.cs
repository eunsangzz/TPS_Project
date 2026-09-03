using System;
using System.Collections.Generic;

[Serializable]
public class ScoreSkillRecord
{
    public string id;
    public int level;

    public static int Revision(ScoreSkillRecord[] skills)
    {
        long count = 0;
        if (skills != null)
            foreach (ScoreSkillRecord skill in skills)
                if (skill != null) count += Math.Max(0, skill.level);
        return (int)Math.Min(int.MaxValue, count);
    }

    public static string Format(ScoreSkillRecord[] skills)
    {
        var labels = new List<string>();
        if (skills != null)
            foreach (ScoreSkillRecord record in skills)
                if (record != null && record.level > 0 && Enum.TryParse(record.id, out PlayerSkill skill) && Enum.IsDefined(typeof(PlayerSkill), skill))
                    labels.Add($"{PlayerSkills.Title(skill)} {(PlayerSkills.IsPermanentUpgrade(skill) ? "LV " : "x")}{record.level}");
        return labels.Count == 0 ? "No skill record" : string.Join(" / ", labels);
    }
}
