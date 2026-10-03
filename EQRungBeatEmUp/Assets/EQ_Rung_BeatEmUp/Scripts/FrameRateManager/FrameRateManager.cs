using UnityEngine;

public class FrameRateManager : MonoBehaviour
{
    private void Awake()
    {
        // Disable VSync so targetFrameRate controls the FPS.
        QualitySettings.vSyncCount = 0;

        // Lock the game to 60 FPS.
        Application.targetFrameRate = 60;
    }
}