using UnityEngine;

/// <summary>
/// Houdt de speler binnen de circulaire kaartrand.
///
/// Setup: voeg dit script toe aan de speler (hetzelfde GameObject als PlayerController).
/// Geen verdere configuratie nodig — de radius wordt automatisch van MapController gelezen.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MapBoundary : MonoBehaviour
{
    [Tooltip("Hoeveel units van de rand de speler maximaal nog kan staan (0 = precies op de rand).")]
    [SerializeField] private float edgeInset = 0.3f;

    private Rigidbody _rb;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (MapController.Instance == null) return;

        Vector3 mapCenter = MapController.Instance.PlatformCenter;
        float   maxRadius = MapController.Instance.CurrentRadius - edgeInset;

        // Bereken XZ-afstand tot het map-centrum
        float dx = transform.position.x - mapCenter.x;
        float dz = transform.position.z - mapCenter.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);

        if (dist <= maxRadius) return; // Binnen de rand: niets te doen
        if (dist < 0.001f) return;     // Speler exact op centrum — geen richting bepaalbaar

        // Richting van centrum naar speler (XZ vlak)
        Vector3 outDir = new Vector3(dx / dist, 0f, dz / dist);

        // Verwijder de naar-buiten-gerichte snelheidscomponent
        float outwardSpeed = Vector3.Dot(_rb.linearVelocity, outDir);
        if (outwardSpeed > 0f)
            _rb.linearVelocity -= outDir * outwardSpeed;

        // Zet speler terug op de rand
        Vector3 clampedPos = new Vector3(
            mapCenter.x + outDir.x * maxRadius,
            transform.position.y,
            mapCenter.z + outDir.z * maxRadius);

        _rb.MovePosition(clampedPos);
    }
}
