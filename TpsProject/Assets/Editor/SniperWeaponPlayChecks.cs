using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class SniperWeaponPlayChecks
{
    const string Active = "SniperWeaponPlayChecks.Active";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator flow;
    static int lastFrame = -1;
    static double deadline;
    static object Get(object o, string field) => o.GetType().GetField(field, Private).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    static object Invoke(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    static void Require(bool c, string message) { if (!c) throw new Exception(message); }
    static int Subscribers()
    {
        var field = typeof(ThirdPersonShooter).GetField("ShotFired", BindingFlags.Static | BindingFlags.NonPublic);
        return (field.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
    }
    static int ActiveCount() => ((IList)typeof(EnemyAI).GetField("ActiveEnemies", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Count;
    static SniperWeaponPlayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            new GameObject("Weapon scope runner").AddComponent<SniperWeaponPlayRunner>().Begin(Checks());

        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Isolated batch only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/SniperWeaponBootstrap.unity");
        SessionState.SetBool(Active, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException();
            if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount;
            if (flow.MoveNext()) return;
            End(0);
        }
        catch (Exception e) { Debug.LogException(e); End(1); }
    }
    static void End(int code)
    {
        EditorApplication.update -= Tick; SessionState.SetBool(Active, false); EditorApplication.Exit(code);
    }
    static void Button(UnityEngine.InputSystem.Mouse mouse, bool held)
    {
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, held
            ? new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right)
            : new UnityEngine.InputSystem.LowLevel.MouseState());
        UnityEngine.InputSystem.InputSystem.Update();
    }
    static void Click(ThirdPersonCamera view, UnityEngine.InputSystem.Mouse mouse, bool held)
    {
        Button(mouse, held); Invoke(view, "UpdateAimScopeState");
    }
    static IEnumerator Checks()
    {
        Time.timeScale = 1f;
        UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        UnityEngine.InputSystem.InputSystem.settings.updateMode = UnityEngine.InputSystem.InputSettings.UpdateMode.ProcessEventsManually;
        UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
        var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        var rig = new GameObject("Sniper camera fixture"); rig.SetActive(false);
        var camera = new GameObject("Camera").AddComponent<Camera>(); camera.fieldOfView = 60f;
        camera.transform.SetParent(rig.transform, false); camera.transform.position = Vector3.up;
        var view = rig.AddComponent<ThirdPersonCamera>(); Set(view, "autoFindPlayer", false);
        var player = new GameObject("Sniper player"); player.SetActive(false);
        var input = player.AddComponent<ThirdPersonInput>();
        var shooter = player.AddComponent<ThirdPersonShooter>();
        var rifleData = ScriptableObject.CreateInstance<WeaponData>();
        Set(shooter, "weaponData", rifleData); Set(shooter, "shooterCamera", camera);
        player.SetActive(true); Set(view, "target", player.transform); rig.SetActive(true); view.enabled = false;
        var loadout = player.GetComponent<PlayerLoadout>(); var skills = player.GetComponent<PlayerSkills>();
        Require(loadout != null && !loadout.IsSlotUnlocked(4) && !loadout.TrySelectSlot(4), "New run must start with sniper locked.");
        Require(Array.FindAll(skills.RollOffers(), PlayerSkills.IsWeaponUnlock).Length == 1 && skills.CanAcquire(PlayerSkill.SniperUnlock),
            "Reward must offer one weapon while keeping sniper eligible for unlock.");
        Require(skills.Acquire(PlayerSkill.SniperUnlock) && loadout.IsSniperEquipped && shooter.IsGunEquipped, "Sniper reward did not unlock/equip slot 4.");
        Require(!skills.CanAcquire(PlayerSkill.SniperUnlock) && Array.IndexOf(skills.RollOffers(), PlayerSkill.SniperUnlock) < 0, "Unlocked sniper can be offered twice.");
        bool saved = false; foreach (var record in skills.CreateScoreSnapshot()) saved |= record.id == "SniperUnlock" && record.level == 1;
        Require(saved && skills.ActiveSummary().Contains("SNIPER"), "Sniper unlock absent from score snapshot or summary.");
        Require(shooter.AmmoInMag == 1 && shooter.ReserveAmmo == 9, "Starting sniper ammo is not 1 + 9.");
        var data = (WeaponData)Get(shooter, "sniperData");
        Require(data == Resources.Load<WeaponData>("SniperWeaponData") && data.damage == 100f && data.magazineSize == 1 && data.pelletCount == 1 && data.reloadTime == 2.5f,
            "Sniper resource/configuration incorrect.");
        skills.Acquire(PlayerSkill.RifleUnlock); skills.Acquire(PlayerSkill.ShotgunUnlock); loadout.TrySelectSlot(2);
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Digit4));
        UnityEngine.InputSystem.InputSystem.Update(); Invoke(input, "Update"); Invoke(loadout, "Update");
        Require(loadout.IsSniperEquipped, "Numeric 4 input did not select sniper.");
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
        UnityEngine.InputSystem.InputSystem.Update(); Invoke(input, "Update");
        var bar = player.GetComponent<PlayerWeaponBarUI>(); Invoke(bar, "LateUpdate");
        Require(((UnityEngine.UI.Text[])Get(bar, "slotNames"))[3].text == "SNIPER" && ((UnityEngine.UI.Text)Get(bar, "ammo")).text == "1 / 9", "Sniper slot/ammo UI incorrect.");
        Debug.Log("[Sniper] PASS: reward, numeric slot 4, UI, score snapshot and 1 + 9 starting ammo.");

        Click(view, mouse, true); Click(view, mouse, false); Click(view, mouse, true); Invoke(view, "UpdateCameraFov");
        Require(view.IsScoped && (float)Get(view, "targetFov") == 10f, "Sniper scope is not 6x.");
        loadout.TrySelectSlot(2); Invoke(view, "UpdateCameraFov");
        Require(view.IsScoped && (float)Get(view, "targetFov") == 30f, "Scoped rifle swap failed to restore 2x.");
        loadout.TrySelectSlot(4); Invoke(view, "UpdateCameraFov");
        Require((float)Get(view, "targetFov") == 10f, "Scoped sniper swap failed to restore 6x.");
        loadout.TrySelectSlot(3); Invoke(view, "UpdateAimScopeState"); Require(!view.IsScoped, "Shotgun gained scope.");
        loadout.TrySelectSlot(4); Button(mouse, true); Invoke(view, "UpdateAimScopeState");
        Click(view, mouse, false); Click(view, mouse, true); Click(view, mouse, false); Click(view, mouse, true);
        Require(view.IsScoped, "Sniper scope re-entry failed.");
        Invoke(shooter, "ToggleFireMode"); Require(shooter.CurrentFireMode == WeaponData.FireMode.Single, "Sniper changed to automatic fire.");
        Debug.Log("[Sniper] PASS: sniper 6x, rifle 2x, shotgun scope block, and single-fire lock.");

        var target = new GameObject("Sniper damage target"); target.transform.position = new Vector3(0,1,10);
        var collider = target.AddComponent<BoxCollider>(); collider.size = new Vector3(3,3,1);
        var health = target.AddComponent<EnemyHealth>(); health.maxHealth = health.currentHealth = 2000f; health.regenPerSecond = 0f;
        Physics.SyncTransforms(); int shots = 0;
        Action<ThirdPersonShooter, Vector3> listener = (owner, origin) => { if (owner == shooter) shots++; };
        ThirdPersonShooter.ShotFired += listener;
        Time.timeScale = 10f;
        for (int i = 0; i < 10; i++)
        {
            Invoke(shooter, "TryShoot");
            Require(shots == i + 1 && shooter.AmmoInMag == 0 && shooter.ReserveAmmo == 9 - i, "Shot count/ammo consumption incorrect.");
            Require(Mathf.Abs(health.currentHealth - (2000f - 100f * (i + 1))) < 0.01f, "Shot did not deal exactly 100 base damage.");
            Invoke(shooter, "TryShoot"); Require(shots == i + 1, "Sniper fired more than once without reload.");
            Require(!shooter.IsReloading, "Empty sniper automatically reloaded.");
            if (i < 9)
            {
                Invoke(shooter, "TryStartReload");
                Require(shooter.IsReloading, "Manual sniper reload did not start.");
                yield return new WaitForSeconds(2.6f);
                Require(!shooter.IsReloading && shooter.AmmoInMag == 1 && shooter.ReserveAmmo == 8 - i, "Reload did not load exactly one round.");
                Set(input, "<FireHeld>k__BackingField", true); Set(input, "<FirePressed>k__BackingField", false);
                Invoke(shooter, "Update"); Require(shots == i + 1, "Holding fire shot again after reload.");
            }
        }
        Require(shooter.AmmoInMag + shooter.ReserveAmmo == 0 && !shooter.IsReloading, "Total ammo did not exhaust after ten shots.");
        Invoke(shooter, "TryStartReload"); Invoke(shooter, "TryShoot"); Require(shots == 10, "Empty sniper fired or regenerated ammunition.");
        Debug.Log("[Sniper] PASS: ten 100-damage single-projectile shots exhaust ten rounds; each intervening reload takes time and loads one round; held fire does not repeat.");

        shooter.ResetAmmoForStage(); Invoke(shooter, "TryShoot"); Invoke(shooter, "TryStartReload");
        Require(shooter.IsReloading, "Switch-cancel fixture did not reload.");
        loadout.TrySelectSlot(2); yield return new WaitForSeconds(2.6f); loadout.TrySelectSlot(4);
        Require(!shooter.IsReloading && shooter.AmmoInMag == 0 && shooter.ReserveAmmo == 9, "Switched-away sniper reload completed in background.");
        Invoke(shooter, "TryStartReload"); shooter.enabled = false;
        yield return new WaitForSeconds(2.6f);
        Require(!shooter.IsReloading && shooter.AmmoInMag == 0 && shooter.ReserveAmmo == 9, "Disabled shooter retained reload.");
        shooter.enabled = true; shooter.RecoverAmmo(100); Require(shooter.ReserveAmmo == 10, "Ammo recovery exceeded ten-round capacity.");
        for (int i = 0; i < 5; i++) { shooter.ResetAmmoForStage(); Require(shooter.AmmoInMag == 1 && shooter.ReserveAmmo == 9, "Stage refill accumulated sniper ammunition."); }
        loadout.TrySelectSlot(2); Require(shooter.AmmoInMag + shooter.ReserveAmmo == 90, "Rifle ammo changed.");
        loadout.TrySelectSlot(3); Require(shooter.AmmoInMag + shooter.ReserveAmmo == 32, "Shotgun ammo changed.");
        ThirdPersonShooter.ShotFired -= listener;
        Debug.Log("[Sniper] PASS: reload cancellation on weapon switch/disable, ammo recovery cap, repeated stage refill, and separate rifle/shotgun ammo.");
        Time.timeScale = 1f; Button(mouse, false);
        UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse); UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
        UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(player); UnityEngine.Object.Destroy(rig); UnityEngine.Object.Destroy(rifleData);
        yield return null; Require(data != null, "Shared sniper resource was destroyed with player.");
        Debug.Log("[Sniper] COMPLETE: all sniper gameplay checks passed.");
    }
}
public class SniperWeaponPlayRunner : MonoBehaviour
{
    public void Begin(IEnumerator checks) { StartCoroutine(Run(checks)); }
    IEnumerator Run(IEnumerator checks)
    {
        yield return null;
        while (true)
        {
            bool more;
            try { more = checks.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool("SniperWeaponPlayChecks.Active", false); EditorApplication.Exit(1); yield break; }
            if (!more) break;
            yield return checks.Current;
        }
        SessionState.SetBool("SniperWeaponPlayChecks.Active", false); EditorApplication.Exit(0);
    }
}
