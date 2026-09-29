# Combat HUD font

- Source: [Noto Sans CJK KR Regular](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/Korean/NotoSansCJKkr-Regular.otf), downloaded 2026-09-28.
- License: SIL Open Font License 1.1; see `LICENSE.txt` and the copyright metadata embedded in the font.
- `Assets/Resources/CombatHUDFont.asset` includes a static TMP atlas for the Korean HUD labels, ASCII letters, digits and punctuation. It does not rely on a font installed on the player's computer.
- Font generation: `Tools > Combat HUD > Build Korean Font` (`CombatHUDAssets.Build`). The command preserves an existing atlas. To add new labels, update `CombatHUDAssets.Glyphs` and regenerate the asset deliberately.

The original font is retained for reproducible atlas generation.
