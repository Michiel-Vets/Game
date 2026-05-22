using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Altijd zichtbaar. Klik om de huidige wave te skippen en de volgende meteen te starten.
/// Bestaande enemies worden verwijderd zodra de nieuwe wave begint.
/// </summary>
public class SkipWaveButton : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Awake()
    {
        if (button != null)
            button.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        WaveManager.Instance?.SkipToNextWave();
    }
}
