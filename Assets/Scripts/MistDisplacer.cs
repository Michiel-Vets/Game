using UnityEngine;

public class MistDisplacer : MonoBehaviour
{
    private void OnEnable()
    {
        VolumetricMistController.Register(transform);
        Debug.Log($"[MistDisplacer] Geregistreerd: {gameObject.name}");
    }

    private void OnDisable()
    {
        VolumetricMistController.Unregister(transform);
        Debug.Log($"[MistDisplacer] Verwijderd: {gameObject.name}");
    }
}