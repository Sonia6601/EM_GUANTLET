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

    /// <summary>
    /// Inicializa componentes y carga estadísticas del personaje.
    /// </summary>
    protected virtual void Awake()
    {
        uniqueEntity = GetComponent<UniqueEntity>();
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        characterCollider = GetComponent<Collider2D>();

        LoadStats();
    }

    /// <summary>
    /// Inicializa la vida actual con la vida inicial configurada.
    /// </summary>
    protected virtual void Start()
    {
        health = initialHealth;
    }

    /// <summary>
    /// Gestiona la lógica por frame de las clases derivadas.
    /// </summary>
    protected virtual void Update()
    {
    }

    /// <summary>
    /// Gestiona knockback activo y movimiento físico del personaje.
    /// </summary>
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
    }

    /// <summary>
    /// Marca el personaje como muerto y desactiva su interacción física.
    /// </summary>
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

    /// <summary>
    /// Aplica daño al personaje y activa el knockback asociado.
    /// </summary>
    public virtual void TakeDamage(int amount, Vector2 knockbackDir)
    {
        if (isDead) return;
        if (amount <= 0) return;

        health -= amount;

        Debug.Log($"[{EntityType}:{EntityId}] {gameObject.name} took {amount} damage. Health: {health}/{initialHealth}");

        TakeKnockback(knockbackDir, knockbackForce);
    }

    /// <summary>
    /// Aplica una fuerza de knockback al personaje si procede.
    /// </summary>
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

    /// <summary>
    /// Carga estadísticas base desde el ScriptableObject o aplica valores por defecto.
    /// </summary>
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

    /// <summary>
    /// Desplaza al personaje según su vector de movimiento y velocidad.
    /// </summary>
    /// 
    protected virtual void Move()
    {
        rb.MovePosition(rb.position + movement.normalized * moveSpeed * Time.fixedDeltaTime);
    }
}


////////////// TO DO LIST //////////////
/// 
/// eventos de animación para muerte de enemigos
/// Usar eventos de animación para sincronizar destrucción y finalización de ataques.
/// sonidos
/// Reordenación de código (métodos y propiedades públicas al final, privados al principio)
/// Documentación del proyecto (README, diagramas, comentarios en código, etc.)
/// modificar la generación aleatoria del mapa para que encaje con el modo de semilla (seed) y sea reproducible

