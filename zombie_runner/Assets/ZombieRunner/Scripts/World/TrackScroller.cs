using UnityEngine;

namespace ZombieRunner.World
{
    /// <summary>
    /// Endless track made from a few ground segments laid end to end. When a segment falls behind the
    /// squad it jumps to the front, so the run never reaches the end of the track.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackScroller : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [Tooltip("Segments laid end to end along +Z, each Segment Length long.")]
        [SerializeField] private Transform[] segments = new Transform[0];
        [SerializeField, Min(1f)] private float segmentLength = 60f;
        [Tooltip("How far behind the target a segment's far end must be before it moves to the front.")]
        [SerializeField, Min(0f)] private float recycleBehind = 20f;

        private void LateUpdate()
        {
            if (target == null || segments.Length == 0) return;

            float threshold = target.position.z - recycleBehind;
            float loopLength = segmentLength * segments.Length;

            for (int i = 0; i < segments.Length; i++)
            {
                Transform segment = segments[i];
                if (segment.position.z + segmentLength * 0.5f < threshold)
                {
                    segment.position += new Vector3(0f, 0f, loopLength);
                }
            }
        }
    }
}
