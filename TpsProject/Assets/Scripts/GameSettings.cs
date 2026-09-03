using UnityEngine;

public static class GameSettings
{
    private const string SensitivityKey = "TPS.Settings.MouseSensitivity";
    private const string VolumeKey = "TPS.Settings.MasterVolume";
    public const float MinSensitivity = 0.1f;
    public const float MaxSensitivity = 3f;

    public static float MouseSensitivity { get; private set; } = 1f;
    public static float MasterVolume { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Load()
    {
        MouseSensitivity = Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinSensitivity, MaxSensitivity);
        MasterVolume = Clamp(PlayerPrefs.GetFloat(VolumeKey, 1f), 0f, 1f);
        AudioListener.volume = MasterVolume;
    }

    public static void SetMouseSensitivity(float value)
    {
        MouseSensitivity = Clamp(value, MinSensitivity, MaxSensitivity);
        PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
    }

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Clamp(value, 0f, 1f);
        AudioListener.volume = MasterVolume;
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
    }

    // Save at the end of editing, rather than writing to disk on every drag event.
    public static void Save() => PlayerPrefs.Save();

    private static float Clamp(float value, float min, float max)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, min, max);
    }
}
