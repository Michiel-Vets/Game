using UnityEngine;
using UnityEngine.UI;

public class WaveProgressUI : MonoBehaviour
{
    [SerializeField] private Image progressFill;
    [SerializeField] private GameObject progressContainer;

    private void Start()
    {
        if (progressContainer != null)
            progressContainer.SetActive(false);
    }

    public void SetProgress(float progress)
    {
        if (progressContainer != null && !progressContainer.activeSelf)
            progressContainer.SetActive(true);

        if (progressFill != null)
            progressFill.fillAmount = progress;

        // Verberg als de wave volledig voorbij is
        if (progress >= 0.99f)
            Hide();
    }

    public void Hide()
    {
        if (progressContainer != null)
            progressContainer.SetActive(false);
    }
}