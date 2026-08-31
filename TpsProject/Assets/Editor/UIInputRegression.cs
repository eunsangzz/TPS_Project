using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class UIInputRegression
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Requires an isolated batch project.");
        try
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            throw new InvalidOperationException("Run this regression with only the new Input System enabled.");
#endif
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject owner = new GameObject("Test UI Owner");
            EventSystem eventSystem = GameUIInput.EnsureEventSystem(owner.transform);
            Expect(eventSystem.transform.parent == owner.transform, "New EventSystem is not owned by the UI.");
            InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
            Expect(module != null && module.enabled && module.actionsAsset != null, "New input module is not configured.");
            Expect(module.point.action != null && module.leftClick.action != null && module.submit.action != null,
                "Default mouse and navigation bindings are missing.");
            Expect(GameUIInput.EnsureEventSystem(owner.transform) == eventSystem, "Created a duplicate EventSystem.");
            Expect(eventSystem.GetComponents<InputSystemUIInputModule>().Length == 1, "Created duplicate input modules.");

            StandaloneInputModule legacy = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            module.enabled = false;
            GameUIInput.EnsureEventSystem(owner.transform);
            Expect(!legacy.enabled && module.enabled, "Existing legacy module was not replaced.");

            if (EventSystem.current != eventSystem) Invoke(eventSystem, "OnEnable");
            // Batch edit-mode checks explicitly initialize the normal Play Mode lifecycle.
            Invoke(module, "OnEnable");
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Mouse mouse = InputSystem.AddDevice<Mouse>("UIRegressionMouse");
            module.ActivateModule();
            Invoke(eventSystem, "Update");

            GameObject root = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            Invoke(root.GetComponent<GraphicRaycaster>(), "OnEnable");
            LoginScreenUI login = owner.AddComponent<LoginScreenUI>();
            int clicks = 0;
            Button button = (Button)typeof(LoginScreenUI).GetMethod("CreateButton", PrivateInstance).Invoke(login,
                new object[] { root.transform, "LoginButton", "LOGIN", new UnityEngine.Events.UnityAction(() => clicks++) });
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(300f, 60f);
            Camera camera = new GameObject("UI Test Camera", typeof(Camera)).GetComponent<Camera>();
            camera.targetTexture = new RenderTexture(Screen.width, Screen.height, 24);
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.position);
            var hits = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(new PointerEventData(eventSystem) { position = position }, hits);
            Debug.Log($"[UIInputRegression] pointer={position}, hits={hits.Count}, depth={button.targetGraphic.depth}");
            SendMouse(mouse, module, position, false);
            SendMouse(mouse, module, position, true);
            SendMouse(mouse, module, position, false);
            Expect(clicks > 0, "Login-style button did not receive the simulated mouse click.");

            InputField field = (InputField)typeof(LoginScreenUI).GetMethod("CreateInput", PrivateInstance).Invoke(login,
                new object[] { root.transform, "UsernameInput", "Username", false, "" });
            field.ActivateInputField();
            Invoke(field, "LateUpdate");
            Expect(field.isFocused, "Input field did not receive focus.");
            field.ProcessEvent(Event.KeyboardEvent("a"));
            field.ProcessEvent(Event.KeyboardEvent("b"));
            Expect(field.text == "ab", "Input field could not accept text.");
            field.DeactivateInputField();
            field.gameObject.SetActive(false);

            Time.timeScale = 0f;
            int beforePausedClick = clicks;
            SendMouse(mouse, module, position, false);
            SendMouse(mouse, module, position, true);
            SendMouse(mouse, module, position, false);
            Expect(clicks > beforePausedClick, "UI clicks stopped while the game was paused.");
            Time.timeScale = 1f;

            Debug.Log("[UIInputRegression] PASS: creation, reuse, legacy replacement, mouse click, text focus/input, and paused UI click (Input System only).");
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void SendMouse(Mouse mouse, InputSystemUIInputModule module, Vector2 position, bool pressed)
    {
        InputState.Change(mouse, new MouseState { position = position, buttons = pressed ? (ushort)1 : (ushort)0 });
        module.Process();
    }

    private static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, PrivateInstance).Invoke(target, null);

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
