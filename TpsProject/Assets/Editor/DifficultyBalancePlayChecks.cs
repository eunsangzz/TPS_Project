using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class DifficultyBalancePlayChecks
{
    const string Active = "DifficultyBalancePlayChecks.Active";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string field) => o.GetType().GetField(field, Private).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    static object Invoke(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Near(float actual, float expected, string message) => Require(Mathf.Abs(actual - expected) < .001f, $"{message}: {actual} != {expected}");
    static DifficultyBalancePlayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("Balance checks").AddComponent<DifficultyBalancePlayRunner>().Begin(Checks());
        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Use an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/BalanceBootstrap.unity");
        SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
    }
    static IEnumerator Checks()
    {
        Time.timeScale = 1f;
        var stage = new GameObject("Stage").AddComponent<StageManager>(); stage.enabled = false;
        var meleePrefab = new GameObject("MeleeTestPrefab"); meleePrefab.SetActive(false);
        var rangedPrefab = new GameObject("RangedTestPrefab"); rangedPrefab.SetActive(false);
        Set(stage, "enemyPrefabObjects", new[] { meleePrefab, rangedPrefab });
        var enemyRoot = new GameObject("Difficulty enemy"); enemyRoot.SetActive(false);
        var agent = enemyRoot.AddComponent<NavMeshAgent>(); agent.enabled = false;
        var enemy = enemyRoot.AddComponent<EnemyAI>();
        var combat = enemyRoot.GetComponent<EnemyCombat>();
        if (combat == null) combat = enemyRoot.AddComponent<EnemyCombat>();
        foreach (int round in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 30, 6, 1 })
        {
            Set(stage, "currentStage", round);
            int total = stage.EnemyCountForStage(round), melee = stage.MeleeCountForStage(round, total);
            Require(total == (round == 1 ? 3 : round == 2 ? 5 : 7), "Enemy count progression incorrect");
            Require(melee == (round == 1 ? 3 : round == 2 ? 3 : round >= 4 && round < 6 ? 5 : round >= 6 ? 2 : 4), "Wrong composition at round " + round);
            foreach (EnemyType type in new[] { EnemyType.Melee, EnemyType.Ranged })
            {
                Require(Invoke(stage, "PickEnemyPrefabObject", type) == (type == EnemyType.Melee ? meleePrefab : rangedPrefab), "Typed prefab choice failed");
                enemy.enemyType = type; Invoke(stage, "ConfigureEnemy", enemy);
                bool boostedMelee = (round >= 4 && round < 6) || round >= 8;
                Near(enemy.meleeMoveSpeed, EnemyAI.DefaultMeleeMoveSpeed * (boostedMelee ? 1.25f : 1f), "Melee speed/reset");
                Near(combat.meleeDamage, boostedMelee ? 15 : 10, "Melee damage/reset");
                Near(combat.rangedDamage, round >= 6 ? 18 : 12, "Ranged damage/reset");
                Near(combat.rangedAttackRate, round >= 6 ? .9f : .6f, "Ranged fire rate/reset");
            }
        }
        Debug.Log("[Balance] PASS: rounds 1-9/30, counts, typed prefabs, majorities, boosts and 6/1 stat resets.");
        UnityEngine.Object.Destroy(enemyRoot);

        var rifle = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/WeaponData.asset");
        var shotgun = Resources.Load<WeaponData>("ShotgunWeaponData");
        var sniper = Resources.Load<WeaponData>("SniperWeaponData");
        Near(rifle.GetStats(0).Damage, 12, "Rifle initial damage"); Near(rifle.GetStats(3).Damage, 20, "Rifle damage cap");
        Near(rifle.GetStats(0).FireRate, 6, "Rifle initial rate"); Near(rifle.GetStats(3).FireRate, 10, "Rifle rate cap");
        Require(rifle.GetStats(0).MagazineSize == 15 && rifle.GetStats(3).MagazineSize == 30, "Rifle magazine progression");
        Near(sniper.GetStats(0).Damage, 50, "Sniper initial damage"); Near(sniper.GetStats(3).Damage, 100, "Sniper cap");
        Near(sniper.GetStats(0).ReloadTime, 4, "Sniper initial reload"); Near(sniper.GetStats(3).ReloadTime, 2.5f, "Sniper reload cap");
        for (int level = 0; level <= 3; level++)
        {
            var stats = shotgun.GetStats(level); Require(stats.PelletCount == 4 + level, "Shotgun pellet progression");
            Near(stats.Damage, 15, "Per-pellet damage");
            Near(stats.ReloadTime, 3.6f - .4f * level, "Shotgun reload progression");
            if (level > 0) Require(stats.HipSpread < shotgun.GetStats(level - 1).HipSpread && stats.ShoulderSpread < shotgun.GetStats(level - 1).ShoulderSpread, "Shotgun accuracy not improving");
        }
        Near(shotgun.GetStats(0).HipSpread, 18, "Shotgun close range spread"); Near(shotgun.GetStats(3).HipSpread, 7, "Shotgun old accuracy cap");
        Near(shotgun.GetStats(3).ShoulderSpread, 4, "Shotgun old ADS accuracy cap");

        for (int mask = 0; mask < 8; mask++)
        {
            var owner = new GameObject("Offer checks").AddComponent<PlayerSkills>();
            PlayerSkill[] unlocks = { PlayerSkill.RifleUnlock, PlayerSkill.ShotgunUnlock, PlayerSkill.SniperUnlock };
            PlayerSkill[] upgrades = { PlayerSkill.RifleUpgrade, PlayerSkill.ShotgunUpgrade, PlayerSkill.SniperUpgrade };
            for (int i = 0; i < 3; i++)
            {
                if ((mask & (1 << i)) != 0) Require(owner.Acquire(unlocks[i]), "Unlock failed");
                Require(owner.CanAcquire(upgrades[i]) == ((mask & (1 << i)) != 0), "Locked weapon offered an upgrade");
            }
            for (int capped = 0; capped < 2; capped++)
            {
                for (int roll = 0; roll < 128; roll++)
                {
                    var offers = owner.RollOffers();
                    Require(offers.Length == 3 && offers.Distinct().Count() == 3 && offers.All(owner.CanAcquire), "Reward eligibility/uniqueness");
                    Require(offers.Count(PlayerSkills.IsWeaponUnlock) == (mask == 7 ? 0 : 1), "One unlock per reward was lost");
                    Require(!offers.Contains(PlayerSkill.PowerRounds), "Legacy damage boost returned above weapon caps");
                }
                foreach (PlayerSkill skill in Enum.GetValues(typeof(PlayerSkill)))
                    if (PlayerSkills.IsPermanentUpgrade(skill)) while (owner.Acquire(skill)) { }
            }
            owner.Acquire(PlayerSkill.FirstAid); owner.Acquire(PlayerSkill.Supply);
            Require(ScoreSkillRecord.Revision(owner.CreateScoreSnapshot()) == owner.SelectionCount, "New skill snapshot revision incorrect");
            UnityEngine.Object.Destroy(owner.gameObject);
        }
        Debug.Log("[Balance] PASS: weapon baselines/caps, 2048 reward rolls, unlock gating, caps and score snapshots.");

        var camera = new GameObject("Shot camera").AddComponent<Camera>(); camera.transform.position = new Vector3(0,1.2f,-3);
        var root = new GameObject("Player"); root.SetActive(false);
        var health = root.AddComponent<PlayerHealth>();
        var shooter = root.AddComponent<ThirdPersonShooter>();
        WeaponData[] data = { UnityEngine.Object.Instantiate(rifle), UnityEngine.Object.Instantiate(shotgun), UnityEngine.Object.Instantiate(sniper) };
        foreach (var weapon in data)
        {
            weapon.hipSpread = weapon.shoulderSpread = weapon.scopeSpread = 0;
            weapon.upgradedHipSpread = weapon.upgradedShoulderSpread = weapon.upgradedScopeSpread = 0;
            weapon.fireClip = weapon.reloadClip = null;
        }
        Set(shooter, "weaponData", data[0]); Set(shooter, "shotgunData", data[1]); Set(shooter, "sniperData", data[2]); Set(shooter, "shooterCamera", camera);
        root.SetActive(true);
        var skills = root.GetComponent<PlayerSkills>(); var loadout = root.GetComponent<PlayerLoadout>();
        foreach (var unlock in new[] { PlayerSkill.RifleUnlock, PlayerSkill.ShotgunUnlock, PlayerSkill.SniperUnlock }) skills.Acquire(unlock);
        var target = new GameObject("Hit target"); target.transform.position = new Vector3(0,1.2f,4);
        target.AddComponent<BoxCollider>().size = Vector3.one;
        var targetHealth = target.AddComponent<EnemyHealth>(); targetHealth.maxHealth = targetHealth.currentHealth = 10000; targetHealth.regenPerSecond = 0;
        var duplicate = new GameObject("Duplicate collider"); duplicate.transform.SetParent(target.transform, false); duplicate.AddComponent<BoxCollider>();
        Physics.SyncTransforms(); health.TakeDamage(80);
        WeaponRuntime[] runtimes = { (WeaponRuntime)Get(shooter, "runtime"), (WeaponRuntime)Get(shooter, "shotgunRuntime"), (WeaponRuntime)Get(shooter, "sniperRuntime") };
        PlayerSkill[] weaponUpgrades = { PlayerSkill.RifleUpgrade, PlayerSkill.ShotgunUpgrade, PlayerSkill.SniperUpgrade };
        for (int level = 0; level <= 3; level++)
        {
            if (level > 0) { skills.Acquire(PlayerSkill.LifeSteal); foreach (var upgrade in weaponUpgrades) skills.Acquire(upgrade); }
            for (int i = 0; i < 3; i++)
            {
                loadout.TrySelectSlot(i + 2); shooter.ResetAmmoForStage();
                var stats = data[i].GetStats(level); float before = targetHealth.currentHealth, hp = health.CurrentHealth;
                Invoke(shooter, "TryShoot");
                Near(before - targetHealth.currentHealth, stats.Damage * stats.PelletCount, "Actual upgraded shot damage");
                Near(health.CurrentHealth - hp, level == 0 ? 0 : .5f + .5f * level, "Once-per-shot lifesteal with pellets/duplicate colliders");
                Near((float)Get(runtimes[i], "nextFireTime") - Time.time, 1f / stats.FireRate, "Upgraded fire interval");
                Require(shooter.AmmoInMag == stats.MagazineSize - 1, "Magazine size not applied at stage reset");
                Require(!shooter.IsReloading, "Automatic reload returned");
                IEnumerator reload = (IEnumerator)Invoke(shooter, "ReloadRoutine", runtimes[i], data[i]); reload.MoveNext();
                Near((float)typeof(WaitForSeconds).GetField("m_Seconds", Private).GetValue(reload.Current), stats.ReloadTime, "Upgraded reload duration");
                reload.MoveNext(); Require(shooter.AmmoInMag == stats.MagazineSize, "Upgraded magazine not used for reload");
            }
        }
        loadout.TrySelectSlot(3); shooter.ResetAmmoForStage();
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0,1.2f,2); wall.transform.localScale = new Vector3(3,3,.2f);
        Physics.SyncTransforms(); float playerBefore = health.CurrentHealth; Invoke(shooter, "TryShoot"); Near(health.CurrentHealth, playerBefore, "Wall hit granted lifesteal");
        UnityEngine.Object.Destroy(wall); yield return null;
        target.transform.position = new Vector3(0,1,1.5f); Physics.SyncTransforms(); loadout.TrySelectSlot(1);
        var meleeAttack = root.GetComponent<PlayerMelee>(); Set(meleeAttack, "attackDirection", Vector3.forward);
        playerBefore = health.CurrentHealth; Invoke(meleeAttack, "ApplyHit"); Near(health.CurrentHealth - playerBefore, 2, "Melee lifesteal or collider deduplication");
        health.Heal(999); Invoke(meleeAttack, "ApplyHit"); Near(health.CurrentHealth, health.MaxHealth, "Lifesteal exceeded max health");
        health.TakeDamage(999); skills.OnAttackHit(); Require(health.IsDead && health.CurrentHealth == 0, "Lifesteal revived dead player");
        Near(rifle.damage, 12, "Shared rifle asset mutated"); Near(sniper.damage, 50, "Shared sniper asset mutated");
        var newSkills = new GameObject("Fresh run").AddComponent<PlayerSkills>(); Require(newSkills.Level(PlayerSkill.RifleUpgrade) == 0 && newSkills.LifeStealHealing == 0, "Upgrades leaked across runs");
        UnityEngine.Object.Destroy(root); foreach (var weapon in data) UnityEngine.Object.Destroy(weapon);
        Debug.Log("[Balance] PASS: actual shots at all levels, damage/fire rate/magazines/reloads, lifesteal pellets/melee/walls/health cap/death and shared-asset isolation.");
        Debug.Log("[Balance] COMPLETE");
    }
}
public class DifficultyBalancePlayRunner : MonoBehaviour
{
    public void Begin(IEnumerator checks) => StartCoroutine(Run(checks));
    IEnumerator Run(IEnumerator checks)
    {
        yield return null;
        while (true)
        {
            bool more;
            try { more = checks.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); yield break; }
            if (!more) break;
            yield return checks.Current;
        }
        SessionState.SetBool("DifficultyBalancePlayChecks.Active", false); EditorApplication.Exit(0);
    }
}
