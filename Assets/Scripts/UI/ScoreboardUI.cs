using TMPro;
using UnityEngine;

public class ScoreboardUI : MonoBehaviour
{
    public static ScoreboardUI Instance { get; private set; }

    [SerializeField] private TMP_Text totalKillsText;
    [SerializeField] private string labelFormat = "Kills: {0}";

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        if (totalKillsText == null || WaveManager.Instance == null) return;

        bool visible = WaveManager.Instance.CurrentWave >= 1;
        if (totalKillsText.gameObject.activeSelf != visible)
            totalKillsText.gameObject.SetActive(visible);

        if (visible)
            totalKillsText.text = string.Format(labelFormat, WaveManager.Instance.TotalKillCount);
    }
}
