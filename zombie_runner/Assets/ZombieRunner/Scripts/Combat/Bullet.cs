using UnityEngine;
using ZombieRunner.Enemies;
using ZombieRunner.Pooling;

namespace ZombieRunner.Combat
{
    /// <summary>
    /// Pooled projectile. Hits are found by sweeping a sphere along each frame's path rather than by
    /// physics triggers: a bullet travelling ~0.5 m per frame would tunnel through a trigger collider,
    /// and a sweep needs no Rigidbody on either side.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Bullet : PooledBehaviour
    {
        [SerializeField, Min(0.1f)] private float speed = 32f;
        [SerializeField, Min(0.05f)] private float lifetime = 1.2f;
        [SerializeField, Min(1)] private int damage = 1;
        [Tooltip("Radius of the swept hit test.")]
        [SerializeField, Min(0.01f)] private float hitRadius = 0.12f;

        private Vector3 direction;
        private int hitMask;
        private float timeLeft;

        public float Speed => speed;

        private void Awake()
        {
            // Bullets never need a collider; one left on the prefab would only cost physics time.
            if (TryGetComponent(out Collider col)) col.enabled = false;
        }

        protected internal override void OnSpawned()
        {
            timeLeft = lifetime;
            direction = transform.forward;
        }

        /// <param name="dir">Normalised travel direction.</param>
        /// <param name="mask">Layers the bullet can hit.</param>
        public void Launch(Vector3 dir, LayerMask mask)
        {
            direction = dir;
            hitMask = mask;
            timeLeft = lifetime;
            if (dir.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(dir);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float step = speed * dt;
            Vector3 pos = transform.position;

            if (Physics.SphereCast(pos, hitRadius, direction, out RaycastHit hit, step, hitMask, QueryTriggerInteraction.Collide))
            {
                Rigidbody body = hit.rigidbody;
                if (body != null && body.TryGetComponent(out ZombieAgent zombie))
                {
                    zombie.ApplyDamage(damage, direction);
                }
                Despawn();
                return;
            }

            transform.position = pos + direction * step;

            timeLeft -= dt;
            if (timeLeft <= 0f) Despawn();
        }
    }
}
