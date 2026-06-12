using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : CharController
{
    protected int damageToEnemy;
    protected float attackCooldown;

    private float horizontalInput;         // Entrada horizontal (A/D o flechas)
    private float verticalInput;           // Entrada vertical (W/S o flechas)

    public float moveSpeed = 5f;           // Velocidad de movimiento


    private PlayerControls controls;

    public bool IsAttacking { get; private set; } = false;
    public int DamageToEnemy => damageToEnemy;
    public NetworkVariable<Vector2> Position = new NetworkVariable<Vector2>();
    public NetworkVariable<float> moveSpeedSync = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


    /// <summary>
    /// Inicializa controles de entrada y registra el jugador local en el gestor global.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        controls = new PlayerControls();

        controls.Player.Move.performed += ctx => movement = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += _ => movement = Vector2.zero;


        gameObject.SetActive(false);

        UniqueEntity uniqueEntity = GetComponent<UniqueEntity>();
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterLocalPlayer(this, uniqueEntity);
    }


    public override void OnNetworkSpawn()
    {

        base.OnNetworkSpawn();

        // Dispara eventos iniciales para actualizar el HUD
        GameEvents.HealthChanged(health);
        GameEvents.KeysChanged();
        GameEvents.DiamondsChanged();

        IsAttacking = false;
    }


    /// <summary>
    /// Inicializa estado del jugador y notifica los valores iniciales al HUD.
    /// </summary>
    protected override void Start()
    {
        base.Start(); 

        gameObject.SetActive(false);
        Debug.LogWarning("[START PLAYER CONTROLLER] JUGADOR DESACTIVADO");

        if (IsOwner)
        {
            //SendDirectionToServerRpc(transform.position);
            Debug.LogWarning("[START PLAYER CONTROLLER] ENVIANDO MOVIMIENTO");

        }
    }

    

    /// <summary>
    /// Actualiza animación, orientación y estado de vida en cada frame.
    /// </summary>
    protected override void Update()
    {

        if (!IsOwner) return; 
        animator.SetFloat("speed", movement.sqrMagnitude);

        if (movement.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
            transform.position = Position.Value;
        }

        checkDeath();
    }


    private void FixedUpdate()
    {
        if (!IsOwner) return; //si no eres el dueño del script no mueves nada


    //private void FixedUpdate()
    //{
    //    if (!IsOwner || !IsSpawned) return; //si no eres el dueño del script no mueves nada
    //    string escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
    //    if (escena != SceneNames.PlaygroundLevel) return;

    //    //var sr = GetComponent<SpriteRenderer>();
    //    //if (sr != null && !sr.enabled) return; 

    //    Vector2 movimiento = movement.normalized;


    //    if (movimiento.sqrMagnitude > 0.01f)
    //    {
    //        //Se mueve el jugador localmente para respuesta inmediata
    //        transform.Translate((Vector3)movimiento * moveSpeed * Time.fixedDeltaTime, Space.World);

    //        //Notificar al servidor
    //        //SendMovementToServerRpc(transform.position, transform.rotation);
    //    }



    //}


    //[ServerRpc]
    //void SendDirectionToServerRpc(Vector3 moveDirection)
    //{
    //    if (moveDirection == Vector3.zero) return;

    //    Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
    //    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.fixedDeltaTime);

    //    float adjustedSpeed = moveSpeed;
    //    transform.Translate(moveDirection * adjustedSpeed * Time.fixedDeltaTime, Space.World);

    //    BroadcastTransformClientRpc(transform.position, transform.rotation);
    //}

    //[ClientRpc]
    //void BroadcastTransformClientRpc(Vector3 pos, Quaternion rot)
    //{
    //    if (IsOwner) return;

    //    transform.position = pos;
    //    transform.rotation = rot;
    //}
    void MovePlayer()
    {

        // Calcular la dirección de movimiento en relación a la cámara
        Vector3 moveDirection = new Vector3(verticalInput, horizontalInput,0);
        moveDirection.y = 0f; // Asegurarnos de que el movimiento es horizontal (sin componente Y)

        // Mover el jugador usando el Transform
        if (moveDirection != Vector3.zero)
        {
            // Calcular la rotación en Y basada en la dirección del movimiento
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.fixedDeltaTime);

            // Ajustar la velocidad si es zombie
            float adjustedSpeed = moveSpeed;

            // Mover al jugador en la dirección deseada
            transform.Translate(moveDirection * adjustedSpeed * Time.fixedDeltaTime, Space.World);

            MoveRequestRpc(transform.position, transform.rotation);
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    void MoveRequestRpc(Vector3 pos, Quaternion rot)
    {
        this.transform.position = pos;
        this.transform.rotation = rot;
    }

    void LateUpdate()
    {
        if (!IsOwner)
        {
            animator.SetFloat("Speed", moveSpeedSync.Value);
        }
    }

    /// <summary>
    /// Activa el mapa de controles y suscribe la acción de ataque.
    /// </summary>
    private void OnEnable()
    {
        controls.Enable();
        controls.Player.Attack.performed += onAttack;
    }

    /// <summary>
    /// Desuscribe la acción de ataque y desactiva el mapa de controles.
    /// </summary>
    private void OnDisable()
    {
        controls.Player.Attack.performed -= onAttack;
        controls.Disable();
    }

    /// <summary>
    /// Gestiona la muerte del jugador y lanza el flujo de fin de partida.
    /// </summary>
    public override void Die()
    {
        base.Die();

        // Dispara evento de muerte
        GameEvents.PlayerDied();

        GameManager.Instance?.TriggerGameOver();

    }

    /// <summary>
    /// Aplica daño al jugador y notifica el cambio de salud al HUD.
    /// </summary>
    public override void TakeDamage(int amount, Vector2 knockbackDir)
    {
        base.TakeDamage(amount, knockbackDir);

        // Dispara evento de cambio de salud
        GameEvents.HealthChanged(health);
    }

    /// <summary>
    /// Aplica un conjunto de estadísticas de personaje y recarga sus valores activos.
    /// </summary>
    public void ApplyCharacterStats(PlayerStats newStats)
    {
        if (newStats == null)
        {
            Debug.LogWarning("[PlayerController] ApplyCharacterStats llamado con null");
            return;
        }

        stats = newStats;

        // Recargar todas las stats
        LoadStats();

        Debug.Log($"[PlayerController] Stats aplicadas: {newStats.characterName}");
    }

    /// <summary>
    /// Carga estadísticas del personaje seleccionado y aplica valores de combate y movimiento.
    /// </summary>
    protected override void LoadStats()
    {
        //  PRIMERO: Intenta cargar desde GameManager (personaje seleccionado)
        if (GameManager.Instance != null && GameManager.Instance.SelectedCharacterStats != null)
        {
            stats = GameManager.Instance.SelectedCharacterStats;
            Debug.Log($"[PlayerController] Cargando personaje seleccionado: {stats.characterName}");
        }

        // Si no hay personaje seleccionado, usa el asignado en el prefab (fallback)
        if (stats == null)
        {
            Debug.LogWarning("[PlayerController] No hay personaje seleccionado, usando stats por defecto del prefab");
        }

        base.LoadStats();

        //  Haz casting del campo heredado
        PlayerStats playerStats = stats as PlayerStats;

        if (playerStats != null)
        {
            // Aplica el bonus de velocidad del jugador
            moveSpeed *= playerStats.speedBonus;
            
            // Carga stats específicas del jugador
            damageToEnemy = playerStats.attackDamage;
            attackCooldown = playerStats.attackCooldown;
        }
        else
        {
            // Valores por defecto si no hay PlayerStats
            Debug.LogWarning($"[{gameObject.name}] No tiene PlayerStats asignado. Usando valores por defecto.");
            damageToEnemy = 50;
            attackCooldown = 0.5f;
            moveSpeed *= 1.25f; // Bonus por defecto
        }
    }

    /// <summary>
    /// Verifica si la salud ha llegado a cero y ejecuta la muerte una sola vez.
    /// </summary>
    private void checkDeath()
    {
        if (health <= 0 && !isDead)
        {
            Die();
        }
    }

    /// <summary>
    /// Inicia la animación de ataque y programa su final según el cooldown.
    /// </summary>
    private void onAttack(InputAction.CallbackContext context)
    {
        animator.SetTrigger("Attack");
        IsAttacking = true;
        Invoke(nameof(endAttack), attackCooldown);
    }

    /// <summary>
    /// Finaliza el estado de ataque del jugador.
    /// </summary>
    private void endAttack()
    {
        IsAttacking = false;
    }
}
