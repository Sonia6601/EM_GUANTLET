using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Tilemaps;

[Serializable]
public class WeightedTile
{
    public TileBase tile;

    [Range(0, 100)]
    public float weight = 1f;
}

public class TilemapFiller : MonoBehaviour
{
    /// <summary>
    /// Construye una sala cuadrada completa con tiles, muros y spawners.
    /// </summary>
    public void BuildSquareRoom(
        Tilemap tilemap,
        int size,
        WeightedTile[] weightedTiles,
        GameObject[] enemySpawners,
        GameObject wallPrefab,
        GameObject cornerPrefab,
        GameObject openDoor,
        GameObject closedDoor)
    {
        Vector2Int sizeVec = new Vector2Int(size, size);
        fillMap(tilemap, weightedTiles, sizeVec);
        spawnWalls(wallPrefab, cornerPrefab, openDoor, closedDoor, sizeVec);
        spawnEnemySpawnerAtCenter(enemySpawners, sizeVec);
    }

    /// <summary>
    /// Construye un anillo rectangular con tiles, muros, decorativos y spawners.
    /// </summary>
    public void BuildRectangularRingRoom(
        Tilemap tilemap,
        Vector2Int innerSize,
        int ringWidth,
        WeightedTile[] weightedTiles,
        GameObject[] enemySpawners,
        GameObject wallPrefab,
        GameObject cornerPrefab,
        GameObject openDoor,
        GameObject closedDoor,
        GameObject decorativeElement,
        float decorativeElementPercentage)
    {
        int outerWidth = innerSize.x + 2 * ringWidth;
        int outerHeight = innerSize.y + 2 * ringWidth;
        Vector2Int sizeVec = new Vector2Int(outerWidth, outerHeight);

        fillRingMap(tilemap, weightedTiles, innerSize, ringWidth);
        spawnWallsForRing(wallPrefab, cornerPrefab, openDoor, closedDoor, sizeVec);
        spawnDecorativeElements(decorativeElement, decorativeElementPercentage, innerSize, ringWidth);
        spawnSpawnersInRing(enemySpawners, innerSize, ringWidth);
    }

