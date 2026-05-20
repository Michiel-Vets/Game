using System.Collections;
using System.Collections.Generic;
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

    public void ShowWaveMessage(int wave, WaveType type)
    {
        string ordinal = GetOrdinal(wave);
        string message = type == WaveType.Standard
            ? $"{ordinal} wave"
            : $"{ordinal} wave\n<size=70%>{GetWaveTypeLabel(type)}</size>";

        Show(message);
    }

    public void ShowCompletionMessage(int points)
    {
        Show($"Wave Complete!\n<size=70%>+{points} pts</size>");
    }

    public void ShowPenaltyMessage()
    {
        Show("Enemies Escaped!\n<size=70%>Next wave will be harder</size>");
    }

    private string GetWaveTypeLabel(WaveType type)
    {
        var parts = new List<string>();
        if ((type & WaveType.Horde) != 0) parts.Add("HORDE");
        if ((type & WaveType.Elite) != 0) parts.Add("ELITE");
        if ((type & WaveType.Siege) != 0) parts.Add("SIEGE");
        return string.Join(" + ", parts);
    }

    private void Show(string message)
    {
        if (waveText == null) return;
        if (displayCoroutine != null) StopCoroutine(displayCoroutine);
        displayCoroutine = StartCoroutine(DisplayMessage(message));
    }

    private IEnumerator DisplayMessage(string message)
    {
        waveText.text = message;

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