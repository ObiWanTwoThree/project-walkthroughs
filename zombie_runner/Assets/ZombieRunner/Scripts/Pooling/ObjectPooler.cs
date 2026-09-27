using UnityEngine;
using ZombieRunner.Combat;
using ZombieRunner.Enemies;

namespace ZombieRunner.Pooling
{
    /// <summary>
    /// Scene-wide owner of the typed pools. Pools are pre-warmed in Awake, which runs
    /// before any gameplay script's Start.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class ObjectPooler : MonoBehaviour
    {
        public static ObjectPooler Instance { get; private set; }

        [Header("Bullets")]
        [SerializeField] private Bullet bulletPrefab;
        [SerializeField, Min(0)] private int bulletPrewarm = 150;

        [Header("Zombies")]
        [SerializeField] private ZombieAgent zombiePrefab;
        [SerializeField, Min(0)] private int zombiePrewarm = 200;

        [Header("Hierarchy")]
        [Tooltip("Group pooled instances under a folder per pool. Tidy while debugging, but Unity updates " +
                 "one large moving hierarchy more slowly than many root objects, so leave this off for profiling and builds.")]
        [SerializeField] private bool groupUnderFolders;

        public ComponentPool<Bullet> Bullets { get; private set; }
        public ComponentPool<ZombieAgent> Zombies { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            Bullets = new ComponentPool<Bullet>(bulletPrefab, bulletPrewarm, MakeFolder("Pool_Bullet"), "Bullet");
            Zombies = new ComponentPool<ZombieAgent>(zombiePrefab, zombiePrewarm, MakeFolder("Pool_Zombie"), "Zombie");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private Transform MakeFolder(string folderName)
        {
            if (!groupUnderFolders) return null;
            var folder = new GameObject(folderName).transform;
            folder.SetParent(transform, false);
            return folder;
        }

        // Keeps the singleton correct when domain reload is disabled in Enter Play Mode settings.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;
    }
}
