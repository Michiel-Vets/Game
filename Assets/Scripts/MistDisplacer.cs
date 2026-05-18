using UnityEngine;

public class MistDisplacer : MonoBehaviour
{
    private void OnEnable() => MistTrailController.Register(transform);
    private void OnDisable() => MistTrailController.Unregister(transform);
}