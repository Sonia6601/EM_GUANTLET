using Unity.Netcode;
using UnityEngine;

public class EnemyChaseController : EnemyController
{
    protected float chaseRange;
    protected float wanderChangeInterval;
    protected float wanderSpeedMin;
    protected float wanderSpeedMax;
    protected float idleChance;

    private Transform playerTransform;
    private Vector2 wanderDirection;
    private float wanderSpeed;
    private float wanderTimer;

    protected override void Start()
    {
        base.Start();

        if (IsServer)
        {
            setNewWanderDirection();
        }
    }

    protected override void LoadStats()
    {
        base.LoadStats();

        ChaseEnemyStats chaseStats = stats as ChaseEnemyStats;

        if (chaseStats != null)
        {
            chaseRange = chaseStats.chaseRange;
            wanderChangeInterval = chaseStats.wanderChangeInterval;
            wanderSpeedMin = chaseStats.wanderSpeedMin;
            wanderSpeedMax = chaseStats.wanderSpeedMax;
            idleChance = chaseStats.idleChance;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No tiene ChaseEnemyStats asignado. Usando valores por defecto.");
            chaseRange = 10f;
            wanderChangeInterval = 2f;
            wanderSpeedMin = 0.3f;
            wanderSpeedMax = 0.7f;
            idleChance = 0.2f;
        }
    }

    protected override void MoveServer()
    {
        if (!IsServer || isKnockback)
            return;

        FindClosestPlayer();

        if (playerTransform == null)
        {
            wanderMovement();
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > chaseRange)
            wanderMovement();
        else
            chasePlayer();
    }

    private void chasePlayer()
    {
        Vector2 direction = (playerTransform.position - transform.position).normalized;
        movement = direction;

        rb.linearVelocity = direction * moveSpeed;

        if (direction.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void wanderMovement()
    {
        wanderTimer -= Time.fixedDeltaTime;

        if (wanderTimer <= 0f)
            setNewWanderDirection();

        rb.linearVelocity = wanderDirection * wanderSpeed;

        if (wanderDirection.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(wanderDirection.y, wanderDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }

    private void setNewWanderDirection()
    {
        if (Random.value < idleChance)
        {
            wanderDirection = Vector2.zero;
            wanderSpeed = 0f;
        }
        else
        {
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            wanderDirection = new Vector2(Mathf.Cos(randomAngle), Mathf.Sin(randomAngle));
            wanderSpeed = Random.Range(moveSpeed * wanderSpeedMin, moveSpeed * wanderSpeedMax);
        }

        wanderTimer = wanderChangeInterval;
    }

    private void FindClosestPlayer()
    {
        PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        float closestDistance = float.MaxValue;
        Transform closestPlayer = null;

        foreach (var player in allPlayers)
        {
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPlayer = player.transform;
            }
        }

        playerTransform = closestPlayer;
    }

    protected override void spawnDrops()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        base.spawnDrops();
    }
}