using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CombatHUDPlayChecks
{
    internal const string Active = "CombatHUDPlayChecks.Active";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string field) => o.GetType().GetField(field, Private).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Find<T>() where T : Object => Object.FindFirstObjectByType<T>();
    static IList Popups(CombatFeedbackUI ui) => (IList)Get(ui, "active");
    static TextMeshProUGUI PopupText(object popup) => (TextMeshProUGUI)popup.GetType().GetField("Text").GetValue(popup);

    static CombatHUDPlayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Active, false))
                new GameObject("HUD check runner").AddComponent<CombatHUDCheckRunner>().Begin(Checks());
        };
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
        CombatHUDAssets.Build();
        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");
        SessionState.SetBool(Active, true);
        EditorApplication.EnterPlaymode();
    }

    static IEnumerator Checks()
    {
        yield return null;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("CombatHUDFont");
        Require(font != null && font.HasCharacters(CombatHUDAssets.Glyphs), "Korean glyphs missing.");
        var stage = Find<StageManager>();
        var status = Find<StageStatusUI>();
        var shooter = Find<ThirdPersonShooter>();
        var health = Find<PlayerHealth>();
        var hud = Find<PlayerHUD>();
        Require(stage != null && status != null && shooter != null && hud != null, "MainScene HUD integration missing.");
        Require(((TextMeshProUGUI)Get(status, "stageText")).text == "스테이지 1", "Initial stage label incorrect.");
        var enemies = (IList)Get(stage, "aliveEnemies");
        Require(((TextMeshProUGUI)Get(status, "remainingEnemiesText")).text == "남은 적 " + enemies.Count, "Stage count is not based on alive enemies.");
        health.GrantInvulnerability(600f);
        foreach (EnemyAI enemy in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            enemy.enabled = false;
            if (enemy.TryGetComponent<EnemyCombat>(out var combat)) combat.enabled = false;
        }
        var loadout = shooter.GetComponent<PlayerLoadout>();
        loadout.UnlockWeapon(2);
        var runtime = (WeaponRuntime)Get(shooter, "runtime");
        runtime.ConsumeAmmo();
        Call(shooter, "TryStartReload");
        var feedback = shooter.GetComponent<CombatFeedbackUI>();
        feedback.ShowDamage(40f, shooter.ShooterCamera.ViewportToWorldPoint(new Vector3(0.57f, 0.55f, 8f)));
        yield return null;
        Capture(shooter.ShooterCamera, "hud-1920x1080", 1920, 1080);
        Capture(shooter.ShooterCamera, "hud-1280x720", 1280, 720);
        Capture(shooter.ShooterCamera, "hud-2560x1080", 2560, 1080);
        ScopeOverlay scopeOverlay = Find<ScopeOverlay>();
        if (scopeOverlay != null)
        {
            Call(scopeOverlay, "SetRawAlpha", 1f);
            Require(((Canvas)Get(feedback, "canvas")).sortingOrder > ((Canvas)Get(scopeOverlay, "canvas")).sortingOrder, "Feedback is below scope mask.");
            Capture(shooter.ShooterCamera, "hud-scope", 1920, 1080);
            Call(scopeOverlay, "SetRawAlpha", 0f);
        }
        var dead = enemies.Cast<EnemyHealth>().ToArray();
        foreach (var enemy in dead) enemy.TakeDamage(99999f, enemy.transform.position, Vector3.forward);
        Require(((TextMeshProUGUI)Get(status, "remainingEnemiesText")).text == "남은 적 0 · 다음 스테이지 준비", "Stage clear label incorrect.");
        Capture(shooter.ShooterCamera, "hud-stage-clear", 1920, 1080);
        float until = Time.time + 5f;
        while (!stage.IsChoosingSkill && Time.time < until) yield return null;
        Require(stage.IsChoosingSkill, "Stage skill selection did not open.");
        yield return null;
        Require(!((Canvas)Get(hud, "healthCanvas")).enabled && !((Canvas)Get(status, "canvas")).enabled && !((Canvas)Get(feedback, "canvas")).enabled,
            "Combat HUD remained above skill selection.");
        yield return new WaitForSecondsRealtime(0.25f);
        Require(Find<SkillSelectionUI>().TryChoose(0), "Could not select stage reward.");
        yield return null;
        Require(stage.CurrentStage == 2 && ((TextMeshProUGUI)Get(status, "stageText")).text == "스테이지 2", "Next stage did not refresh.");
        Debug.Log("[CombatHUD] PASS: MainScene integration, stage clear/selection/advance, three resolution captures.");

        Scene previous = SceneManager.GetActiveScene();
        Scene test = SceneManager.CreateScene("Combat HUD regression");
        SceneManager.SetActiveScene(test);
        yield return SceneManager.UnloadSceneAsync(previous);
        foreach (var scope in Object.FindObjectsByType<ScopeOverlay>(FindObjectsSortMode.None)) Object.Destroy(scope.gameObject);
        GameObject staleScope = GameObject.Find("ScopeOverlayCanvas");
        if (staleScope != null) Object.Destroy(staleScope);
        Time.timeScale = 1f;

        var camera = new GameObject("Test camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0f, 1f, -4f);
        var root = new GameObject("Player"); root.SetActive(false);
        health = root.AddComponent<PlayerHealth>();
        shooter = root.AddComponent<ThirdPersonShooter>();
        var rifle = ScriptableObject.CreateInstance<WeaponData>();
        var shotgun = Object.Instantiate(Resources.Load<WeaponData>("ShotgunWeaponData"));
        var sniper = Object.Instantiate(Resources.Load<WeaponData>("SniperWeaponData"));
        foreach (var data in new[] { rifle, shotgun, sniper })
        {
            data.hipSpread = data.shoulderSpread = data.scopeSpread = 0f;
            data.reloadTime = 0.3f;
            data.fireClip = data.reloadClip = null;
        }
        rifle.damage = 40f;
        Set(shooter, "weaponData", rifle); Set(shooter, "shotgunData", shotgun); Set(shooter, "sniperData", sniper);
        Set(shooter, "shooterCamera", camera);
        root.SetActive(true);
        loadout = root.GetComponent<PlayerLoadout>();
        loadout.UnlockWeapon(2); loadout.UnlockWeapon(3); loadout.UnlockWeapon(4);
        feedback = root.GetComponent<CombatFeedbackUI>();
        var hudRoot = new GameObject("HUD", typeof(RectTransform), typeof(Canvas));
        hud = hudRoot.AddComponent<PlayerHUD>();
        yield return null;
        health.TakeDamage(75f); yield return null;
        Require(((TextMeshProUGUI)Get(hud, "healthText")).text == "체력 25 / 100", "Damage did not update HP label.");
        Require(((Image)Get(hud, "healthFill")).color == CombatHUDStyle.Danger, "25% HP is not red.");
        health.Heal(10f); yield return null;
        Require(((Image)Get(hud, "healthFill")).color == CombatHUDStyle.Accent, "Heal did not restore HP color.");
        health.IncreaseMaxHealth(20f); yield return null;
        Require(((Slider)Get(hud, "healthSlider")).maxValue == 120f, "Maximum HP change not reflected.");

        var target = new GameObject("Target", typeof(BoxCollider), typeof(EnemyHealth)).GetComponent<EnemyHealth>();
        target.transform.position = new Vector3(0f, 1f, 12f);
        target.GetComponent<BoxCollider>().size = new Vector3(4f, 4f, 1f);
        target.regenPerSecond = 0f;
        var weapons = new[] { rifle, shotgun, sniper };
        for (int i = 0; i < weapons.Length; i++)
        {
            Call(feedback, "ClearPopups");
            loadout.TrySelectSlot(i + 2);
            target.currentHealth = target.maxHealth = 10000f;
            Physics.SyncTransforms();
            Call(shooter, "ShootOnce");
            Require(Popups(feedback).Count == 1, "Expected one popup per shot/target for weapon " + i);
            Require(PopupText(Popups(feedback)[0]).text == (weapons[i].damage * weapons[i].pelletCount).ToString("0.#"), "Pellet sum/weapon damage mismatch.");
            yield return null;
        }
        Call(feedback, "ClearPopups");
        loadout.TrySelectSlot(2);
        target.currentHealth = 10f;
        Call(shooter, "ShootOnce");
        Require(PopupText(Popups(feedback)[0]).text == "40", "Overkill should display attack damage, not remaining HP.");
        Object.Destroy(target.gameObject); yield return null;
        Require(Popups(feedback).Count == 1, "Popup disappeared with dead target.");
        Call(feedback, "ClearPopups"); Call(shooter, "ShootOnce");
        Require(Popups(feedback).Count == 0, "Miss produced a damage popup.");

        root.GetComponent<PlayerSkills>().Acquire(PlayerSkill.PiercingRounds);
        var front = Target("Front", new Vector3(0f, 1f, 10f));
        var back = Target("Back", new Vector3(0f, 1f, 14f));
        Physics.SyncTransforms(); Call(shooter, "ShootOnce");
        Require(Popups(feedback).Count == 2 && Popups(feedback).Cast<object>().Select(p => PopupText(p).text).OrderBy(s => s).SequenceEqual(new[] { "20", "40" }), "Piercing damage/target separation incorrect.");
        Object.Destroy(front.gameObject); Object.Destroy(back.gameObject); yield return null;
        Call(feedback, "ClearPopups");
        var meleeTarget = Target("Melee", new Vector3(0f, 1f, 1.5f)); meleeTarget.currentHealth = 10f;
        loadout.TrySelectSlot(1);
        Set(loadout.Melee, "attackDirection", Vector3.forward);
        Physics.SyncTransforms(); Call(loadout.Melee, "ApplyHit");
        Require(Popups(feedback).Count == 1 && PopupText(Popups(feedback)[0]).text == "40", "Melee popup incorrect.");
        Object.Destroy(meleeTarget.gameObject);
        Debug.Log("[CombatHUD] PASS: health, all weapons, shotgun aggregation, overkill, misses, piercing and melee.");

        Call(feedback, "ClearPopups");
        feedback.ShowDamage(12.25f, camera.transform.position - camera.transform.forward);
        yield return null;
        Require(!PopupText(Popups(feedback)[0]).gameObject.activeSelf, "Behind-camera popup visible.");
        for (int i = 0; i < 100; i++) feedback.ShowDamage(1f, camera.transform.position + camera.transform.forward * 8f);
        Require(Popups(feedback).Count == 64, "Popup pool is unbounded.");
        yield return new WaitForSeconds(0.85f);
        Require(Popups(feedback).Count == 0, "Popups did not expire.");

        for (int i = 0; i < 3; i++)
        {
            loadout.TrySelectSlot(i + 2);
            runtime = (WeaponRuntime)Get(shooter, i == 0 ? "runtime" : i == 1 ? "shotgunRuntime" : "sniperRuntime");
            runtime.ConsumeAmmo(); Call(shooter, "TryStartReload");
            Require(shooter.IsReloading && shooter.ReloadRemaining > 0f, "Reload did not start.");
            yield return new WaitForSeconds(0.08f);
            Require(shooter.ReloadProgress > 0f && shooter.ReloadProgress < 1f, "Reload progress not synchronized.");
            float remaining = shooter.ReloadRemaining;
            Time.timeScale = 0f; yield return new WaitForSecondsRealtime(0.12f);
            Require(Mathf.Abs(remaining - shooter.ReloadRemaining) < 0.005f, "Reload progressed during pause.");
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.35f);
            Require(!shooter.IsReloading && shooter.ReloadRemaining == 0f && shooter.ReloadProgress == 0f, "Reload completion not reset.");
            runtime.ConsumeAmmo(); Call(shooter, "TryStartReload"); loadout.TrySelectSlot(1);
            Require(!shooter.IsReloading && shooter.ReloadRemaining == 0f, "Weapon switch did not cancel reload.");
        }
        loadout.TrySelectSlot(2);
        runtime = (WeaponRuntime)Get(shooter, "runtime"); runtime.ConsumeAmmo(); Call(shooter, "TryStartReload");
        shooter.ResetAmmoForStage();
        Require(!shooter.IsReloading && shooter.ReloadProgress == 0f, "Stage reset left reload active.");
        runtime.ConsumeAmmo(); Call(shooter, "TryStartReload");
        health.TakeDamage(999f); yield return null;
        Require(!shooter.IsReloading && !((Canvas)Get(feedback, "canvas")).enabled && !((Canvas)Get(hud, "healthCanvas")).enabled, "Death did not cancel/hide combat HUD.");
        Object.Destroy(root); Object.Destroy(hudRoot); yield return null;
        Require(GameObject.Find("CombatFeedbackCanvas") == null && GameObject.Find("PlayerHealthCanvas") == null, "HUD canvas leaked after owner destruction.");
        Debug.Log("[CombatHUD] PASS: popup culling/lifetime/pool, reload completion/pause/switch/reset/death, canvas cleanup.");
        Debug.Log("[CombatHUD] COMPLETE");
    }

    static EnemyHealth Target(string name, Vector3 point)
    {
        var target = new GameObject(name, typeof(BoxCollider), typeof(EnemyHealth)).GetComponent<EnemyHealth>();
        target.transform.position = point; target.currentHealth = target.maxHealth = 10000f; target.regenPerSecond = 0f;
        return target;
    }

    static void Capture(Camera camera, string name, int width, int height)
    {
        var texture = new RenderTexture(width, height, 24);
        var previousTarget = camera.targetTexture;
        var previousActive = RenderTexture.active;
        var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        var scalers = canvases.Select(c => c.GetComponent<CanvasScaler>()).ToArray();
        var modes = scalers.Select(s => s != null ? s.uiScaleMode : CanvasScaler.ScaleMode.ConstantPixelSize).ToArray();
        var scales = canvases.Select(c => c.scaleFactor).ToArray();
        camera.targetTexture = texture;
        for (int i = 0; i < canvases.Length; i++)
        {
            var canvas = canvases[i]; canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 0.5f;
            if (scalers[i] != null) { scalers[i].enabled = false; }
            canvas.scaleFactor = modes[i] == CanvasScaler.ScaleMode.ScaleWithScreenSize ? Mathf.Sqrt(width / 1920f * height / 1080f) : scales[i];
        }
        Canvas.ForceUpdateCanvases();
        camera.Render(); RenderTexture.active = texture;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Captures")); Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        foreach (var canvas in canvases) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null; }
        for (int i = 0; i < canvases.Length; i++) { canvases[i].scaleFactor = scales[i]; if (scalers[i] != null) scalers[i].enabled = true; }
        camera.targetTexture = previousTarget; RenderTexture.active = previousActive;
        Object.Destroy(image); texture.Release(); Object.Destroy(texture);
    }
}

public class CombatHUDCheckRunner : MonoBehaviour
{
    public void Begin(IEnumerator checks) { DontDestroyOnLoad(gameObject); StartCoroutine(Run(checks)); }
    IEnumerator Run(IEnumerator checks)
    {
        while (true)
        {
            bool more;
            try { more = checks.MoveNext(); }
            catch (Exception error) { Debug.LogException(error); SessionState.SetBool(CombatHUDPlayChecks.Active, false); EditorApplication.Exit(1); yield break; }
            if (!more) break;
            yield return checks.Current;
        }
        SessionState.SetBool(CombatHUDPlayChecks.Active, false); EditorApplication.Exit(0);
    }
}
