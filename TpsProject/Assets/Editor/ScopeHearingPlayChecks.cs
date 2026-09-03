using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;

// Runs inside DodgePlayRegression's isolated play-mode fixture.
public static class ScopeHearingPlayChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int passed;

    public static IEnumerator Run(GameObject player, Camera camera, Mouse mouse)
    {
        passed = 0;
        PlayerDodge dodge = player.GetComponent<PlayerDodge>();
        ThirdPersonShooter shooter = player.GetComponent<ThirdPersonShooter>();
        PlayerLoadout loadout = player.GetComponent<PlayerLoadout>();
        WeaponRuntime runtime = (WeaponRuntime)Get(shooter, "runtime");
        loadout.UnlockWeapon(2);

        GameObject rig = new GameObject("ScopeTestRig");
        rig.SetActive(false);
        camera.transform.SetParent(rig.transform, true);
        ThirdPersonCamera view = rig.AddComponent<ThirdPersonCamera>();
        Set(view, "autoFindPlayer", false);
        Set(view, "target", player.transform);
        Set(view, "inputSource", player.GetComponent<ThirdPersonInput>());
        ScopeOverlay overlay = rig.AddComponent<ScopeOverlay>();
        Set(overlay, "textureSize", 64);
        rig.SetActive(true);
        view.enabled = false; // Drive input explicitly; do not reposition the fixture's camera.
        float normalFov = camera.fieldOfView;
        Button(mouse, true); Invoke(view, "UpdateAimScopeState");
        Button(mouse, false); Invoke(view, "UpdateAimScopeState");
        Button(mouse, true); Invoke(view, "UpdateAimScopeState");
        Expect(view.IsScoped, "Double right-click did not enter scope.");
        camera.fieldOfView = normalFov / 4f;
        Set(overlay, "currentAlpha", 1f);
        Set(dodge, "nextDodgeTime", 0f);
        Expect(dodge.TryDodge(), "Scoped dodge could not start.");
        Invoke(overlay, "Update");
        Expect(!view.IsScoped && !view.IsAiming && Mathf.Approximately(camera.fieldOfView, normalFov), "Dodge failed to immediately clear aim and zoom.");
        Expect(Mathf.Approximately((float)Get(overlay, "currentAlpha"), 0f), "Scope overlay remained visible during dodge.");
        Invoke(view, "UpdateAimScopeState");
        Expect(!view.IsAiming, "Held right-click restored aim during dodge.");
        while (dodge.IsDodging) yield return null;
        Invoke(view, "UpdateAimScopeState");
        Expect(!view.IsAiming, "Held right-click restored scope after dodge.");
        Button(mouse, false); Invoke(view, "UpdateAimScopeState");
        Button(mouse, true); Invoke(view, "UpdateAimScopeState");
        Expect(view.IsAiming && !view.IsScoped, "Fresh right-click should restart shoulder aim, not old scope.");
        Button(mouse, false); Invoke(view, "UpdateAimScopeState");
        camera.transform.SetParent(null, true);
        UnityEngine.Object.Destroy(rig);
        camera.fieldOfView = normalFov;
        camera.transform.rotation = Quaternion.Euler(-80f, 0f, 0f); // Fire harmlessly into the sky.

        Vector3 origin = player.transform.position;
        EnemyAI nearby = Enemy(player, origin + Vector3.back * 7f, EnemyType.Melee, 30f);
        EnemyAI outside = Enemy(player, origin + Vector3.back * 6f + Vector3.right * 2f, EnemyType.Melee, 2f);
        EnemyAI ranged = Enemy(player, origin + Vector3.back * 8f + Vector3.left * 2f, EnemyType.Ranged, 30f);
        yield return null;
        int heardEvents = 0;
        Action<ThirdPersonShooter, Vector3> listener = (source, position) => { if (source == shooter) heardEvents++; };
        ThirdPersonShooter.ShotFired += listener;
        Fire(shooter, runtime);
        Expect(heardEvents == 1, "One rifle shot should publish one sound event.");
        Expect(nearby.IsInvestigatingGunshot && ranged.IsInvestigatingGunshot, "Nearby melee/ranged enemies did not investigate.");
        Expect(!outside.IsInvestigatingGunshot, "Enemy outside hearing radius heard the shot.");
        Expect(Vector3.Distance(nearby.LastHeardShotPosition, origin) < 0.01f, "Heard position was not the shot origin.");
        Vector3 destination = nearby.GetComponent<NavMeshAgent>().destination;
        player.GetComponent<CharacterController>().Move(Vector3.right * 1.5f);
        Invoke(nearby, "Update");
        Expect(Vector3.Distance(nearby.GetComponent<NavMeshAgent>().destination, destination) < 0.01f,
            "Enemy tracked a hidden player's new position instead of the shot location.");
        Vector3 enemyStart = nearby.transform.position;
        float until = Time.time + 0.3f;
        while (Time.time < until) yield return null;
        Expect(Vector3.Distance(enemyStart, nearby.transform.position) > 0.1f, "Hearing changed state but did not move the enemy.");
        Fire(shooter, runtime);
        Expect(Vector3.Distance(nearby.LastHeardShotPosition, player.transform.position) < 0.01f, "A new shot did not update the investigation point.");

        int before = heardEvents;
        Invoke(shooter, "TryShoot");
        Expect(heardEvents == before, "Fire cooldown generated a false gunshot.");
        while (runtime.AmmoInMag > 0) runtime.ConsumeAmmo();
        Fire(shooter, runtime);
        Expect(heardEvents == before, "Empty magazine generated a gunshot.");
        shooter.ResetAmmoForStage();
        loadout.TrySelectSlot(1);
        Fire(shooter, runtime);
        Expect(heardEvents == before, "Melee loadout generated a gunshot.");
        loadout.UnlockWeapon(3);
        Fire(shooter, (WeaponRuntime)Get(shooter, "shotgunRuntime"));
        Expect(heardEvents == before + 1, "Shotgun must publish one sound, not four pellet sounds.");

        Set(nearby, "heardShotExpiresAt", Time.time - 1f);
        Invoke(nearby, "Update");
        Expect(!nearby.IsInvestigatingGunshot, "Expired sound investigation did not return to patrol.");
        Fire(shooter, (WeaponRuntime)Get(shooter, "shotgunRuntime"));
        nearby.GetComponent<NavMeshAgent>().Warp(nearby.GetComponent<NavMeshAgent>().destination);
        Set(nearby, "gunshotSearchDuration", 0f);
        Invoke(nearby, "Update");
        Expect(!nearby.IsInvestigatingGunshot, "Enemy did not finish searching after arriving at the sound.");
        Fire(shooter, (WeaponRuntime)Get(shooter, "shotgunRuntime"));
        nearby.GetComponent<NavMeshAgent>().Warp(player.transform.position + Vector3.back * 3f);
        EnemyPerception perception = nearby.GetComponent<EnemyPerception>();
        perception.viewDistance = 100f;
        perception.horizontalViewAngleTotal = 360f;
        perception.useLineOfSight = false;
        Invoke(nearby, "Update");
        Expect(nearby.CanCurrentlySeePlayer && !nearby.IsInvestigatingGunshot, "Visual contact did not take priority over hearing.");

        ranged.enabled = false;
        Vector3 previousHeard = ranged.LastHeardShotPosition;
        player.GetComponent<CharacterController>().Move(Vector3.left);
        Fire(shooter, (WeaponRuntime)Get(shooter, "shotgunRuntime"));
        Expect(ranged.LastHeardShotPosition == previousHeard, "Disabled AI remained subscribed to gunshots.");
        outside.GetComponent<EnemyHealth>().ApplyDamage(1000f);
        Set(outside, "gunshotHearingRange", 100f);
        Fire(shooter, (WeaponRuntime)Get(shooter, "shotgunRuntime"));
        Expect(!outside.IsInvestigatingGunshot, "Dead enemy investigated gunfire.");
        ThirdPersonShooter.ShotFired -= listener;
        nearby.gameObject.SetActive(false);
        outside.gameObject.SetActive(false);
        ranged.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(nearby.gameObject);
        UnityEngine.Object.Destroy(outside.gameObject);
        UnityEngine.Object.Destroy(ranged.gameObject);
        shooter.ResetAmmoForStage();
        loadout.TrySelectSlot(2);
        camera.transform.rotation = Quaternion.identity;
        Debug.Log($"[ScopeHearingPlayChecks] PASS: {passed} checks; scope cancellation/re-entry, rifle/shotgun hearing, fixed destinations, movement, range, expiry, arrival, visibility, and lifecycle.");
    }

    private static EnemyAI Enemy(GameObject player, Vector3 position, EnemyType type, float range)
    {
        GameObject enemy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SciFiWarriorPBRHPPolyart/Prefabs/MeleeEnemy.prefab"), position, Quaternion.identity);
        enemy.SetActive(false);
        EnemyHealth health = enemy.AddComponent<EnemyHealth>();
        EnemyPerception perception = enemy.GetComponent<EnemyPerception>();
        if (perception == null) perception = enemy.AddComponent<EnemyPerception>();
        perception.viewDistance = 0f;
        perception.rangedDetectDistance = 0f;
        if (enemy.GetComponent<EnemyCombat>() == null) enemy.AddComponent<EnemyCombat>();
        if (enemy.GetComponent<EnemyTactics>() == null) enemy.AddComponent<EnemyTactics>();
        if (enemy.GetComponent<NavMeshAgent>() == null) enemy.AddComponent<NavMeshAgent>();
        EnemyAI ai = enemy.AddComponent<EnemyAI>();
        ai.player = player.transform;
        ai.health = health;
        ai.enemyType = type;
        Set(ai, "gunshotHearingRange", range);
        Set(ai, "dodgeChance", 0f);
        enemy.SetActive(true);
        return ai;
    }

    private static void Fire(ThirdPersonShooter shooter, WeaponRuntime runtime)
    {
        runtime.SetNextFireTime(Time.time - 10f, 1f);
        Invoke(shooter, "TryShoot");
    }
    private static void Button(Mouse mouse, bool held)
    {
        InputSystem.QueueStateEvent(mouse, held ? new MouseState().WithButton(MouseButton.Right) : new MouseState());
        InputSystem.Update();
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Invoke(object target, string name) => target.GetType().GetMethod(name, Private).Invoke(target, null);
    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        passed++;
    }
}
