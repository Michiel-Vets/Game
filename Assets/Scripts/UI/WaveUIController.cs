using System.Collections;
using TMPro;
using UnityEngine;

public class WaveUIController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text waveText;

    [Header("Display")]
    [SerializeField] private float displayDuration = 2.5f;
    [SerializeField] private float fadeTime = 0.6f;

    private Coroutine displayCoroutine;

    private void Awake()
    {
        if (waveText != null)
            waveText.alpha = 0f;
    }

    private static readonly string[] Ordinals =
    {
        "1st", "2nd", "3rd", "4th", "5th",
        "6th", "7th", "8th", "9th", "10th",
        "11th", "12th", "13th", "14th", "15th",
        "16th", "17th", "18th", "19th", "20th"
    };

    private string GetOrdinal(int n)
    {
        if (n >= 1 && n <= Ordinals.Length) return Ordinals[n - 1];
        int mod100 = n % 100;
        int mod10 = n % 10;
        if (mod100 >= 11 && mod100 <= 13) return $"{n}th";
        return mod10 switch { 1 => $"{n}st", 2 => $"{n}nd", 3 => $"{n}rd", _ => $"{n}th" };
    }

    // NIEUWE METHODE: met WaveType parameter
    public void ShowWaveMessage(int wave, WaveType type)
    {
        if (waveText == null) return;
        if (displayCoroutine != null) StopCoroutine(displayCoroutine);
        displayCoroutine = StartCoroutine(DisplayWave(wave, type));
    }

    private string GetWaveTypeName(WaveType type) => type switch
    {
        WaveType.Siege => "SIEGE WAVE",
        WaveType.Horde => "HORDE WAVE",
        WaveType.Elite => "ELITE WAVE",
        _              => "WAVE",
    };

    private IEnumerator DisplayWave(int wave, WaveType type)
    {
        waveText.text = $"{GetOrdinal(wave)} {GetWaveTypeName(type)}";

        float t = 0f;
        while (t < fadeTime)
        {
            waveText.alpha = t / fadeTime;
            t += Time.deltaTime;
            yield return null;
        }
        waveText.alpha = 1f;

        yield return new WaitForSeconds(displayDuration);

        t = 0f;
        while (t < fadeTime)
        {
            waveText.alpha = 1f - t / fadeTime;
            t += Time.deltaTime;
            yield return null;
        }
        waveText.alpha = 0f;
    }
}