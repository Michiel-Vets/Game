using UnityEngine;

/// <summary>
/// Gedeelde basis voor alle oppak-items: trigger-setup, bobbing-animatie en rotatie.
/// Subklassen implementeren OnTriggerEnter om het specifieke effect toe te passen.
/// </summary>
public abstract class BasePickup : MonoBehaviour
{
    [SerializeField] private float bobHeight     = 0.15f;
    [SerializeField] private float bobSpeed      = 2f;
    [SerializeField] private float rotationSpeed = 90f;

    private Vector3 _startPosition;

    protected virtual void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }
    }

    protected virtual void Start()
    {
        _startPosition = transform.position;
    }

    private void Update()
    {
        float newY = _startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
    }
}
