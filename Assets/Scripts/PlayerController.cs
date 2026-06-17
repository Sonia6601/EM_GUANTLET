using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : CharController
{
    protected int damageToEnemy;
    protected float attackCooldown;

    private float horizontalInput;         // Entrada horizontal (A/D o flechas)
    private float verticalInput;           // Entrada vertical (W/S o flechas)

    //public float moveSpeed = 5f;           // Velocidad de movimiento


    private PlayerControls controls;

    public bool IsAttacking { get; private set; } = false;
    public int DamageToEnemy => damageToEnemy;
    public NetworkVariable<Vector2> Position = new NetworkVariable<Vector2>();
    public NetworkVariable<float> moveSpeedSync = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private readonly NetworkVariable<int> characterIndexSync = new NetworkVariable<int>(
    0,
    NetworkVariableReadPermission.Everyone,
    NetworkVariableWritePermission.Owner
);
    private NetworkVariable<int> initialHealth_online = new NetworkVariable<int>(99, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> health_online = new NetworkVariable<int>(99, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    /// <summary>
    /// Inicializa controles de entrada y registra el jugador local en el gestor global.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        controls = new PlayerControls();


        // ✅ Ocultar hasta que LevelGenerator lo reposicione
        Debug.LogWarning("[AWAKE PLAYER CONTROLLER] SPRITE RENDERER DESACTVADO - NO SE VE");
        //gameObject.SetActive(false);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        
    }


    public override void OnNetworkSpawn()
    {

        base.OnNetworkSpawn();

        characterIndexSync.OnValueChanged += OnCharacterIndexChanged;
        health_online.OnValueChanged += OnHealthChanged;
        Debug.LogWarning("[ON NETWORK SPAWN] VOY A HACER LA CONEXION RED");
        //gameObject.SetActive(true);
        
        //var sr = GetComponent<SpriteRenderer>();
        
        if (IsOwner)
        {
            UniqueEntity uniqueEntity = GetComponent<UniqueEntity>();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RegisterLocalPlayer(this, uniqueEntity);
                characterIndexSync.Value = GameManager.Instance.GetSelectedCharacterIndex();
            }
            ActualizarAspectoVisual(characterIndexSync.Value);

        }
        else
        {
            if (characterIndexSync.Value!=0)
            {
                ActualizarAspectoVisual(characterIndexSync.Value);
            }
        }

            string escenaActual = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (escenaActual == SceneNames.PlaygroundLevel)
        {
            //if (sr != null) sr.enabled = true;
            // Dispara eventos iniciales para actualizar el HUD
            GameEvents.HealthChanged(health_online.Value);
            GameEvents.KeysChanged();
            GameEvents.DiamondsChanged();

            IsAttacking = false;
        }
        
        
        
    }
    private void OnCharacterIndexChanged(int previousValue, int newValue)
    {
        ActualizarAspectoVisual(newValue);
    }
    private void ActualizarAspectoVisual(int index)
    {
        if (GameManager.Instance == null) return;

        PlayerStats statsAsignadas = GameManager.Instance.GetCharacterStatsByIndex(index);

        if (statsAsignadas == null) return;

        stats = statsAsignadas;

        if (IsOwner)
        {
            // Le aplicamos las stats mecánicas al CharController base
            damageToEnemy = statsAsignadas.attackDamage;
            attackCooldown = statsAsignadas.attackCooldown;
            initialHealth = statsAsignadas.maxHealth;
        }

        if (!IsSpawned) health_online = initialHealth_online; // Solo inicializa vida si está naciendo

        if (IsServer)
        {
            health_online.Value = initialHealth;
            health = health_online.Value; // Sincroniza la variable local del CharController base
        }

        // Cambiamos el color/animaciones en el renderizador
        if (statsAsignadas.animatorController != null && animator != null)
        {
            animator.runtimeAnimatorController = statsAsignadas.animatorController;
            Debug.Log($"[NETCODE SUCCESS] {gameObject.name} visualmente sincronizado al personaje index: {index} ({statsAsignadas.characterName})");
        }

        // Forzamos que el SpriteRenderer se encienda por si acaso el Awake lo dejó oculto
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        health = newValue;

        if (IsOwner)
        {
            GameEvents.HealthChanged(newValue);

        }

    }


    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        characterIndexSync.OnValueChanged -= OnCharacterIndexChanged;
        health_online.OnValueChanged -= OnHealthChanged;
    }


    /// <summary>
    /// Inicializa estado del jugador y notifica los valores iniciales al HUD.
    /// </summary>
    protected override void Start()
    {
        base.Start(); 

        gameObject.SetActive(false);
        Debug.LogWarning("[START PLAYER CONTROLLER] JUGADOR DESACTIVADO");
    }



    /// <summary>
    /// Actualiza animación, orientación y estado de vida en cada frame.
    /// </summary>
    protected override void Update()
    {

        if (!IsOwner) return; //si no eres el jugador no puedes mover el jugador
        if (!IsSpawned || health_online.Value == 0) return;

        if (isDead)
        {
            movement = Vector2.zero;
            if (rb != null) rb.linearVelocity = Vector2.zero;
            animator.SetFloat("speed", 0f);
            return;
        }

        animator.SetFloat("speed", movement.sqrMagnitude);

        if (movement.sqrMagnitude > 0.01f)
        {
            float angle = Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, angle - 90f);
        }

        if (!IsSpawned || health_online.Value == 0) return; //si aun no se ha spawneado no se mira si ha muerto (puede aparecer muerto por lag)
        checkDeath();
    }


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
    //void SendMovementToServerRpc(Vector3 pos, Quaternion rot)
    //{
    //    BroadcastTransformClientRpc(pos, rot);
    //}
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
    protected override void FixedUpdate()
    {

        base.FixedUpdate();


    }
    protected override void Move()
    {
        //Si no es el duenno o no está spawneado, no hagas nada
        if (!IsOwner || !IsSpawned) return;

        //Si la escena actual no es la de juego, no hagas nada
        string escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (escena != SceneNames.PlaygroundLevel) return;


        base.Move();
    }

    [Rpc(SendTo.Everyone)]
    public void ActivarPersonajeRpc(Vector3 pos)
    {
        transform.position = pos;
        gameObject.SetActive(true);
        //Debug.LogWarning("Host Activado");
        //Decirle al server que active a los persoanjes
        NotificarActivacionRpc();

    }

    [Rpc(SendTo.Server)]
    public void NotificarActivacionRpc()
    {
        //El server activa el networkObject en todos los clientes
        ActivarPersonajesRpc();
    }

    [Rpc(SendTo.Everyone)]
    public void ActivarPersonajesRpc()
    {
        gameObject.SetActive(true);
        //Debug.LogWarning("Personajes Activados");
    }

    //void MovePlayer()
    //{

    //    // Calcular la dirección de movimiento en relación a la cámara
    //    Vector3 moveDirection = new Vector3(verticalInput, horizontalInput, 0);
    //    moveDirection.y = 0f; // Asegurarnos de que el movimiento es horizontal (sin componente Y)

    //    // Mover el jugador usando el Transform
    //    if (moveDirection != Vector3.zero)
    //    {
    //        // Calcular la rotación en Y basada en la dirección del movimiento
    //        Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
    //        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, 720f * Time.fixedDeltaTime);

    //        // Ajustar la velocidad si es zombie
    //        float adjustedSpeed = moveSpeed;

    //        // Mover al jugador en la dirección deseada
    //        transform.Translate(moveDirection * adjustedSpeed * Time.fixedDeltaTime, Space.World);

    //        MoveRequestRpc(transform.position, transform.rotation);
    //    }
    //}

    //[Rpc(SendTo.ClientsAndHost)]
    //void MoveRequestRpc(Vector3 pos, Quaternion rot)
    //{
    //    this.transform.position = pos;
    //    this.transform.rotation = rot;
    //}

    void LateUpdate()
    {
        if (!IsOwner)
        {
            animator.SetFloat("speed", moveSpeedSync.Value);
        }
    }


    /// <summary>
    /// Activa el mapa de controles y suscribe la acción de ataque.
    /// </summary>
    private void OnEnable()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;

        if (!IsOwner) return;

        if (controls == null) controls = new PlayerControls();

        //Activamos inputs
        controls.Enable();

        //Suscribimos movimientos del personaje
        controls.Player.Move.performed += OnMovePerformed;
        controls.Player.Move.canceled += OnMoveCanceled;

        //Sucribimos el ataque
        controls.Player.Attack.performed += onAttack;
    }

    /// <summary>
    /// Desuscribe la acción de ataque y desactiva el mapa de controles.
    /// </summary>
    private void OnDisable()
    {
        if (controls != null)
        {
            //Desuscribimos el movimiento del personaje
            controls.Player.Move.performed -= OnMovePerformed;
            controls.Player.Move.canceled -= OnMoveCanceled;

            //Desiscribimos el ataque
            controls.Player.Attack.performed -= onAttack;

            //Desactivamos los inputs
            controls.Disable();
        }
    }

    /// <summary>
    /// Gestiona la muerte del jugador y lanza el flujo de fin de partida.
    /// </summary>
    public override void Die()
    {

        base.Die();

        // Dispara evento de muerte
        GameEvents.PlayerDied();

        //GameManager.Instance?.TriggerGameOver();

    }

    /// <summary>
    /// Aplica daño al jugador y notifica el cambio de salud al HUD.
    /// </summary>
    public override void TakeDamage(int amount, Vector2 knockbackDir)
    {
        if (isDead) return;
        if (amount <= 0) return;

        if (IsServer)
        {
            health_online.Value -= amount;
            health = health_online.Value;
        }

        base.TakeDamage(amount, knockbackDir);

        // Dispara evento de cambio de salud
        //GameEvents.HealthChanged(health_online.Value);
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
        if (IsOwner && GameManager.Instance != null && GameManager.Instance.SelectedCharacterStats != null)
        {
            stats = GameManager.Instance.SelectedCharacterStats;
            Debug.Log($"[PlayerController] Cargando personaje seleccionado: {stats.characterName}");
        }

        // Si no hay personaje seleccionado, usa el asignado en el prefab (fallback)

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
        //Debug.Log($"[checkDeath] health_online: {health_online} | isDead: {isDead}");
        if (health_online.Value <= 0 && !isDead)
        {
            Die();
        }
    }

    /// <summary>
    /// Inicia la animación de ataque y programa su final según el cooldown.
    /// </summary>
    private void onAttack(InputAction.CallbackContext context)
    {
        if (!IsOwner || isDead) return;
        animator.SetTrigger("Attack");
        IsAttacking = true;
        Invoke(nameof(endAttack), attackCooldown);
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || isDead) return;
        movement = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || isDead) return;
        movement = Vector2.zero;
    }

    /// <summary>
    /// Finaliza el estado de ataque del jugador.
    /// </summary>
    private void endAttack()
    {
        IsAttacking = false;
    }
}