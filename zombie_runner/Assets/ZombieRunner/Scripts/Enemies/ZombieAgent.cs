using System;
using UnityEngine;
using ZombieRunner.Pooling;

namespace ZombieRunner.Enemies
{
    /// <summary>
    /// Pooled zombie. Walks down-track (-Z) and drifts toward the squad. Reacts to hits with a
    /// material flash and a decaying knockback, and returns to the pool when it dies, reaches the
    /// squad, or falls far enough behind it.
    /// </summary>
    /// <remarks>
    /// The kinematic Rigidbody lets the moving collider be queried cheaply and raise trigger events
    /// against the squad. Movement is still written straight to the transform.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public sealed class ZombieAgent : PooledBehaviour
    {
        /// <summary>Raised when a zombie's health reaches zero, just before it is recycled.</summary>
        public static event Action<ZombieAgent> Killed;

        /// <summary>Raised when a zombie touches the squad. Hook squad losses here.</summary>
        public static event Action<ZombieAgent> ReachedSquad;

        [Header("Stats")]
        [SerializeField, Min(1)] private int maxHealth = 3;
        [SerializeField, Min(0f)] private float advanceSpeed = 2.2f;
        [Tooltip("Sideways drift toward the squad in m/s. 0 walks straight down the track.")]
        [SerializeField, Min(0f)] private float lateralPursuitSpeed = 0.6f;

        [Header("Hit Reaction")]
        [Tooltip("Speed added along the bullet's path on each hit. Total push distance is impulse ÷ damping.")]
        [SerializeField, Min(0f)] private float knockbackImpulse = 2.5f;
        [SerializeField, Min(0.1f)] private float knockbackDamping = 10f;
        [SerializeField, Min(0f)] private float flashDuration = 0.08f;
        [SerializeField] private Renderer bodyRenderer;
        [Tooltip("Shared material swapped in while flashing. Swapping shared materials keeps the renderer " +
                 "SRP Batcher compatible, which a MaterialPropertyBlock would not.")]
        [SerializeField] private Material flashMaterial;

        [Header("Recycling")]
        [SerializeField] private string squadTag = "Player";
        [Tooltip("Recycle once this far behind the squad.")]
        [SerializeField, Min(0f)] private float despawnBehindDistance = 6f;

        private Material baseMaterial;
        private Transform squad;
        private int health;
        private Vector3 velocity;
        private Vector3 knockbackVelocity;
        private float flashTimer;

        /// <summary>Current walking velocity, excluding knockback. Used by the shooter to lead shots.</summary>
        public Vector3 Velocity => velocity;
        public int Health => health;

        private void Awake()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;

            if (bodyRenderer == null) bodyRenderer = GetComponentInChildren<Renderer>();
            if (bodyRenderer != null) baseMaterial = bodyRenderer.sharedMaterial;
        }

        /// <summary>Called by the spawner right after Spawn.</summary>
        public void Activate(Transform squadTransform)
        {
            squad = squadTransform;
        }

        protected internal override void OnSpawned()
        {
            health = maxHealth;
            velocity = new Vector3(0f, 0f, -advanceSpeed);
            knockbackVelocity = Vector3.zero;
            flashTimer = 0f;
            SetFlash(false);
        }

        protected internal override void OnDespawned()
        {
            SetFlash(false);
            squad = null;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;

            velocity.x = 0f;
            if (squad != null && lateralPursuitSpeed > 0f)
            {
                velocity.x = Mathf.Clamp(squad.position.x - pos.x, -1f, 1f) * lateralPursuitSpeed;
            }

            pos += velocity * dt;
            if (knockbackVelocity.sqrMagnitude > 1e-4f)
            {
                pos += knockbackVelocity * dt;
                knockbackVelocity *= Mathf.Exp(-knockbackDamping * dt); // frame-rate independent decay
            }
            transform.position = pos;

            if (flashTimer > 0f)
            {
                flashTimer -= dt;
                if (flashTimer <= 0f) SetFlash(false);
            }

            if (squad != null && pos.z < squad.position.z - despawnBehindDistance)
            {
                Despawn();
            }
        }

        public void ApplyDamage(int amount, Vector3 hitDirection)
        {
            if (!IsSpawned) return;

            health -= amount;
            if (health <= 0)
            {
                Killed?.Invoke(this);
                Despawn();
                return;
            }

            hitDirection.y = 0f;
            if (hitDirection.sqrMagnitude > 1e-4f)
            {
                knockbackVelocity += hitDirection.normalized * knockbackImpulse;
            }

            flashTimer = flashDuration;
            SetFlash(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsSpawned || !other.CompareTag(squadTag)) return;

            ReachedSquad?.Invoke(this);
            Despawn();
        }

        private void SetFlash(bool on)
        {
            if (bodyRenderer == null || flashMaterial == null) return;
            bodyRenderer.sharedMaterial = on ? flashMaterial : baseMaterial;
        }

        // Clears stale subscribers when domain reload is disabled in Enter Play Mode settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Killed = null;
            ReachedSquad = null;
        }
    }
}
