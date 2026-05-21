using UnityEngine;
using TMPro;
using System.Collections;

public class WaveTooltipUI : MonoBehaviour
{
    [SerializeField] private GameObject tooltipPanel;
    [SerializeField] private TMP_Text tooltipText;
    [SerializeField] private float displayDuration = 3f;

    private Coroutine hideCoroutine;

    private void Start()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    public void ShowTooltip(WaveType type)
    {
        if (tooltipPanel == null) return;

        string message = GetTooltipMessage(type);
        tooltipText.text = message;

        tooltipPanel.SetActive(true);

        if (hideCoroutine != null)
            StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private string GetTooltipMessage(WaveType type)
    {
        switch (type)
        {
            case WaveType.Siege:
                return "SIEGE WAVE\nEnemies spawn non-stop! Stay on the move!";
            case WaveType.Horde:
                return "HORDE WAVE\nMore enemies, but they're weaker!";
            case WaveType.Elite:
                return "ELITE WAVE\nStronger enemies, but fewer of them!";
            default:
                return "";
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        tooltipPanel.SetActive(false);
    }
}