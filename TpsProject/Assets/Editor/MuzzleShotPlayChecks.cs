using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

[InitializeOnLoad]
public static class MuzzleShotPlayChecks
{
    const string Active = "MuzzleShotPlayChecks.Active";
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
    static MuzzleShotPlayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Active, false)) return;
            new GameObject("Weapon scope runner").AddComponent<MuzzleShotPlayRunner>().Begin(Checks());

        };
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new Exception("Isolated batch only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/MuzzleShotBootstrap.unity");
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
    static void Fire(ThirdPersonShooter shooter, WeaponRuntime runtime, WeaponData data)
    {
        if (runtime.AmmoInMag == 0) runtime.ResetAmmo(data, data.magazineSize + 2);
        runtime.SetNextFireTime(Time.time - 100f, data.fireRate);
        Invoke(shooter, "TryShoot");
    }
    static GameObject Wall(Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.transform.position = position; wall.transform.localScale = scale; Physics.SyncTransforms(); return wall;
    }
    static IEnumerator Checks()
    {
        Time.timeScale = 1f;
        var camera = new GameObject("Muzzle test camera").AddComponent<Camera>(); camera.gameObject.AddComponent<AudioListener>();
        camera.transform.position = new Vector3(0,1.5f,-4);
        var root = new GameObject("Muzzle test player"); root.SetActive(false);
        var self = root.AddComponent<BoxCollider>(); self.center = Vector3.up; self.size = new Vector3(1,2,1);
        var vfx = root.AddComponent<WeaponVFX>();
        var muzzle = new GameObject("Muzzle"); muzzle.transform.SetParent(root.transform, false);
        muzzle.transform.position = new Vector3(0.8f,1.2f,0.7f); muzzle.AddComponent<SphereCollider>().radius = 0.05f;
        Set(vfx, "muzzle", muzzle.transform);
        var shooter = root.AddComponent<ThirdPersonShooter>();
        var rifle = ScriptableObject.CreateInstance<WeaponData>();
        var shotgun = UnityEngine.Object.Instantiate(Resources.Load<WeaponData>("ShotgunWeaponData"));
        var sniper = UnityEngine.Object.Instantiate(Resources.Load<WeaponData>("SniperWeaponData"));
        WeaponData[] weapons = { rifle, shotgun, sniper };
        foreach (var data in weapons) { data.hipSpread = data.shoulderSpread = data.scopeSpread = 0f; data.fireClip = data.reloadClip = null; }
        Set(shooter, "weaponData", rifle); Set(shooter, "shotgunData", shotgun); Set(shooter, "sniperData", sniper); Set(shooter, "shooterCamera", camera);
        root.SetActive(true);
        var loadout = root.GetComponent<PlayerLoadout>(); loadout.UnlockWeapon(2); loadout.UnlockWeapon(3); loadout.UnlockWeapon(4);
        WeaponRuntime[] runtimes = { (WeaponRuntime)Get(shooter, "runtime"), (WeaponRuntime)Get(shooter, "shotgunRuntime"), (WeaponRuntime)Get(shooter, "sniperRuntime") };
        var target = new GameObject("Centered target"); target.transform.position = new Vector3(0,1.5f,12);
        target.AddComponent<BoxCollider>().size = Vector3.one * 0.4f;
        var health = target.AddComponent<EnemyHealth>(); health.maxHealth = health.currentHealth = 10000f; health.regenPerSecond = 0;
        Physics.SyncTransforms();
        for (int i = 0; i < 3; i++)
        {
            loadout.TrySelectSlot(i + 2); var data = weapons[i]; var runtime = runtimes[i];
            float damage = data.damage * data.pelletCount;
            float before = health.currentHealth; int ammoBefore = shooter.AmmoInMag;
            Fire(shooter, runtime, data);
            Require(Mathf.Abs(health.currentHealth - (before - damage)) < 0.01f, "Off-center muzzle failed to converge on center target.");
            Require(shooter.AmmoInMag == ammoBefore - 1, "One shot consumed incorrect ammunition.");
            var tracer = (LineRenderer)Get(vfx, "tracer");
            Require(Vector3.Distance(tracer.GetPosition(0), muzzle.transform.position) < 0.001f, "Hip/shoulder tracer did not start at physical muzzle.");
            foreach (LineRenderer line in root.GetComponentsInChildren<LineRenderer>())
                if (line.enabled) Require(Vector3.Distance(line.GetPosition(0), muzzle.transform.position) < 0.001f, "Hip/shoulder pellet tracer origin differs from muzzle.");

            // This wall blocks the muzzle's ray but leaves the camera center ray clear.
            var wall = Wall(new Vector3(0.75f,1.25f,1.2f), new Vector3(0.6f,0.8f,0.15f));
            Require(!Physics.Raycast(camera.ViewportPointToRay(new Vector3(0.5f,0.5f,0)), out RaycastHit cameraHit, 5f) || cameraHit.collider != wall.GetComponent<Collider>(), "Wall fixture unexpectedly blocks camera.");
            before = health.currentHealth; Fire(shooter, runtime, data);
            Require(health.currentHealth == before && tracer.GetPosition(1).z < 1.3f, "Hip/shoulder shot passed through muzzle-side cover.");
            UnityEngine.Object.Destroy(wall); yield return null;

            // A clipped physical muzzle must not fire through its containing wall.
            wall = Wall(muzzle.transform.position, Vector3.one * 0.3f);
            before = health.currentHealth; Fire(shooter, runtime, data);
            Require(health.currentHealth == before && Vector3.Distance(tracer.GetPosition(0), tracer.GetPosition(1)) < 0.001f, "Clipped muzzle fired through containing wall.");
            UnityEngine.Object.Destroy(wall); yield return null;
            foreach (Collider retained in (Collider[])Get(shooter, "muzzleOverlapBuffer"))
                Require(ReferenceEquals(retained, null), "Overlap buffer retained collider references.");

            // An obstruction behind the gun must not redirect a muzzle-origin shot backward.
            wall = Wall(new Vector3(0,1.5f,-1), new Vector3(1,0.8f,0.3f));
            before = health.currentHealth; Fire(shooter, runtime, data);
            Require(Mathf.Abs(health.currentHealth - (before - damage)) < 0.01f, "Camera obstruction behind muzzle redirected hip/shoulder shot backward.");
            UnityEngine.Object.Destroy(wall); yield return null;
            Debug.Log("[MuzzleShot] PASS slot " + (i + 2) + ": hip/shoulder muzzle origin, convergence, cover, clipping, and one-round consumption.");
        }
        loadout.TrySelectSlot(2);
        target.transform.position = new Vector3(0,1.5f,2);
        muzzle.transform.localPosition = new Vector3(-0.8f,1.25f,0.7f);
        Physics.SyncTransforms(); float previous = health.currentHealth; Fire(shooter, runtimes[0], rifle);
        Require(Mathf.Abs(health.currentHealth - (previous - rifle.damage)) < 0.01f, "Close target convergence failed after moving muzzle to opposite side.");
        root.transform.rotation = Quaternion.Euler(0,0,12); Physics.SyncTransforms();
        previous = health.currentHealth; Fire(shooter, runtimes[0], rifle);
        Require(Mathf.Abs(health.currentHealth - (previous - rifle.damage)) < 0.01f, "Moved/rotated muzzle no longer hits centered target.");
        var lineNow = (LineRenderer)Get(vfx, "tracer");
        Require(Vector3.Distance(lineNow.GetPosition(0), muzzle.transform.position) < 0.001f, "Rotated muzzle tracer is stale.");
        target.SetActive(false); Physics.SyncTransforms(); Fire(shooter, runtimes[0], rifle);
        Require(Mathf.Abs(Vector3.Distance(lineNow.GetPosition(0), lineNow.GetPosition(1)) - rifle.range) < 0.01f, "Empty-space shot range must be measured from muzzle.");
        Debug.Log("[MuzzleShot] PASS: hip/shoulder shots use the moved/rotated muzzle and muzzle-based weapon range.");

        var cameraController = camera.gameObject.AddComponent<ThirdPersonCamera>();
        FieldInfo aimStateField = typeof(ThirdPersonCamera).GetField("aimState", Private);
        aimStateField.SetValue(cameraController, Enum.Parse(aimStateField.FieldType, "Scope"));
        Vector3 cameraCenterOrigin = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)).origin;
        target.SetActive(true); Physics.SyncTransforms();
        previous = health.currentHealth; Fire(shooter, runtimes[0], rifle);
        Require(Mathf.Abs(health.currentHealth - (previous - rifle.damage)) < 0.01f, "Scoped camera-center shot missed centered target.");
        Require(Vector3.Distance(lineNow.GetPosition(0), cameraCenterOrigin) < 0.001f, "Scoped tracer did not start at camera center.");

        var scopedWall = Wall(muzzle.transform.position, Vector3.one * 0.3f);
        previous = health.currentHealth; Fire(shooter, runtimes[0], rifle);
        Require(Mathf.Abs(health.currentHealth - (previous - rifle.damage)) < 0.01f, "Visual muzzle clipping blocked scoped camera-center shot.");
        UnityEngine.Object.Destroy(scopedWall); yield return null;

        scopedWall = Wall(new Vector3(0,1.5f,-1), new Vector3(1,0.8f,0.3f));
        previous = health.currentHealth; Fire(shooter, runtimes[0], rifle);
        Require(Mathf.Abs(health.currentHealth - previous) < 0.01f && lineNow.GetPosition(1).z < -0.8f, "Camera-side obstruction did not block scoped shot.");
        UnityEngine.Object.Destroy(scopedWall); yield return null;
        Debug.Log("[MuzzleShot] PASS: scoped shots originate at camera center and ignore visual muzzle position.");

        UnityEngine.Object.Destroy(root); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(camera.gameObject);
        foreach (var data in weapons) UnityEngine.Object.Destroy(data);
        Debug.Log("[MuzzleShot] COMPLETE: all hybrid muzzle/scope gameplay checks passed.");
    }
}
public class MuzzleShotPlayRunner : MonoBehaviour
{
    public void Begin(IEnumerator checks) { StartCoroutine(Run(checks)); }
    IEnumerator Run(IEnumerator checks)
    {
        yield return null;
        while (true)
        {
            bool more;
            try { more = checks.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); SessionState.SetBool("MuzzleShotPlayChecks.Active", false); EditorApplication.Exit(1); yield break; }
            if (!more) break;
            yield return checks.Current;
        }
        SessionState.SetBool("MuzzleShotPlayChecks.Active", false); EditorApplication.Exit(0);
    }
}
