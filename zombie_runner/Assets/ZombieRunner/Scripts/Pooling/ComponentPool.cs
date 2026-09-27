using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ZombieRunner.Pooling
{
    /// <summary>
    /// Typed pool of prefab instances. Everything is instantiated up front; Spawn and Despawn
    /// only toggle activation, so steady-state gameplay allocates nothing.
    /// </summary>
    public sealed class ComponentPool<T> : IPoolReturn where T : PooledBehaviour
    {
        private readonly T prefab;
        private readonly Transform folder;
        private readonly Stack<T> available;
        private int created;
        private bool expansionWarned;

        public string Name { get; }
        public T Prefab => prefab;
        public int CountAll => created;
        public int CountActive => created - available.Count;
        public int CountInactive => available.Count;

        /// <param name="folder">Parent for the instances, or null to keep them at the scene root.</param>
        public ComponentPool(T prefab, int prewarm, Transform folder, string name)
        {
            if (prefab == null) throw new ArgumentNullException(nameof(prefab), $"Pool '{name}' has no prefab assigned.");

            this.prefab = prefab;
            this.folder = folder;
            Name = name;

            // Double capacity so returns after a modest expansion don't resize the stack mid-run.
            available = new Stack<T>(Mathf.Max(prewarm, 1) * 2);
            for (int i = 0; i < prewarm; i++) available.Push(CreateInstance());
        }

        public T Spawn(Vector3 position, Quaternion rotation)
        {
            T item;
            if (available.Count > 0)
            {
                item = available.Pop();
            }
            else
            {
                if (!expansionWarned)
                {
                    expansionWarned = true;
                    Debug.LogWarning($"[ComponentPool] '{Name}' ran dry at {created} instances and is expanding. " +
                                     "Raise its prewarm size to keep gameplay allocation-free.");
                }
                item = CreateInstance();
            }

            item.transform.SetPositionAndRotation(position, rotation);
            item.IsSpawned = true;
            item.gameObject.SetActive(true);
            item.OnSpawned();
            return item;
        }

        void IPoolReturn.Return(PooledBehaviour item)
        {
            if (!item.IsSpawned) return; // already back in the pool

            item.IsSpawned = false;
            item.OnDespawned();
            item.gameObject.SetActive(false);
            available.Push((T)item);
        }

        private T CreateInstance()
        {
            T item = folder != null ? Object.Instantiate(prefab, folder) : Object.Instantiate(prefab);
            item.name = prefab.name;
            item.Owner = this;
            item.IsSpawned = false;
            item.gameObject.SetActive(false);
            created++;
            return item;
        }
    }
}
