using System.Collections.Generic;
using UnityEngine;

public class RoadGenerator : MonoBehaviour
{
    [Header("Настройки дороги")]
    public GameObject roadRingPrefab;
    public Transform player;
    public float segmentLength = 10f;
    public int segmentsToSpawn = 10;

    [Header("Спавн препятствий и врагов")]
    public GameObject[] enemyPrefabs;
    public GameObject[] obstaclePrefabs;
    [Range(0f, 1f)]
    public float spawnChance = 0.6f;
    public float spawnRadius = 4f;
    public float maxLaneOffset = 3.5f;

    private float spawnZ = 0f;
    private float safeZone = 25f;
    private List<GameObject> activeRings = new List<GameObject>();

    void Start()
    {
        for (int i = 0; i < segmentsToSpawn; i++)
        {
            SpawnRing(canSpawnObstacles: i > 3);
        }
    }

    void Update()
    {
        if (player.position.z - safeZone > activeRings[0].transform.position.z)
        {
            SpawnRing(true);
            DeleteOldestRing();
        }
    }

    void SpawnRing(bool canSpawnObstacles)
    {
        GameObject newRing = Instantiate(roadRingPrefab, new Vector3(0, 0, spawnZ), Quaternion.identity);
        activeRings.Add(newRing);

        if (canSpawnObstacles && Random.value <= spawnChance)
            SpawnRandomObjectOnRing(newRing.transform);

        spawnZ += segmentLength;
    }

    void SpawnRandomObjectOnRing(Transform ringTransform)
    {
        bool spawnEnemy = Random.value > 0.5f && enemyPrefabs.Length > 0;
        GameObject prefabToSpawn = null;

        if (spawnEnemy)
            prefabToSpawn = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
        else if (obstaclePrefabs.Length > 0)
            prefabToSpawn = obstaclePrefabs[Random.Range(0, obstaclePrefabs.Length)];

        if (prefabToSpawn == null) return;

        int randomWall = Random.Range(0, 4);
        float angleZ = randomWall * 90f;
        float randomXOffset = Random.Range(-maxLaneOffset, maxLaneOffset);

        EnemyController isEnemy = prefabToSpawn.GetComponent<EnemyController>();

        if (isEnemy != null)
        {
            GameObject spawnedObj = Instantiate(prefabToSpawn, new Vector3(0, 0, ringTransform.position.z), Quaternion.identity);
            EnemyController enemy = spawnedObj.GetComponent<EnemyController>();
            enemy.InitSpawn(randomXOffset, spawnRadius, angleZ);
        }
        else
        {
            GameObject spawnedObj = Instantiate(prefabToSpawn, ringTransform);
            Vector3 localPos = new Vector3(randomXOffset, -spawnRadius, 0);

            spawnedObj.transform.localRotation = Quaternion.Euler(0, 0, angleZ) * prefabToSpawn.transform.localRotation;
            spawnedObj.transform.localPosition = Quaternion.Euler(0, 0, angleZ) * localPos;
        }
    }

    void DeleteOldestRing()
    {
        Destroy(activeRings[0]);
        activeRings.RemoveAt(0);
    }
}