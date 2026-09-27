using UnityEngine;
using ZombieRunner.Enemies;
using ZombieRunner.Pooling;

namespace ZombieRunner.Spawning
{
    /// <summary>
    /// Keeps a conveyor of horde clusters spawned ahead of the squad. Each cluster is a staggered grid
    /// with jittered positions and a random lateral offset, so the silhouettes read as swarms.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HordeSpawner : MonoBehaviour
    {
        [Header("Tracking")]
        [SerializeField] private Transform playerTransform;
        [Tooltip("Clusters are spawned until this far ahead of the squad. Keep it beyond the camera's view so none pop in.")]
        [SerializeField, Min(1f)] private float lookAheadDistance = 65f;

        [Header("Cadence")]
        [SerializeField, Min(1f)] private float clusterSpacingZ = 30f;
        [SerializeField, Min(0f)] private float firstClusterOffsetZ = 25f;

        [Header("Formation")]
        [SerializeField, Min(1)] private int minRows = 3;
        [SerializeField, Min(1)] private int maxRows = 6;
        [SerializeField, Min(1)] private int columns = 5;
        [SerializeField, Min(0.1f)] private float lateralSpacing = 1.1f;
        [SerializeField, Min(0.1f)] private float longitudinalSpacing = 1.2f;
        [Tooltip("Random offset applied to each zombie on X and Z.")]
        [SerializeField, Min(0f)] private float positionJitter = 0.25f;
        [Tooltip("Half width of the walkable track. Clusters are shifted sideways at random but kept inside it.")]
        [SerializeField, Min(0f)] private float trackHalfWidth = 4.25f;
        [SerializeField] private float spawnHeight;

        private static readonly Quaternion FacingSquad = Quaternion.Euler(0f, 180f, 0f);
        private float nextSpawnZ;

        private void Start()
        {
            if (playerTransform == null)
            {
                Debug.LogError("[HordeSpawner] Player Transform is not assigned.", this);
                enabled = false;
                return;
            }
            nextSpawnZ = playerTransform.position.z + firstClusterOffsetZ;
        }

        private void Update()
        {
            float horizon = playerTransform.position.z + lookAheadDistance;
            while (nextSpawnZ <= horizon)
            {
                SpawnCluster(nextSpawnZ);
                nextSpawnZ += clusterSpacingZ;
            }
        }

        private void SpawnCluster(float originZ)
        {
            ComponentPool<ZombieAgent> pool = ObjectPooler.Instance.Zombies;

            int rows = Random.Range(minRows, maxRows + 1);
            float halfWidth = (columns - 1) * lateralSpacing * 0.5f;
            float maxShift = Mathf.Max(0f, trackHalfWidth - halfWidth - lateralSpacing * 0.5f);
            float centerX = Random.Range(-maxShift, maxShift);
            float edge = trackHalfWidth + 0.5f;

            for (int r = 0; r < rows; r++)
            {
                // Offset alternate rows by half a column so the pack doesn't read as a grid.
                float stagger = (r & 1) == 1 ? lateralSpacing * 0.5f : 0f;

                for (int c = 0; c < columns; c++)
                {
                    float x = centerX - halfWidth + c * lateralSpacing + stagger + Random.Range(-positionJitter, positionJitter);
                    float z = originZ + r * longitudinalSpacing + Random.Range(-positionJitter, positionJitter);

                    var position = new Vector3(Mathf.Clamp(x, -edge, edge), spawnHeight, z);
                    pool.Spawn(position, FacingSquad).Activate(playerTransform);
                }
            }
        }

        private void OnValidate()
        {
            if (maxRows < minRows) maxRows = minRows;
        }
    }
}
