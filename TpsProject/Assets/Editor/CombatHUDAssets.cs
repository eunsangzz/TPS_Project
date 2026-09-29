using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class CombatHUDAssets
{
    public const string FontPath = "Assets/Resources/CombatHUDFont.asset";
    public const string Glyphs = "체력 스테이지 남은 적 다음 준비 재장전 초 ·";

    [MenuItem("Tools/Combat HUD/Build Korean Font")]
    public static void Build()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) != null) return;
        Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/CombatHUD/NotoSansCJKkr-Regular.otf");
        if (source == null) throw new InvalidOperationException("Combat HUD Korean source font is missing.");
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024);
        font.name = "CombatHUDFont";
        string characters = new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray()) + Glyphs;
        if (!font.TryAddCharacters(characters, out string missing))
            throw new InvalidOperationException("Missing HUD glyphs: " + missing);
        // All HUD labels and numeric characters are baked, including in standalone builds.
        font.atlasPopulationMode = AtlasPopulationMode.Static;
        AssetDatabase.CreateAsset(font, FontPath);
        font.material.name = "CombatHUDFont Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (Texture2D atlas in font.atlasTextures)
        {
            atlas.name = "CombatHUDFont Atlas";
            AssetDatabase.AddObjectToAsset(atlas, font);
        }
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        Debug.Log("[CombatHUD] Korean font atlas generated.");
    }
}
