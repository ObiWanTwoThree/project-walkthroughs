using UnityEngine;
using ZombieRunner.Enemies;
using ZombieRunner.Pooling;

namespace ZombieRunner.Combat
{
    /// <summary>
    /// Sweeps a non-allocating radar around the squad, locks the nearest zombie ahead of it,
    /// and fires pooled bullets at a fixed cadence while a target exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadShooter : MonoBehaviour
    {
        [Header("Weapon")]
        [SerializeField] private Transform firePoint;
        [Tooltip("Seconds between shots. 0.1 = 10 rounds per second.")]
        [SerializeField, Min(0.01f)] private float fireInterval = 0.1f;
        [Tooltip("Aim where the target will be when the bullet arrives, not where it is now.")]
        [SerializeField] private bool leadTargets = true;

        [Header("Radar")]
        [SerializeField, Min(1f)] private float targetingRange = 16f;
        [Tooltip("Zombies more than this far behind the squad's Z are ignored.")]
        [SerializeField, Min(0f)] private float rearTolerance;
        [SerializeField] private LayerMask zombieLayer;
        [Tooltip("Colliders read per sweep. Keep it above the number of zombies that can be in range at once: " +
                 "once the buffer is full, the physics query returns an arbitrary subset and the true nearest can be missed.")]
        [SerializeField, Min(8)] private int radarCapacity = 128;

        private Collider[] radarBuffer;
        private float nextFireTime;
#if UNITY_EDITOR
        private bool bufferWarned;
#endif

        private void Awake()
        {
            radarBuffer = new Collider[radarCapacity];
            if (firePoint == null) firePoint = transform;
        }

        private void OnEnable()
        {
            nextFireTime = Time.time;
        }

        private void Update()
        {
            float now = Time.time;
            if (now < nextFireTime) return;
            if (!TryAcquireTarget(out ZombieAgent target, out Vector3 aimPoint)) return; // stay ready to fire

            Fire(target, aimPoint);

            // Accumulate so the cadence holds at any frame rate; reset after an idle spell so it doesn't burst.
            nextFireTime = now - nextFireTime > fireInterval ? now + fireInterval : nextFireTime + fireInterval;
        }

        private bool TryAcquireTarget(out ZombieAgent target, out Vector3 aimPoint)
        {
            Vector3 origin = transform.position;
            int count = Physics.OverlapSphereNonAlloc(origin, targetingRange, radarBuffer, zombieLayer, QueryTriggerInteraction.Collide);

#if UNITY_EDITOR
            if (count == radarBuffer.Length && !bufferWarned)
            {
                bufferWarned = true;
                Debug.LogWarning($"[SquadShooter] Radar buffer is full ({count}); raise Radar Capacity so the nearest zombie isn't missed.", this);
            }
#endif

            float minZ = origin.z - rearTolerance;
            float bestSqr = float.MaxValue;
            Collider best = null;
            Vector3 bestCenter = default;

            for (int i = 0; i < count; i++)
            {
                Collider candidate = radarBuffer[i];
                Vector3 center = candidate.bounds.center;
                if (center.z < minZ) continue;

                float sqr = (center - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                    bestCenter = center;
                }
            }

            target = null;
            aimPoint = bestCenter;
            if (best == null) return false;

            Rigidbody body = best.attachedRigidbody;
            if (body != null) body.TryGetComponent(out target);
            return true;
        }

        private void Fire(ZombieAgent target, Vector3 aimPoint)
        {
            Vector3 muzzle = firePoint.position;
            Bullet bullet = ObjectPooler.Instance.Bullets.Spawn(muzzle, Quaternion.identity);

            if (leadTargets && target != null)
            {
                float travelTime = Vector3.Distance(muzzle, aimPoint) / bullet.Speed;
                aimPoint += target.Velocity * travelTime;
            }

            Vector3 toTarget = aimPoint - muzzle;
            bullet.Launch(toTarget.sqrMagnitude > 1e-6f ? toTarget.normalized : Vector3.forward, zombieLayer);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, targetingRange);
        }
    }
}
