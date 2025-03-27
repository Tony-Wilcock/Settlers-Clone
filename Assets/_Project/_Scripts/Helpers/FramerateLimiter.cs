using UnityEngine;

namespace PunkyFruitBat
{
    public class FramerateLimiter : MonoBehaviour
    {
        [Tooltip("Target framerate. Set to 0 or less to disable the limit.")]
        [SerializeField][Range(15, 120)] private int targetFrameRate = 60;

        // Use this for initialization and whenever the targetFrameRate might change
        void ApplyFramerateLimit()
        {
            if (targetFrameRate > 0)
            {
                Application.targetFrameRate = targetFrameRate;
                QualitySettings.vSyncCount = 0;  // VSync must be disabled for targetFrameRate to work.  Crucial!
            }
            else
            {
                Application.targetFrameRate = -1; // Resets to the platform default.
            }
        }

        void Start()
        {
            ApplyFramerateLimit();
        }



        // This is *very important* for changes in the Inspector during play mode.
        //  Without this, changing the value in the Inspector at runtime won't have any effect.
        void OnValidate()
        {
            ApplyFramerateLimit();
        }
    }
}
