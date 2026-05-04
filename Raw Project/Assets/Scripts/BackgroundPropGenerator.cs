using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundPropGenerator : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject[] groundPrefabs;
    public GameObject[] skyPrefabs;
    public GameObject groundSurfacePrefab;

    [Header("Spawn Distances")]
    public float initialSpawnAhead = 50f;
    public float spawnDistanceAhead = 220f;
    public float despawnDistanceBehind = 45f;
    public float surfaceTileLength = 30f;

    [Header("Spawn Density")]
    public float spawnStepZ = 10f;
    [Range(0, 5)] public int maxGroundPerStep = 2;
    [Range(0, 5)] public int maxSkyPerStep = 1;
    [Range(0f, 1f)] public float groundSpawnChance = 0.65f;
    [Range(0f, 1f)] public float skySpawnChance = 0.4f;

    [Header("Ground / Sky Placement")]
    public float horizontalRange = 30f;
    public float groundY = -22f;
    public float skyY = 22f;
    public float yJitter = 2.5f;
    public float zJitter = 2f;
    public float surfaceY = -24f;

    [Header("Randomization")]
    public bool randomYRotation = true;
    public Vector2 randomScaleRange = new Vector2(0.9f, 1.2f);
    public Vector3 surfaceScale = Vector3.one;

    private readonly List<GameObject> activeProps = new List<GameObject>();
    private readonly List<GameObject> activeSurfaceTiles = new List<GameObject>();
    private float nextSpawnZ;
    private float nextSurfaceZ;

    void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (player == null)
        {
            enabled = false;
            return;
        }

        nextSpawnZ = player.position.z + initialSpawnAhead;
        nextSurfaceZ = player.position.z - despawnDistanceBehind;

        SpawnSurfaceUpTo(player.position.z + spawnDistanceAhead);

        while (nextSpawnZ < player.position.z + spawnDistanceAhead)
        {
            SpawnAtZ(nextSpawnZ);
            nextSpawnZ += spawnStepZ;
        }
    }

    void Update()
    {
        if (player == null) return;

        while (nextSpawnZ < player.position.z + spawnDistanceAhead)
        {
            SpawnAtZ(nextSpawnZ);
            nextSpawnZ += spawnStepZ;
        }

        SpawnSurfaceUpTo(player.position.z + spawnDistanceAhead);

        CleanupBehindPlayer();
    }

    private void SpawnSurfaceUpTo(float targetZ)
    {
        if (groundSurfacePrefab == null) return;

        while (nextSurfaceZ < targetZ)
        {
            Vector3 pos = new Vector3(0f, surfaceY, nextSurfaceZ + surfaceTileLength * 0.5f);
            GameObject tile = Instantiate(groundSurfacePrefab, pos, Quaternion.identity);
            tile.transform.localScale = Vector3.Scale(tile.transform.localScale, surfaceScale);
            activeSurfaceTiles.Add(tile);

            nextSurfaceZ += surfaceTileLength;
        }
    }

    private void SpawnAtZ(float z)
    {
        SpawnBand(groundPrefabs, maxGroundPerStep, groundSpawnChance, groundY, z);
        SpawnBand(skyPrefabs, maxSkyPerStep, skySpawnChance, skyY, z);
    }

    private void SpawnBand(GameObject[] prefabs, int maxCountPerStep, float spawnChance, float baseY, float z)
    {
        if (prefabs == null || prefabs.Length == 0 || maxCountPerStep <= 0) return;

        for (int i = 0; i < maxCountPerStep; i++)
        {
            if (Random.value > spawnChance) continue;

            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) continue;

            float x = Random.Range(-horizontalRange, horizontalRange);
            float y = baseY + Random.Range(-yJitter, yJitter);
            float spawnZ = z + Random.Range(-zJitter, zJitter);

            Vector3 pos = new Vector3(x, y, spawnZ);

            Quaternion rot = Quaternion.identity;
            if (randomYRotation)
            {
                rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            }

            GameObject prop = Instantiate(prefab, pos, rot);

            float scale = Random.Range(randomScaleRange.x, randomScaleRange.y);
            prop.transform.localScale *= scale;

            activeProps.Add(prop);
        }
    }

    private void CleanupBehindPlayer()
    {
        float despawnZ = player.position.z - despawnDistanceBehind;

        for (int i = activeProps.Count - 1; i >= 0; i--)
        {
            GameObject prop = activeProps[i];
            if (prop == null)
            {
                activeProps.RemoveAt(i);
                continue;
            }

            if (prop.transform.position.z < despawnZ)
            {
                Destroy(prop);
                activeProps.RemoveAt(i);
            }
        }

        for (int i = activeSurfaceTiles.Count - 1; i >= 0; i--)
        {
            GameObject tile = activeSurfaceTiles[i];
            if (tile == null)
            {
                activeSurfaceTiles.RemoveAt(i);
                continue;
            }

            if (tile.transform.position.z + surfaceTileLength * 0.5f < despawnZ)
            {
                Destroy(tile);
                activeSurfaceTiles.RemoveAt(i);
            }
        }
    }
}
