using Unity.Netcode;
using UnityEngine;

public class EnemyLemniscateController : EnemyController
{
    protected float patrolDistanceX;
    protected float patrolDistanceY;

    private Vector3 spawnPosition;
    private Vector3 lastPosition;
    private float patrolTime = 0f;

    protected override void Start()
    {
        base.Start();
        spawnPosition = transform.position;
        lastPosition = spawnPosition;
    }

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

    protected override void MoveServer()
    {
        if (!IsServer || isKnockback)
            return;

        patrolTime += Time.fixedDeltaTime * moveSpeed;

        float x = Mathf.Sin(patrolTime) * patrolDistanceX;
        float y = Mathf.Sin(patrolTime) * Mathf.Cos(patrolTime) * patrolDistanceY;

        Vector3 nextPosition = spawnPosition + new Vector3(x, y, 0);

        Vector2 direction = (nextPosition - lastPosition).normalized;
        movement = direction;

        rb.MovePosition(nextPosition);
        lastPosition = nextPosition;

        if (direction.sqrMagnitude > 0.01f)
        {
            float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, targetAngle);
        }
    }

    protected override void spawnDrops()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        base.spawnDrops();
    }
}