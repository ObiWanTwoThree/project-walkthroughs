using UnityEngine;

namespace ZombieRunner.Pooling
{
    /// <summary>
    /// Base class for anything handed out by a <see cref="ComponentPool{T}"/>.
    /// Call <see cref="Despawn"/> instead of Destroy or SetActive(false).
    /// </summary>
    public abstract class PooledBehaviour : MonoBehaviour
    {
        internal IPoolReturn Owner;

        /// <summary>True between Spawn and Despawn. Guards against returning the same instance twice.</summary>
        public bool IsSpawned { get; internal set; }

        /// <summary>Called after the instance has been positioned and activated.</summary>
        protected internal virtual void OnSpawned() { }

        /// <summary>Called just before the instance is deactivated and returned.</summary>
        protected internal virtual void OnDespawned() { }

        /// <summary>Returns this instance to its pool. Safe to call more than once.</summary>
        public void Despawn()
        {
            if (Owner != null) Owner.Return(this);
            else gameObject.SetActive(false);
        }
    }

    internal interface IPoolReturn
    {
        void Return(PooledBehaviour item);
    }
}
