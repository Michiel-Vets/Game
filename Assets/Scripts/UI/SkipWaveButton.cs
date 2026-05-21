using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Zichtbaar tijdens de pauze tussen waves; verdwijnt zodra een wave actief is.
/// Werkt ook als het script in een verborgen settings panel zit, omdat het een
/// CanvasGroup gebruikt in plaats van SetActive — Update blijft draaien zolang
/// het parent-object actief is.
/// Wijs 'buttonGroup' toe aan de CanvasGroup op de knop (of diens root).
/// </summary>
public class SkipWaveButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup buttonGroup;

    private bool _lastBreakState;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(OnClicked);

        SetVisible(false);
    }

    private void Update()
    {
        bool isBreak = WaveManager.Instance != null && WaveManager.Instance.IsBreak;
        if (isBreak == _lastBreakState) return;

        _lastBreakState = isBreak;
        SetVisible(isBreak);
    }

    private void OnClicked()
    {
        WaveManager.Instance?.SkipToNextWave();
    }

    private void SetVisible(bool visible)
    {
        if (buttonGroup == null) return;
        buttonGroup.alpha          = visible ? 1f : 0f;
        buttonGroup.interactable   = visible;
        buttonGroup.blocksRaycasts = visible;
    }
}
