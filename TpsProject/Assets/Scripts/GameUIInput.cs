using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class GameUIInput
{
    public static EventSystem EnsureEventSystem(Transform owner)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
        {
            GameObject root = new GameObject("EventSystem", typeof(EventSystem));
            root.transform.SetParent(owner, false);
            eventSystem = root.GetComponent<EventSystem>();
        }

        // Existing scenes may still contain a module that reads the disabled legacy Input API.
        foreach (StandaloneInputModule legacyModule in eventSystem.GetComponents<StandaloneInputModule>())
            legacyModule.enabled = false;

        InputSystemUIInputModule module = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        module.enabled = true;
        if (module.actionsAsset == null) module.AssignDefaultActions();
        eventSystem.UpdateModules();
        return eventSystem;
    }
}
