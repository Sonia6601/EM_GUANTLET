using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(Animator), typeof(UniqueEntity))]
public abstract class CharController : NetworkBehaviour
{
    [Header("Character Stats")]
    [SerializeField] protected CharacterStats stats;

    protected UniqueEntity uniqueEntity;

    protected bool isDead = false;

    protected float moveSpeed;
    protected int initialHealth;
    protected float knockbackForce;
    protected float knockbackDuration;

    protected int health;
    protected bool isKnockback = false;
    protected float knockbackTimer = 0f;

    protected Rigidbody2D rb;
    protected Animator animator;
    protected Vector2 movement;
    protected Collider2D characterCollider;

    public string EntityId => uniqueEntity?.EntityId ?? "UNKNOWN";
    public EntityType EntityType => uniqueEntity?.Type ?? EntityType.Player;

    protected virtual void Awake()
    {
        uniqueEntity = GetComponent<UniqueEntity>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        characterCollider = GetComponent<Collider2D>();

        LoadStats();
    }

    protected virtual void Start()
    {
        health = initialHealth;
    }

    protected virtual void Update()
    {
    }

    protected virtual void FixedUpdate()
    {
        if (isKnockback)
        {
            knockbackTimer -= Time.fixedDeltaTime;
            if (knockbackTimer <= 0f)
            {
                isKnockback = false;
                rb.linearVelocity = Vector2.zero;
            }
            return;
        }

        Move();
        MoveServer();
    }

    protected virtual void Move()
    {
        rb.MovePosition(rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Método virtual para movimiento. Las clases hijas lo sobrescriben.
    /// </summary>
    protected virtual void MoveServer()
    {
    }

    public virtual void Die()
    {
        if (isDead) return;

        isDead = true;
        health = 0;

        Debug.Log($"[{EntityType}:{EntityId}] {gameObject.name} died");

        animator.SetBool("IsDead", true);
        moveSpeed = 0f;

        if (characterCollider != null)
            characterCollider.enabled = false;
    }

    public virtual void TakeDamage(int amount, Vector2 knockbackDir)
    {
        if (isDead) return;
        if (amount <= 0) return;

        health -= amount;

        Debug.Log($"[{EntityType}:{EntityId}] {gameObject.name} took {amount} damage. Health: {health}/{initialHealth}");

        TakeKnockback(knockbackDir, knockbackForce);
    }

    public virtual void TakeKnockback(Vector2 knockbackDir, float customKnockbackForce = 0f)
    {
        if (isDead) return;
        if (isKnockback) return;

        float force = customKnockbackForce > 0f ? customKnockbackForce : knockbackForce;

        rb.linearVelocity = Vector2.zero;
        rb.AddForce(knockbackDir.normalized * force, ForceMode2D.Impulse);

        isKnockback = true;
        knockbackTimer = knockbackDuration;
    }

    protected virtual void LoadStats()
    {
        if (stats != null)
        {
            moveSpeed = stats.moveSpeed;
            initialHealth = stats.maxHealth;
            knockbackForce = stats.knockbackForce;
            knockbackDuration = stats.knockbackDuration;

            if (stats.animatorController != null)
                animator.runtimeAnimatorController = stats.animatorController;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] No tiene CharacterStats asignado. Usando valores por defecto.");
            moveSpeed = 3f;
            initialHealth = 99;
            knockbackForce = 10f;
            knockbackDuration = 0.2f;
        }
    }
}