using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class ScoreSkillsRegression
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated test project.");
        string id = "skill_test_" + Guid.NewGuid().ToString("N");
        string secondId = id + "_other";
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject session = new GameObject("Test session");
            GameAuthClient auth = session.AddComponent<GameAuthClient>();
            var user = new GameAuthClient.AuthUser { id = id, displayName = "Test" };
            Set(auth, "<Token>k__BackingField", "local-test-only");
            Set(auth, "<CurrentUser>k__BackingField", user);
            PlayerDataClient data = session.AddComponent<PlayerDataClient>();
            ScoreClient client = session.AddComponent<ScoreClient>();
            client.Initialize(auth, data);
            PlayerSkills first = new GameObject("First run", typeof(PlayerSkills)).GetComponent<PlayerSkills>();
            first.Acquire(PlayerSkill.LifeSteal);
            client.QueueScore(500, first);
            string saved = PlayerPrefs.GetString("PendingScoreRecord_" + id);
            Expect(saved.Contains("\"score\":500") && saved.Contains(first.RunId) && saved.Contains("LifeSteal"), "Persisted record lost score/run/skills.");
            first.Acquire(PlayerSkill.LifeSteal);
            Expect(PlayerPrefs.GetString("PendingScoreRecord_" + id) == saved, "Later upgrades mutated an earlier pending snapshot.");
            Loaded(client, id, 500, first.RunId, new[] { new ScoreSkillRecord { id = "LifeSteal", level = 1 } });
            Expect(!client.HasPendingScore, "Confirmed snapshot was not cleared.");
            client.QueueScore(500, first);
            Expect(client.HasPendingScore, "Same-run skill choice at the same score was dropped.");
            Loaded(client, id, 500, first.RunId, new[] { new ScoreSkillRecord { id = "LifeSteal", level = 1 } });
            Expect(client.HasPendingScore, "Older acknowledgement discarded newer skills.");
            ScoreClient resumed = new GameObject("Resumed score client", typeof(ScoreClient)).GetComponent<ScoreClient>();
            resumed.Initialize(auth, data);
            Loaded(resumed, id, 500, first.RunId, new[] { new ScoreSkillRecord { id = "LifeSteal", level = 1 } });
            Expect(resumed.HasPendingScore && PlayerPrefs.GetString("PendingScoreRecord_" + id).Contains("\"level\":2"), "Pending metadata did not survive client restart.");
            PlayerSkills other = new GameObject("Other run", typeof(PlayerSkills)).GetComponent<PlayerSkills>();
            other.Acquire(PlayerSkill.HeavyStrike);
            saved = PlayerPrefs.GetString("PendingScoreRecord_" + id);
            resumed.QueueScore(400, other);
            resumed.QueueScore(500, other);
            Expect(saved == PlayerPrefs.GetString("PendingScoreRecord_" + id), "Lower/tied other run replaced the best build.");
            resumed.QueueScore(600, other);
            Loaded(resumed, id, 500, first.RunId, first.CreateScoreSnapshot());
            Expect(resumed.HasPendingScore, "Higher pending score was cleared by a stale response.");
            Loaded(resumed, id, 600, other.RunId, other.CreateScoreSnapshot());
            Expect(!resumed.HasPendingScore && !PlayerPrefs.HasKey("PendingScoreRecord_" + id) && resumed.BestSkills[0].id == "HeavyStrike", "Record acknowledgement did not replace the build atomically.");
            resumed.QueueScore(700, first);
            Invoke(resumed, "HandleSignedIn", new GameAuthClient.AuthUser { id = secondId });
            Expect(!resumed.HasPendingScore && resumed.BestSkills.Length == 0, "Build leaked across accounts.");
            Invoke(resumed, "HandleSignedIn", user);
            Expect(resumed.HasPendingScore, "Switching accounts lost the original pending record.");
            first.Acquire(PlayerSkill.FirstAid);
            first.Acquire(PlayerSkill.FirstAid);
            first.Acquire(PlayerSkill.Supply);
            first.ConsumeStageAmmoBonus();
            Expect(first.CreateScoreSnapshot().Single(s => s.id == "FirstAid").level == 2 && first.CreateScoreSnapshot().Single(s => s.id == "Supply").level == 1, "Consumed bonus choices disappeared from history.");
            Expect(first.SelectionCount == ScoreSkillRecord.Revision(first.CreateScoreSnapshot()), "Snapshot revision differs from the selection counter.");
            PlayerPrefs.SetInt("PendingBestScore_" + secondId, 123);
            Invoke(resumed, "HandleSignedIn", new GameAuthClient.AuthUser { id = secondId });
            Expect(resumed.HasPendingScore, "Legacy pending score migration failed.");
            Expect(ScoreSkillRecord.Format(null) == "No skill record", "Legacy response formatting failed.");

            GameSession gameSession = new GameObject("UI session", typeof(GameSession)).GetComponent<GameSession>();
            // Edit-mode fixtures do not run Awake or support DontDestroyOnLoad.
            typeof(GameSession).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, gameSession);
            Set(gameSession, "<AuthClient>k__BackingField", auth);
            Set(gameSession, "<PlayerDataClient>k__BackingField", data);
            Set(gameSession, "<ScoreClient>k__BackingField", resumed);
            LoginScreenUI login = new GameObject("Login UI", typeof(LoginScreenUI)).GetComponent<LoginScreenUI>();
            Invoke(login, "Awake");
            var records = Enumerable.Range(0, 9).Select(i => new ScoreSkillRecord { id = ((PlayerSkill)i).ToString(), level = i == 1 ? 1 : i < 6 ? 3 : 999999 }).ToArray();
            var entries = Enumerable.Range(0, 10).Select(i => new ScoreClient.LeaderboardEntry { rank = i + 1, displayName = "Player " + i, bestScore = 10000 - i, bestSkills = i == 9 ? null : records }).ToArray();
            Invoke(login, "HandleLeaderboardLoaded", (object)entries);
            Capture(login, 1280, 720, "score-skills-login-wide.png");
            Capture(login, 390, 844, "score-skills-login-tall.png");
            Debug.Log("[ScoreSkillsRegression] PASS: immutable snapshots, same-run enrichment, stale confirmations, restart, account isolation, legacy saves, bonus history and login layout.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally
        {
            foreach (string key in new[] { id, secondId })
            {
                PlayerPrefs.DeleteKey("PendingScoreRecord_" + key);
                PlayerPrefs.DeleteKey("PendingBestScore_" + key);
            }
            PlayerPrefs.Save();
        }
    }

    private static void Loaded(ScoreClient client, string user, int score, string run, ScoreSkillRecord[] skills) =>
        Invoke(client, "HandleDataLoaded", new PlayerDataClient.PlayerData { userId = user, bestScore = score, bestRunId = run, bestSkills = skills });

    private static void Capture(LoginScreenUI login, int width, int height, string name)
    {
        Canvas canvas = login.GetComponentInChildren<Canvas>();
        Camera camera = new GameObject("UI Camera", typeof(Camera)).GetComponent<Camera>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 0.5f;
        RenderTexture target = new RenderTexture(width, height, 24);
        camera.targetTexture = target;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        for (int pass = 0; pass < 4; pass++) { Invoke(login, "Update"); Canvas.ForceUpdateCanvases(); }
        login.GetComponentInChildren<ScrollRect>().verticalNormalizedPosition = 0f;
        Canvas.ForceUpdateCanvases();
        camera.Render();
        foreach (Text text in login.GetComponentsInChildren<Text>().Where(t => t.name.StartsWith("RankSkills")))
            Expect(text.preferredHeight <= text.rectTransform.rect.height, "Skill history is clipped in login ranking.");
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        image.Apply();
        File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath, "../../", name)), image.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        UnityEngine.Object.DestroyImmediate(image);
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(camera.gameObject);
    }

    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Expect(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
