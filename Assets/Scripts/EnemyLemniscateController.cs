using Unity.Netcode;
using UnityEngine;

public class EnemyLemniscateController : EnemyController
{
    protected float patrolDistanceX;
    protected float patrolDistanceY;

    private Vector3 spawnPosition;
    private Vector3 lastPosition;
    private float patrolTime = 0f;

    /// <summary>
    /// Inicializa la posición base de patrulla en lemniscata.
    /// </summary>
    protected override void Start()
    {
        base.Start();
        spawnPosition = transform.position;
        lastPosition = spawnPosition;
    }

    /// <summary>
    /// Carga las estadísticas específicas del movimiento en lemniscata.
    /// </summary>
    protected override void LoadStats()
    {
        base.LoadStats();

        LemniscateEnemyStats lemniscateStats = stats as LemniscateEnemyStats;

        if (lemniscateStats != null)
        {
            patrolDistanceX = lemniscateStats.patrolDistanceX;
            patrolDistanceY = lemniscateStats.patrolDistanceY;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No tiene LemniscateEnemyStats asignado. Usando valores por defecto.");
            patrolDistanceX = 2f;
            patrolDistanceY = 1f;
        }
    }

    /// <summary>
    /// Calcula y aplica el desplazamiento del enemigo sobre una trayectoria en lemniscata.
    /// </summary>
    /// 
    protected override void Move()
    {
        if (isKnockback)
        {
            lastPosition = transform.position;
            return;
        }

        patrolTime += Time.fixedDeltaTime * moveSpeed;

        float x = Mathf.Sin(patrolTime) * patrolDistanceX;
        float y = Mathf.Sin(patrolTime) * Mathf.Cos(patrolTime) * patrolDistanceY;

        Vector3 newPosition = spawnPosition + new Vector3(x, y, 0f);
        rb.MovePosition(newPosition);

        Vector2 movementDir = newPosition - lastPosition;
        float angle = Mathf.Atan2(movementDir.y, movementDir.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        lastPosition = newPosition;
    }

    /// <summary>
    /// Aplica daño y reajusta la fase de patrulla tras finalizar el knockback.
    /// </summary>
    public override void TakeDamage(int amount, Vector2 knockbackDir)
    {
        base.TakeDamage(amount, knockbackDir);
        StartCoroutine(recalculatePatrolTimeAfterKnockback());
    }

    /// <summary>
    /// Espera al fin del knockback y recalcula la fase de patrulla para evitar saltos.
    /// </summary>
    private System.Collections.IEnumerator recalculatePatrolTimeAfterKnockback()
    {
        while (isKnockback)
            yield return null;

        spawnPosition = transform.position;
        patrolTime = getBestPatrolTime(transform.position, spawnPosition, patrolDistanceX, patrolDistanceY);
    }

    /// <summary>
    /// Encuentra la fase de patrulla que mejor aproxima la posición actual del enemigo.
    /// </summary>
    private float getBestPatrolTime(Vector3 currentPosition, Vector3 origin, float distX, float distY)
    {
        float bestT = patrolTime;
        float minDist = float.MaxValue;

        for (float t = 0f; t < Mathf.PI * 2f; t += 0.01f)
        {
            float x = Mathf.Sin(t) * distX;
            float y = Mathf.Sin(t) * Mathf.Cos(t) * distY;
            Vector3 candidate = origin + new Vector3(x, y, 0f);

            float dist = Vector3.Distance(currentPosition, candidate);
            if (dist < minDist)
            {
                minDist = dist;
                bestT = t;
            }
        }

        return bestT;
    }
}
