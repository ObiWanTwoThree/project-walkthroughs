using UnityEngine;

namespace ZombieRunner.Player
{
    /// <summary>
    /// Keeps a fixed-angle camera locked to the squad's forward progress. Z tracks exactly, to avoid
    /// jitter at speed. X follows part of the squad's sideways movement, with smoothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 11f, -8f);
        [Tooltip("Share of the squad's sideways movement the camera follows: 0 stays on the lane centre, 1 follows fully.")]
        [SerializeField, Range(0f, 1f)] private float lateralFollow = 0.35f;
        [SerializeField, Min(0f)] private float lateralSmoothTime = 0.15f;

        private float velocityX;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 t = target.position;
            Vector3 pos = transform.position;
            pos.y = t.y + offset.y;
            pos.z = t.z + offset.z;
            pos.x = Mathf.SmoothDamp(pos.x, t.x * lateralFollow + offset.x, ref velocityX, lateralSmoothTime);
            transform.position = pos;
        }
    }
}
