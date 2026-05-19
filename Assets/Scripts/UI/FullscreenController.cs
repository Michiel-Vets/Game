using UnityEngine;

public class FullscreenController : MonoBehaviour
{
    public void ToggleFullscreen()
    {
        Screen.fullScreen = !Screen.fullScreen;
    }
}