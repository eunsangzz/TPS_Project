using TMPro;
using UnityEngine;

public class StageStatusUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private TextMeshProUGUI remainingEnemiesText;

    public void SetStageStatus(int stage, int remainingEnemies, bool waitingForNextStage)
    {
        if (stageText != null)
        {
            stageText.text = $"STAGE {stage}";
        }

        if (remainingEnemiesText != null)
        {
            remainingEnemiesText.text = waitingForNextStage
                ? "NEXT STAGE"
                : $"ENEMIES {remainingEnemies}";
        }
    }
}
