using UnityEngine;

namespace ZombieRunner.Core
{
    /// <summary>
    /// Mobile runtime settings. iOS and Android cap at 30 FPS unless a target frame rate is set.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class PerformanceBootstrap : MonoBehaviour
    {
        [SerializeField, Min(15)] private int targetFrameRate = 60;
        [SerializeField] private bool keepScreenAwake = true;

        private void Awake()
        {
            QualitySettings.vSyncCount = 0; // let targetFrameRate govern on every platform
            Application.targetFrameRate = targetFrameRate;
            if (keepScreenAwake) Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void OnDestroy()
        {
            if (keepScreenAwake) Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }
    }
}
