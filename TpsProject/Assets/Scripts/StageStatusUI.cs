using TMPro;
using UnityEngine;

public class StageStatusUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI remainingEnemiesText;
    private Canvas canvas;
    private RectTransform safeArea;
    private PlayerHealth health;

    private void Awake() => EnsureUI();

    private void EnsureUI()
    {
        if (canvas != null) return;
        if (stageText != null) stageText.gameObject.SetActive(false);
        if (remainingEnemiesText != null) remainingEnemiesText.gameObject.SetActive(false);
        canvas = CombatHUDStyle.CreateCanvas("StageStatusCanvas", out safeArea);
        RectTransform panel = CombatHUDStyle.Image("StagePanel", safeArea, new Vector2(0.5f, 1f),
            new Vector2(0f, -28f), new Vector2(440f, 92f), CombatHUDStyle.Panel).rectTransform;
        panel.pivot = new Vector2(0.5f, 1f);
        CombatHUDStyle.Image("Accent", panel, new Vector2(0.5f, 1f), new Vector2(0f, -1f),
            new Vector2(80f, 2f), CombatHUDStyle.Accent);
        stageText = CombatHUDStyle.Text("Stage", panel, new Vector2(0f, 15f), new Vector2(400f, 38f), 29f);
        stageText.color = CombatHUDStyle.Accent;
        remainingEnemiesText = CombatHUDStyle.Text("RemainingEnemies", panel, new Vector2(0f, -21f), new Vector2(416f, 30f), 21f);
        remainingEnemiesText.color = CombatHUDStyle.Muted;
    }

    private void LateUpdate()
    {
        if (canvas == null) return;
        if (health == null) health = FindFirstObjectByType<PlayerHealth>();
        canvas.enabled = isActiveAndEnabled && (health == null || !health.IsDead) && Time.timeScale > 0f;
        CombatHUDStyle.ApplySafeArea(safeArea);
    }

    private void OnDisable() { if (canvas != null) canvas.enabled = false; }
    private void OnDestroy() => CombatHUDStyle.DestroyCanvas(canvas);

    public void SetStageStatus(int stage, int remainingEnemies, bool waitingForNextStage)
    {
        EnsureUI();
        if (stageText != null)
        {
            stageText.text = $"스테이지 {stage}";
        }

        if (remainingEnemiesText != null)
        {
            remainingEnemiesText.text = waitingForNextStage
                ? $"남은 적 {remainingEnemies} · 다음 스테이지 준비"
                : $"남은 적 {remainingEnemies}";
        }
    }
}
