using UnityEngine;

public class BatteryPickup : MonoBehaviour
{
    [SerializeField] private float rechargeAmount = 50f;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private bool billboard = true;

    private Vector3 startPosition;
    private Camera mainCamera;

    private void Start()
    {
        startPosition = transform.position;
        mainCamera = Camera.main;
    }

    private void Update()
    {
        float newY = startPosition.y + Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        if (billboard && mainCamera != null)
            transform.rotation = mainCamera.transform.rotation;
    }

    private void OnTriggerEnter(Collider other)
    {
        BatteryController battery = other.GetComponentInParent<BatteryController>();
        if (battery == null)
            return;

        battery.RechargeBattery(rechargeAmount);
        Destroy(gameObject);
    }
}