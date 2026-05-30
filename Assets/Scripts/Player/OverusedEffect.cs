using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Beheert het visuele "on fire"-effect en HP-aftrek wanneer de speler
/// de batterij leegbrandt tijdens een combo (overused-state).
/// Voeg dit script toe aan hetzelfde GameObject als ComboSystem.
/// </summary>
public class OverusedEffect : MonoBehaviour
{
    [Header("HP Drain")]
    [Tooltip("HP per seconde dat de speler verliest tijdens overused.")]
    [SerializeField] private float hpDrainPerSecond = 18f;

    [Header("Fire Overlay")]
    [SerializeField] private Color fireColorA = new Color(1f, 0.25f, 0f, 0.10f);
    [SerializeField] private Color fireColorB = new Color(1f, 0.6f,  0f, 0.22f);
    [SerializeField] private float flickerSpeed = 9f;

    public bool IsActive { get; private set; }

    private Image  _overlay;
    private HealthController _health;
    private float  _flickerTimer;

    private void Awake()
    {
        _health = FindObjectOfType<HealthController>();
        BuildOverlay();
        _overlay.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!IsActive) return;

        _flickerTimer += Time.deltaTime;
        float t = (Mathf.Sin(_flickerTimer * flickerSpeed) + 1f) * 0.5f;
        _overlay.color = Color.Lerp(fireColorA, fireColorB, t);

        if (_health != null)
            _health.TakeDamage(hpDrainPerSecond * Time.deltaTime);
    }

    public void StartEffect()
    {
        IsActive = true;
        _flickerTimer = 0f;
        _overlay.gameObject.SetActive(true);
    }

    public void StopEffect()
    {
        IsActive = false;
        if (_overlay != null)
            _overlay.gameObject.SetActive(false);
    }

    private void BuildOverlay()
    {
        var canvasGO = new GameObject("OverusedCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 99;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("FireOverlay");
        imgGO.transform.SetParent(canvasGO.transform, false);
        _overlay = imgGO.AddComponent<Image>();
        _overlay.color = Color.clear;
        _overlay.raycastTarget = false;

        var rt = imgGO.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }
}
