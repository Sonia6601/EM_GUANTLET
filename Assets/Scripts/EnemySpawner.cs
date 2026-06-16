using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(UniqueEntity))]
public class EnemySpawner : NetworkBehaviour
{
    [Header("Configuración del Spawner")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private int totalEnemies = 5;
    [SerializeField] private float spawnInterval = 2f;

    [Header("Opciones de área de spawn")]
    [SerializeField] private bool spawnInArea = false;
    [SerializeField] private float spawnRadius = 3f;

    private int spawnedCount = 0;
    private float timer = 0f;

    /// <summary>
    /// Inicializa el temporizador de aparición de enemigos.
    /// </summary>
    private void Start()
    {
        timer = spawnInterval;
    }

    /// <summary>
    /// Controla el intervalo de aparición y limita el número total de enemigos.
    /// </summary>
    private void Update()
    {
        if (!IsServer||enemyPrefab == null || spawnedCount >= totalEnemies)
            return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            spawnEnemy();
            spawnedCount++;
            timer = spawnInterval;
        }
    }

    /// <summary>
    /// Instancia un enemigo en la posición del spawner o dentro del radio configurado.
    /// </summary>
    private void spawnEnemy()
    {
        Vector3 spawnPos = transform.position;

        if (spawnInArea)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float dist = Random.Range(0f, spawnRadius);
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
            spawnPos += new Vector3(offset.x, offset.y, 0f);
        }

        GameObject enemy = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);

        UniqueEntity uniqueEntity = enemy.GetComponent<UniqueEntity>();
        if (uniqueEntity != null)
            uniqueEntity.RegenerateIdOnSpawn();

        NetworkObject netObj= enemy.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.Spawn(true);
        }
    }
}