    /// <summary>
    /// Rellena un área rectangular con tiles ponderados por probabilidad.
    /// </summary>
    private void fillMap(Tilemap tilemap, WeightedTile[] weightedTiles, Vector2Int size)
    {
        Debug.Log("Filling map of size: " + size);

        if (tilemap == null || weightedTiles == null || weightedTiles.Length == 0)
        {
            Debug.LogWarning("Tilemap o lista de tiles no asignados.");
            return;
        }

        float totalWeight = weightedTiles.Sum(wt => wt.weight);
        if (totalWeight <= 0f)
        {
            Debug.LogWarning("La suma de los pesos debe ser mayor que cero.");
            return;
        }

        int xMin = Mathf.FloorToInt(-size.x / 2f);
        int xMax = xMin + size.x;
        int yMin = Mathf.FloorToInt(-size.y / 2f);
        int yMax = yMin + size.y;

        for (int x = xMin; x < xMax; x++)
        {
            for (int y = yMin; y < yMax; y++)
            {
                TileBase tile = getRandomWeightedTile(weightedTiles, totalWeight);
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    /// <summary>
    /// Rellena solo la franja de un anillo rectangular dejando el interior vacío.
    /// </summary>
    private void fillRingMap(Tilemap tilemap, WeightedTile[] weightedTiles, Vector2Int innerSize, int ringWidth)
    {
        if (tilemap == null || weightedTiles == null || weightedTiles.Length == 0)
        {
            Debug.LogWarning("Tilemap o lista de tiles no asignados.");
            return;
        }

        float totalWeight = weightedTiles.Sum(wt => wt.weight);
        if (totalWeight <= 0f)
        {
            Debug.LogWarning("La suma de los pesos debe ser mayor que cero.");
            return;
        }

        int outerWidth = innerSize.x + 2 * ringWidth;
        int outerHeight = innerSize.y + 2 * ringWidth;

        int xMin = Mathf.FloorToInt(-outerWidth / 2f);
        int xMax = xMin + outerWidth;
        int yMin = Mathf.FloorToInt(-outerHeight / 2f);
        int yMax = yMin + outerHeight;

        int innerXMin = xMin + ringWidth;
        int innerXMax = xMax - ringWidth;
        int innerYMin = yMin + ringWidth;
        int innerYMax = yMax - ringWidth;

        for (int x = xMin; x < xMax; x++)
        {
            for (int y = yMin; y < yMax; y++)
            {
                if (x >= innerXMin && x < innerXMax && y >= innerYMin && y < innerYMax)
                    continue;

                TileBase tile = getRandomWeightedTile(weightedTiles, totalWeight);
                tilemap.SetTile(new Vector3Int(x, y, 0), tile);
            }
        }
    }

    /// <summary>
    /// Devuelve un tile aleatorio aplicando el peso relativo de cada opción.
    /// </summary>
    private TileBase getRandomWeightedTile(WeightedTile[] weightedTiles, float totalWeight)
    {
        float rand = UnityEngine.Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (WeightedTile wt in weightedTiles)
        {
            cumulative += wt.weight;
            if (rand <= cumulative)
                return wt.tile;
        }

        return weightedTiles[weightedTiles.Length - 1].tile;
    }

    /// <summary>
    /// Instancia los spawners de enemigos dentro de la sala cuadrada.
    /// </summary>
    private void spawnEnemySpawnerAtCenter(GameObject[] enemySpawners, Vector2Int size)
    {
        if (enemySpawners == null || enemySpawners.Length == 0) return;

        int xMin = Mathf.FloorToInt(-size.x / 2f);
        int xMax = xMin + size.x;
        int yMin = Mathf.FloorToInt(-size.y / 2f);
        int yMax = yMin + size.y;

        for (int i = 0; i < enemySpawners.Length; i++)
        {
            int randX = UnityEngine.Random.Range(xMin + 1, xMax - 1);
            int randY = UnityEngine.Random.Range(yMin + 1, yMax - 1);
            Vector3 spawnPos = new Vector3(randX + 0.5f, randY + 0.5f, -0.1f);

            GameObject spawner = Instantiate(enemySpawners[i], spawnPos, Quaternion.identity);
            UniqueEntity uniqueEntity = spawner.GetComponent<UniqueEntity>();
            if (uniqueEntity != null) uniqueEntity.RegenerateIdOnSpawn();

            if (NetworkManager.Singleton.IsServer)
            {
                NetworkObject netObj = spawner.GetComponent<NetworkObject>();
                if (netObj != null) netObj.Spawn(true);
            }
        }
    }

    /// <summary>
    /// Instancia paredes, esquinas y puertas alrededor de una sala rectangular.
    /// </summary>
    private void spawnWalls(GameObject wallPrefab, GameObject cornerPrefab, GameObject openDoor, GameObject closedDoor, Vector2Int size)
    {
        if (wallPrefab == null) return;

        float wallOffset = 0.65f;
        float matchTilesOffset = 0.5f;

        int xMin = Mathf.FloorToInt(-size.x / 2f);
        int xMax = xMin + size.x;
        int yMin = Mathf.FloorToInt(-size.y / 2f);
        int yMax = yMin + size.y;

        Quaternion rot90 = Quaternion.Euler(0, 0, 90f);
        Quaternion rot180 = Quaternion.Euler(0, 0, 180f);
        Quaternion rot270 = Quaternion.Euler(0, 0, 270f);

        if (cornerPrefab != null)
        {
            Instantiate(cornerPrefab, new Vector3(xMin + matchTilesOffset, yMin + matchTilesOffset, -0.1f), rot90);
            Instantiate(cornerPrefab, new Vector3(xMax - 1 + matchTilesOffset, yMin + matchTilesOffset, -0.1f), rot180);
            Instantiate(cornerPrefab, new Vector3(xMin + matchTilesOffset, yMax - 1 + matchTilesOffset, -0.1f), Quaternion.identity);
            Instantiate(cornerPrefab, new Vector3(xMax - 1 + matchTilesOffset, yMax - 1 + matchTilesOffset, -0.1f), rot270);
        }

        int midX = (xMin + xMax - 1) / 2;
        int midY = (yMin + yMax - 1) / 2;

        Quaternion[] doorRotations = new Quaternion[4];
        doorRotations[0] = Quaternion.identity;
        doorRotations[1] = Quaternion.identity;
        doorRotations[2] = rot90;
        doorRotations[3] = rot90;

        int openDoorIndex = UnityEngine.Random.Range(0, 4);

        for (int x = xMin + 1; x < xMax - 1; x++)
        {
            if (x == midX)
            {
                if (openDoor == null && closedDoor == null)
                {
                    Instantiate(wallPrefab, new Vector3(x + matchTilesOffset, yMax - 1 + matchTilesOffset, -0.1f), Quaternion.identity);
                }
                else
                {
                    GameObject doorPrefab = (openDoorIndex == 0 && openDoor != null) ? openDoor : closedDoor;
                    if (doorPrefab != null)
                    {
                        GameObject door = Instantiate(doorPrefab, new Vector3(x + matchTilesOffset, yMax - 1 + matchTilesOffset, -0.1f), doorRotations[0]);
                        UniqueEntity uniqueEntity = door.GetComponent<UniqueEntity>();
                        if (uniqueEntity != null) uniqueEntity.RegenerateIdOnSpawn();
                    }
                }
            }
            else
            {
                Instantiate(wallPrefab, new Vector3(x + matchTilesOffset, yMax - 1 + matchTilesOffset, -0.1f), Quaternion.identity);
            }

            if (x == midX)
            {
                if (openDoor == null && closedDoor == null)
                {
                    Instantiate(wallPrefab, new Vector3(x + matchTilesOffset, yMin - wallOffset + matchTilesOffset, -0.1f), Quaternion.identity);
                }
                else
                {
                    GameObject doorPrefab = (openDoorIndex == 1 && openDoor != null) ? openDoor : closedDoor;
                    if (doorPrefab != null)
                    {
                        GameObject door = Instantiate(doorPrefab, new Vector3(x + matchTilesOffset, yMin - wallOffset + matchTilesOffset, -0.1f), doorRotations[1]);
                        UniqueEntity uniqueEntity = door.GetComponent<UniqueEntity>();
                        if (uniqueEntity != null) uniqueEntity.RegenerateIdOnSpawn();
                    }
                }
            }
            else
            {
                Instantiate(wallPrefab, new Vector3(x + matchTilesOffset, yMin - wallOffset + matchTilesOffset, -0.1f), Quaternion.identity);
            }
        }

        for (int y = yMin + 1; y < yMax - 1; y++)
        {
            if (y == midY)
            {
                if (openDoor == null && closedDoor == null)
                {
                    Instantiate(wallPrefab, new Vector3(xMin + matchTilesOffset, y + matchTilesOffset, -0.1f), rot90);
                }
                else
                {
                    GameObject doorPrefab = (openDoorIndex == 2 && openDoor != null) ? openDoor : closedDoor;
                    if (doorPrefab != null)
                    {
                        GameObject door = Instantiate(doorPrefab, new Vector3(xMin + matchTilesOffset, y + matchTilesOffset, -0.1f), doorRotations[2]);
                        UniqueEntity uniqueEntity = door.GetComponent<UniqueEntity>();
                        if (uniqueEntity != null) uniqueEntity.RegenerateIdOnSpawn();
                    }
                }
            }
            else
            {
                Instantiate(wallPrefab, new Vector3(xMin + matchTilesOffset, y + matchTilesOffset, -0.1f), rot90);
            }

            if (y == midY)
            {
                if (openDoor == null && closedDoor == null)
                {
                    Instantiate(wallPrefab, new Vector3(xMax - 1 + wallOffset + matchTilesOffset, y + matchTilesOffset, -0.1f), rot90);
                }
                else
                {
                    GameObject doorPrefab = (openDoorIndex == 3 && openDoor != null) ? openDoor : closedDoor;
                    if (doorPrefab != null)
                    {
                        GameObject door = Instantiate(doorPrefab, new Vector3(xMax - 1 + wallOffset + matchTilesOffset, y + matchTilesOffset, -0.1f), doorRotations[3]);
                        UniqueEntity uniqueEntity = door.GetComponent<UniqueEntity>();
                        if (uniqueEntity != null) uniqueEntity.RegenerateIdOnSpawn();
                    }
                }
            }
            else
            {
                Instantiate(wallPrefab, new Vector3(xMax - 1 + wallOffset + matchTilesOffset, y + matchTilesOffset, -0.1f), rot90);
            }
        }
    }

    /// <summary>
    /// Construye los muros de un anillo reutilizando la lógica de muros general.
    /// </summary>
    private void spawnWallsForRing(GameObject wallPrefab, GameObject cornerPrefab, GameObject openDoor, GameObject closedDoor, Vector2Int size)
    {
        spawnWalls(wallPrefab, cornerPrefab, openDoor, closedDoor, size);
    }

    /// <summary>
    /// Instancia decorativos en la franja del anillo usando un porcentaje de ocupación.
    /// </summary>
    private void spawnDecorativeElements(GameObject decorativeElement, float percentage, Vector2Int innerSize, int ringWidth)
    {
        if (decorativeElement == null || percentage <= 0f) return;

        int outerWidth = innerSize.x + 2 * ringWidth;
        int outerHeight = innerSize.y + 2 * ringWidth;

        int xMin = Mathf.FloorToInt(-outerWidth / 2f);
        int xMax = xMin + outerWidth;
        int yMin = Mathf.FloorToInt(-outerHeight / 2f);
        int yMax = yMin + outerHeight;

        int innerXMin = xMin + ringWidth;
        int innerXMax = xMax - ringWidth;
        int innerYMin = yMin + ringWidth;
        int innerYMax = yMax - ringWidth;

        int ringTiles = (outerWidth * outerHeight) - (innerSize.x * innerSize.y);
        int count = Mathf.RoundToInt(ringTiles * Mathf.Clamp01(percentage));

        Debug.Log($"[TilemapFiller] Decorativos: {count} ({percentage * 100f:F1}% de {ringTiles} tiles)");

        for (int i = 0; i < count; i++)
        {
            int randX;
            int randY;

            do
            {
                randX = UnityEngine.Random.Range(xMin + 1, xMax - 1);
                randY = UnityEngine.Random.Range(yMin + 1, yMax - 1);
            }
            while (randX >= innerXMin && randX < innerXMax && randY >= innerYMin && randY < innerYMax);

            Vector3 spawnPos = new Vector3(randX + 0.5f, randY + 0.5f, -0.1f);
            Instantiate(decorativeElement, spawnPos, Quaternion.identity);
        }
    }

    /// <summary>
    /// Instancia los spawners en la franja del anillo evitando el área interior.
    /// </summary>
    private void spawnSpawnersInRing(GameObject[] enemySpawners, Vector2Int innerSize, int ringWidth)
    {
        if (enemySpawners == null || enemySpawners.Length == 0) return;

        int outerWidth = innerSize.x + 2 * ringWidth;
        int outerHeight = innerSize.y + 2 * ringWidth;

        int xMin = Mathf.FloorToInt(-outerWidth / 2f);
        int xMax = xMin + outerWidth;
        int yMin = Mathf.FloorToInt(-outerHeight / 2f);
        int yMax = yMin + outerHeight;

        int innerXMin = xMin + ringWidth;
        int innerXMax = xMax - ringWidth;
        int innerYMin = yMin + ringWidth;
        int innerYMax = yMax - ringWidth;

        for (int i = 0; i < enemySpawners.Length; i++)
        {
            int randX;
            int randY;

            do
            {
                randX = UnityEngine.Random.Range(xMin + 1, xMax - 1);
                randY = UnityEngine.Random.Range(yMin + 1, yMax - 1);
            }
            while (randX >= innerXMin && randX < innerXMax && randY >= innerYMin && randY < innerYMax);

            Vector3 spawnPos = new Vector3(randX + 0.5f, randY + 0.5f, -0.1f);
            GameObject spawner = Instantiate(enemySpawners[i], spawnPos, Quaternion.identity);

            UniqueEntity uniqueEntity = spawner.GetComponent<UniqueEntity>();
            if (uniqueEntity != null) uniqueEntity.RegenerateIdOnSpawn();

            if (NetworkManager.Singleton.IsServer)
            {
                NetworkObject netObj = spawner.GetComponent<NetworkObject>();
                if (netObj != null) netObj.Spawn(true);
            }

        }
    }
}
