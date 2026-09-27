using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ZombieRunner.Player
{
    /// <summary>
    /// Runs the squad forward along +Z and maps horizontal drag (touch or mouse) to a clamped X target,
    /// which the squad follows with frame-rate independent damping.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadMovement : MonoBehaviour
    {
        [Header("Forward Velocity")]
        [SerializeField, Min(0f)] private float forwardSpeed = 7.5f;

        [Header("Horizontal Control")]
        [Tooltip("World units the target moves when the finger drags across the full screen width. " +
                 "Normalising by screen width keeps the feel identical on every resolution.")]
        [SerializeField, Min(0.1f)] private float dragUnitsPerScreenWidth = 10f;
        [Tooltip("Time for the squad to catch up with the drag target. Lower is snappier.")]
        [SerializeField, Min(0.001f)] private float smoothTime = 0.08f;
        [Tooltip("Lane barrier: the squad's X stays within ±this value.")]
        [SerializeField, Min(0f)] private float horizontalLimit = 4.25f;

        private float targetX;
        private float velocityX;
        private float lastPointerX;
        private int activePointerId = -1;

        public float ForwardSpeed => forwardSpeed;
        public float HorizontalLimit => horizontalLimit;
        public Vector3 Velocity => new Vector3(velocityX, 0f, forwardSpeed);

        private void Start()
        {
            targetX = Mathf.Clamp(transform.position.x, -horizontalLimit, horizontalLimit);
        }

        private void OnDisable()
        {
            activePointerId = -1;
            velocityX = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            ReadDrag();

            Vector3 pos = transform.position;
            pos.z += forwardSpeed * dt;
            pos.x = Mathf.SmoothDamp(pos.x, targetX, ref velocityX, smoothTime, Mathf.Infinity, dt);
            pos.x = Mathf.Clamp(pos.x, -horizontalLimit, horizontalLimit);
            transform.position = pos;
        }

        private void ReadDrag()
        {
            if (!TryReadPointer(out float pointerX, out int pointerId))
            {
                activePointerId = -1;
                return;
            }

            // A new press, or a different finger taking over, starts a fresh drag with no jump.
            if (pointerId == activePointerId)
            {
                float delta = (pointerX - lastPointerX) / Mathf.Max(1, Screen.width);
                targetX = Mathf.Clamp(targetX + delta * dragUnitsPerScreenWidth, -horizontalLimit, horizontalLimit);
            }

            activePointerId = pointerId;
            lastPointerX = pointerX;
        }

        private static bool TryReadPointer(out float x, out int id)
        {
#if ENABLE_INPUT_SYSTEM
            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed)
            {
                x = pointer.position.ReadValue().x;
                id = pointer.deviceId;
                return true;
            }
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                x = touch.position.x;
                id = touch.fingerId;
                return true;
            }
            if (Input.GetMouseButton(0))
            {
                x = Input.mousePosition.x;
                id = int.MaxValue; // distinct from any finger id
                return true;
            }
#endif
            x = 0f;
            id = -1;
            return false;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Vector3 p = transform.position;
            Gizmos.DrawLine(new Vector3(-horizontalLimit, p.y, p.z - 5f), new Vector3(-horizontalLimit, p.y, p.z + 30f));
            Gizmos.DrawLine(new Vector3(horizontalLimit, p.y, p.z - 5f), new Vector3(horizontalLimit, p.y, p.z + 30f));
        }
    }
}
