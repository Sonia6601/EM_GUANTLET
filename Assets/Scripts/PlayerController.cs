using Unity.Netcode;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : CharController
{
    protected int damageToEnemy;
    protected float attackCooldown;

    private float horizontalInput; // entrada horizontal (A/D o flechas)
    private float verticalInput;   // entrada vertical (W/S o flechas)

    private PlayerControls controls;

    public bool IsAttacking { get; private set; } = false;
    public int DamageToEnemy => damageToEnemy;
    public NetworkVariable<Vector2> Position = new NetworkVariable<Vector2>();
    public NetworkVariable<float> moveSpeedSync = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    // Índice del personaje elegido, sincronizado a todos pero solo lo escribe el dueño
    private readonly NetworkVariable<int> characterIndexSync = new NetworkVariable<int>(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private NetworkVariable<int> initialHealth_online = new NetworkVariable<int>(99, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> health_online = new NetworkVariable<int>(99, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Diamonds = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Keys = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public string EntityId => GetComponent<UniqueEntity>()?.EntityId ?? "UNKNOWN";

    // Configura los controles de entrada y deja el sprite oculto hasta que se sincronice el personaje
    protected override void Awake()
    {
        base.Awake();
        controls = new PlayerControls();

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Nos suscribimos a los cambios de las variables sincronizadas
        characterIndexSync.OnValueChanged += OnCharacterIndexChanged;
        health_online.OnValueChanged += OnHealthChanged;
        Diamonds.OnValueChanged += OnDiamondsChanged;
        Keys.OnValueChanged += OnKeysChanged;

        if (IsOwner)
        {
            // El dueño se registra como jugador local y avisa al GameManager qué personaje eligió
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
            // Los demás clientes aplican el aspecto visual ya sincronizado
            if (characterIndexSync.Value != 0)
            {
                ActualizarAspectoVisual(characterIndexSync.Value);
            }
        }

        string escenaActual = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

        if (escenaActual == SceneNames.PlaygroundLevel)
        {
            if (IsOwner)
            {
                // Avisamos al HUD los valores iniciales de vida, llaves y diamantes
                GameEvents.HealthChanged(health_online.Value);
                GameEvents.KeysChanged(Keys.Value);
                GameEvents.DiamondsChanged(Diamonds.Value);
            }

            IsAttacking = false;
        }
    }

    private void OnCharacterIndexChanged(int previousValue, int newValue)
    {
        ActualizarAspectoVisual(newValue);
    }

    // Aplica las estadísticas y el aspecto visual correspondientes al índice de personaje
    private void ActualizarAspectoVisual(int index)
    {
        if (GameManager.Instance == null) return;

        PlayerStats statsAsignadas = GameManager.Instance.GetCharacterStatsByIndex(index);

        if (statsAsignadas == null) return;

        stats = statsAsignadas;

        if (IsOwner)
        {
            // Aplicamos las stats mecánicas al CharController base
            damageToEnemy = statsAsignadas.attackDamage;
            attackCooldown = statsAsignadas.attackCooldown;
            initialHealth = statsAsignadas.maxHealth;
        }

        if (!IsSpawned) health_online = initialHealth_online; // solo inicializa vida si recién nace

        if (IsServer)
        {
            health_online.Value = initialHealth;
            health = health_online.Value; // sincroniza la variable local del CharController base
        }

        // Cambiamos el animator según el personaje
        if (statsAsignadas.animatorController != null && animator != null)
        {
            animator.runtimeAnimatorController = statsAsignadas.animatorController;
        }

        // Por si Awake dejó el sprite apagado, lo volvemos a encender
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;
    }

    public void CambiarPersonajeDesdeCliente(int nuevoIdx)
    {
        if (IsOwner)
        {
            characterIndexSync.Value = nuevoIdx;
            ActualizarAspectoVisual(nuevoIdx);
        }
    }

    private void OnHealthChanged(int previousValue, int newValue)
    {
        health = newValue;

        if (IsOwner)
        {
            GameEvents.HealthChanged(newValue);
        }
    }

    private void OnKeysChanged(int previo, int nuevo)
    {
        if (IsOwner)
        {
            GameEvents.KeysChanged(nuevo);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        characterIndexSync.OnValueChanged -= OnCharacterIndexChanged;
        health_online.OnValueChanged -= OnHealthChanged;
        Diamonds.OnValueChanged -= OnDiamondsChanged;
        Keys.OnValueChanged -= OnKeysChanged;
    }

    // Arranca desactivado hasta que el flujo de spawn lo active
    protected override void Start()
    {
        base.Start();
        gameObject.SetActive(false);
    }

    // Cada frame: comrpueba muerte y actualiza animación/orientación según el movimiento
    protected override void Update()
    {
        if (!IsOwner || !IsSpawned) return; // si no soy el dueño, no controlo este jugador

        checkDeath();

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
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();
    }

    // El movimiento real solo se ejecuta si soy el dueño, estoy spawneado y en la escena de juego
    protected override void Move()
    {
        if (!IsOwner || !IsSpawned) return;

        string escena = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (escena != SceneNames.PlaygroundLevel) return;

        base.Move();
    }

    // Activa al personaje en la posición indicada y avisa al server para que lo propague a todos
    [Rpc(SendTo.Everyone)]
    public void ActivarPersonajeRpc(Vector3 pos)
    {
        transform.position = pos;
        gameObject.SetActive(true);
        NotificarActivacionRpc();
    }

    [Rpc(SendTo.Server)]
    public void NotificarActivacionRpc()
    {
        // El server reenvía la activación a todos los clientes
        ActivarPersonajesRpc();
    }

    [Rpc(SendTo.Everyone)]
    public void ActivarPersonajesRpc()
    {
        gameObject.SetActive(true);
    }

    // Los clientes que no son dueños, sincronizan su animación de movimiento con la del dueño
    void LateUpdate()
    {
        if (!IsOwner)
        {
            animator.SetFloat("speed", moveSpeedSync.Value);
        }
    }

    // Activa el mapa de controles y se suscribe a movimiento y ataque
    private void OnEnable()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = true;

        if (!IsOwner) return;

        if (controls == null) controls = new PlayerControls();

        controls.Enable();

        controls.Player.Move.performed += OnMovePerformed;
        controls.Player.Move.canceled += OnMoveCanceled;

        controls.Player.Attack.performed += onAttack;
    }

    // Se desuscribe de los controles y los desactiva
    private void OnDisable()
    {
        if (controls != null)
        {
            controls.Player.Move.performed -= OnMovePerformed;
            controls.Player.Move.canceled -= OnMoveCanceled;

            controls.Player.Attack.performed -= onAttack;

            controls.Disable();
        }
    }

    // Gestiona la muerte del jugador local y avisa al GameManager
    public override void Die()
    {
        base.Die();

        if (IsOwner)
        {
            GameManager.LocalPlayerHasDied = true;
            GameEvents.LocalPlayerDied(); // muestra ya la pantalla de "has muerto"
        }

        if (GameManager.Instance != null)
            GameManager.Instance.NotificarMuerteServerRpc();
    }

    // Aplica daño descontando vida en el servidor y delega el resto en la clase base
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
    }

    // Reemplaza las stats actuales por unas nuevas y recarga todos los valores
    public void ApplyCharacterStats(PlayerStats newStats)
    {
        if (newStats == null)
        {
            Debug.LogWarning("[PlayerController] ApplyCharacterStats llamado con null");
            return;
        }

        stats = newStats;
        LoadStats();

        Debug.Log($"[PlayerController] Stats aplicadas: {newStats.characterName}");
    }

    // Carga las estadísticas del personaje seleccionado (o valores por defecto si no hay ninguno)
    protected override void LoadStats()
    {
        if (IsOwner && GameManager.Instance != null && GameManager.Instance.SelectedCharacterStats != null)
        {
            stats = GameManager.Instance.SelectedCharacterStats;
        }

        base.LoadStats();

        // Casteamos el campo heredado a PlayerStats para acceder a sus campos propios
        PlayerStats playerStats = stats as PlayerStats;

        if (playerStats != null)
        {
            moveSpeed *= playerStats.speedBonus;

            damageToEnemy = playerStats.attackDamage;
            attackCooldown = playerStats.attackCooldown;
        }
        else
        {
            // Sin PlayerStats asignado, usamos valores por defecto
            Debug.LogWarning($"[{gameObject.name}] No tiene PlayerStats asignado. Usando valores por defecto.");
            damageToEnemy = 50;
            attackCooldown = 0.5f;
            moveSpeed *= 1.25f;
        }
    }

    // Comprueba si la vida llegó a cero y, si es así, dispara la muerte una sola vez
    private void checkDeath()
    {
        if (health_online.Value <= 0 && !isDead)
        {
            Die();
        }
    }

    private void OnDiamondsChanged(int previo, int nuevo)
    {
        if (IsOwner)
        {
            GameEvents.DiamondsChanged(nuevo);
        }
    }

    public void AddDiamondServer()
    {
        if (!IsServer) return;
        Diamonds.Value++;
    }

    public void AddKeyServer()
    {
        if (!IsServer) return;
        Keys.Value++;
    }

    public bool UseKeyServer()
    {
        if (!IsServer) return false;
        if (Keys.Value <= 0) return false;
        Keys.Value--;
        return true;
    }

    // Dispara la animación de ataque y la corta automáticamente tras el cooldown
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

    private void endAttack()
    {
        IsAttacking = false;
    }
}