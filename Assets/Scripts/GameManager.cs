using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SceneNames
{
    public const string MainMenu = "MainMenu";
    public const string CharSelection = "CharSelectionScene";
    public const string PlaygroundLevel = "PlaygroundLevel";
    public const string DeadScene = "DeadScene";
    public const string VictoryScene = "VictoryScene";
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public NetworkManager _networkManager;

    [SerializeField] private GameObject _playerBall;

    public PlayerController LocalPlayerController { get; private set; }
    public Transform LocalPlayerTransform => LocalPlayerController != null ? LocalPlayerController.transform : null;
    public UniqueEntity LocalPlayerEntity { get; private set; }

    public int EnemiesKilled { get; private set; }
    public PlayerStats SelectedCharacterStats { get; set; }
    public MapConfig SelectedMapConfig { get; set; }
    public string RoomCode { get; set; }

    public NetworkVariable<int> seed = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public NetworkVariable<int> mapConfigNetwork = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server); // 0 -> valor por defecto // .everyone -> lo pueden leer todos // .server -> solo lo puede modificar el server
    public int SelectedMapIdx { get; set; } = 0;

    [SerializeField] public MapConfig[] availableMaps;

    public NetworkVariable<Unity.Collections.FixedString64Bytes> Code = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    

    [SerializeField] private float delayBeforeScene = 0.5f;

    private PlayerGameState playerState;

    public NetworkVariable<int> clientes = new NetworkVariable<int>();

    /// <summary>
    /// Inicializa el singleton del juego y sus datos persistentes.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        playerState = new PlayerGameState("PLAYER_1");
        SceneManager.sceneUnloaded += onSceneUnloaded;
    }

    public void Start()
    {
        _networkManager = NetworkManager.Singleton;
    
            if (_networkManager == null)
            {
                UnityEngine.Debug.LogError("[GameManager] Start: _networkManager es NULL. Nada funcionará.");
                return;
            }

            if (_networkManager.NetworkConfig.Prefabs.Prefabs.Count > 0)
                _playerBall = _networkManager.NetworkConfig.Prefabs.Prefabs[0].Prefab;

            _networkManager.OnServerStarted += onServerStarted;
            _networkManager.OnClientConnectedCallback += onClientConnected;
            _networkManager.OnClientDisconnectCallback += onClientDisconnect;
    
            UnityEngine.Debug.Log("[GameManager] Start: Callbacks suscritos correctamente.");

    }

    private void onSceneLoadCompleted(string sceneName, LoadSceneMode loadSceneMode,
    List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        UnityEngine.Debug.Log($"[GameManager] onSceneLoadCompleted: escena={sceneName}, clientes={clientsCompleted.Count}");

        if (sceneName != SceneNames.CharSelection) return;
        if (!_networkManager.IsServer) return;

        foreach (ulong clientId in clientsCompleted)
        {
            var existing = _networkManager.ConnectedClients[clientId].PlayerObject;

            // Only skip if it already has PlayerState (i.e. it's our prefab)
            if (existing != null && existing.GetComponent<PlayerState>() != null)
            {
                UnityEngine.Debug.Log($"[GameManager] Cliente {clientId} ya tiene PlayerState, no se vuelve a spawnear.");
                continue;
            }

            // Despawn the wrong prefab if present
            if (existing != null)
            {
                UnityEngine.Debug.LogWarning($"[GameManager] Cliente {clientId} tiene PlayerObject sin PlayerState, despawneando.");
                existing.Despawn();
            }

            var playerObject = Instantiate(_playerBall);
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                UnityEngine.Debug.LogError($"[GameManager] El prefab _playerBall NO tiene componente NetworkObject!");
                continue;
            }

            networkObject.SpawnAsPlayerObject(clientId);
            UnityEngine.Debug.Log($"[GameManager] Spawneado PlayerObject para cliente {clientId}.");
        }
    }


    /// <summary>
    /// Libera suscripciones globales al destruir el gestor.
    /// </summary>
    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= onSceneUnloaded;
    }

    /// <summary>
    /// Suscribe callbacks de eventos persistentes del juego.
    /// </summary>
    private void OnEnable()
    {
        GameEvents.OnPlayerDied += onPlayerDeath;
    }

    /// <summary>
    /// Desuscribe callbacks de eventos persistentes del juego.
    /// </summary>
    private void OnDisable()
    {
        GameEvents.OnPlayerDied -= onPlayerDeath;
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ClientAndHostRpc(int value, ulong sourceNetworkObjectId)
    {
        UnityEngine.Debug.Log($"Client Received the RPC #{value} on NetworkObject #{sourceNetworkObjectId}");
        if (IsOwner) //Only send an RPC to the owner of the NetworkObject
        {
            ServerOnlyRpc(value + 1, sourceNetworkObjectId);
        }
    }

    [Rpc(SendTo.Server)]
    private void ServerOnlyRpc(int value, ulong sourceNetworkObjectId)
    {
        UnityEngine.Debug.Log($"Server received RPC #{value} on NetworkObject #{sourceNetworkObjectId}" );
        ClientAndHostRpc(value, sourceNetworkObjectId);
    }

    private void onServerStarted()
    {
        print("El servidor está listo");
        clientes.Value = 0;
        _networkManager.SceneManager.OnLoadEventCompleted += onSceneLoadCompleted;

    }

    // Evento cuando un cliente se ha conectado
    private void onClientConnected(ulong clientId)
    {
        // Solo si eres el servidor decides instanciar a los clientes
        if (!_networkManager.IsServer) return;

        clientes.Value += 1;
        UnityEngine.Debug.Log("Clientes conectados: " + clientes.Value);

        // Solo spawnear si ya estamos en CharSelection
        // Si no, onSceneLoadCompleted lo hará al cargar la escena
        if (SceneManager.GetActiveScene().name == SceneNames.CharSelection)
        {
            var playerObject = Instantiate(_playerBall);
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
            networkObject.SpawnAsPlayerObject(clientId);
        }
    }

    private void onClientDisconnect(ulong clientId)
    {
        StartCoroutine(HandleDisconnect());
    }

    private IEnumerator HandleDisconnect()
    {
        // Espera un frame (puedes aumentar a 0.1f si sigue fallando)
        yield return null;

        var allPlayers = GameObject.FindGameObjectsWithTag("Player");

        /*int humanosVivos = 0;
        int zombiesVivos = 0;

        foreach (var player in allPlayers)
        {
            if (player.name.Contains("character-human"))
            {
                humanosVivos++;
            }
            else if (player.name.Contains("character-orc"))
            {
                zombiesVivos++;
            }
        }

        //GameManager.Instance.ZombiesVivos.Value = zombiesVivos;
        //GameManager.Instance.HumanosVivos.Value = humanosVivos;
        Debug.Log($"Humanos vivos: {humanosVivos}, Orcos vivos: {zombiesVivos}");

        if (zombiesVivos == 0)
        {
            Debug.Log("No quedan orcos. Los humanos ganan.");
            endHumanWin.Value = true;
        }
        else if (humanosVivos == 0)
        {
            Debug.Log("No quedan humanos. Los orcos ganan.");
            endZombieWin.Value = true;
        }*/

        clientes.Value = Mathf.Max(0, clientes.Value - 1);
        UnityEngine.Debug.Log("Clientes conectados: " + clientes.Value);
    }



    public void CheckAllReady()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject?.GetComponent<PlayerState>();
            if (player == null || !player.isReady.Value)
                return; // Al menos uno no está listo
        }

        // Todos están listos, cambiamos de escena

        StartCoroutine(DespawnAndLoadScene());
    }

    private IEnumerator DespawnAndLoadScene()
    {
        var allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (var player in allPlayers)
        {
            if (player.TryGetComponent<NetworkObject>(out var netObj))
            {
                netObj.Despawn();
            }
        }

        // Esperar 1 frame (mínimo)
        yield return null;

        NetworkManager.Singleton.SceneManager.LoadScene("PlaygroundLevel", LoadSceneMode.Single);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkObject.DontDestroyWithOwner = true;
        }

        if (!IsServer && IsOwner)
        {
            ServerOnlyRpc(0, NetworkObjectId);
        }
    }

    /// <summary>
    /// Registra el jugador local activo y publica su evento de registro.
    /// </summary>
    public void RegisterLocalPlayer(PlayerController player, UniqueEntity entity)
    {
        LocalPlayerController = player;
        LocalPlayerEntity = entity;
        SetPlayerData(entity);
        GameEvents.LocalPlayerRegistered(player);
    }

    /// <summary>
    /// Inicializa el estado del jugador con el identificador de su entidad.
    /// </summary>
    public void SetPlayerData(UniqueEntity playerEntity)
    {
        if (playerEntity == null || string.IsNullOrEmpty(playerEntity.EntityId)) return;
        playerState = new PlayerGameState(playerEntity.EntityId);
    }

    /// <summary>
    /// Reinicia los datos de partida del jugador y estadísticas globales.
    /// </summary>
    public void ResetGameData()
    {
        playerState?.ResetState();
        EnemiesKilled = 0;
    }

    /// <summary>
    /// Incrementa el contador global de enemigos eliminados.
    /// </summary>
    public void AddEnemyKill()
    {
        EnemiesKilled++;
        GameEvents.EnemyKilled(EnemiesKilled);
    }

    /// <summary>
    /// Devuelve la cantidad actual de llaves del jugador local.
    /// </summary>
    public int GetKeys()
    {
        return playerState?.Keys ?? 0;
    }

    /// <summary>
    /// Devuelve la cantidad actual de diamantes del jugador local.
    /// </summary>
    public int GetDiamonds()
    {
        return playerState?.Diamonds ?? 0;
    }

    /// <summary>
    /// Intenta añadir una llave al inventario del jugador actual.
    /// </summary>
    public bool TryAddKey(string playerEntityId, string keyEntityId)
    {
        if (playerState == null) return false;
        playerState.AddKey();
        return true;
    }

    /// <summary>
    /// Intenta añadir un diamante al inventario del jugador actual.
    /// </summary>
    public bool TryAddDiamond(string playerEntityId, string diamondEntityId)
    {
        if (playerState == null) return false;
        playerState.AddDiamond();
        return true;
    }

    /// <summary>
    /// Intenta abrir una puerta consumiendo una llave del jugador actual.
    /// </summary>
    public bool TryOpenDoor(string playerEntityId, string doorEntityId)
    {
        if (playerState == null) return false;
        return playerState.UseKey();
    }

    /// <summary>
    /// Intenta activar la condición de victoria para el jugador actual.
    /// </summary>
    public bool TryTriggerVictory(string playerEntityId, string chestEntityId)
    {
        if (playerState == null) return false;
        victoryAchieved();
        return true;
    }

    /// <summary>
    /// Guarda el personaje seleccionado, reinicia datos y carga el nivel de juego.
    /// </summary>
    public void StartGame_(PlayerStats selectedCharacter)
    {

        if (selectedCharacter == null)
        {
            UnityEngine.Debug.LogError("[GameManager] StartGame llamado sin personaje seleccionado.");
            return;
        }

        UnityEngine.Debug.Log($"selected character is {selectedCharacter.characterName}");
        SelectedCharacterStats = selectedCharacter;
        ResetGameData();

        NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.PlaygroundLevel, LoadSceneMode.Single);
    }

    /// <summary>
    /// Guarda mapa y personaje seleccionados e inicia la partida.
    /// </summary>
    public void StartGame(PlayerStats selectedCharacter)
    {
        if (IsServer)
        {
            seed.Value = Random.Range(1, int.MaxValue); //0 -> no asignado
        }
        StartGame_(selectedCharacter);
    }

    /// <summary>
    /// Inicia el flujo de fin de partida por muerte del jugador.
    /// </summary>
    public void TriggerGameOver()
    {
        UnityEngine.Debug.Log($"[GameManager] Game Over. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
        Invoke(nameof(loadDeadScene), delayBeforeScene);
    }

    /// <summary>
    /// Limpia los eventos de escena cuando se descarga el nivel jugable.
    /// </summary>
    private void onSceneUnloaded(Scene scene)
    {
        if (scene.name == SceneNames.PlaygroundLevel)
        {
            GameEvents.ClearSceneEvents();
        }

        
    }

    /// <summary>
    /// Carga la escena de derrota del jugador.
    /// </summary>
    private void loadDeadScene()
    {
        SceneManager.LoadScene(SceneNames.DeadScene);
    }

    /// <summary>
    /// Registra logs de victoria y programa la carga de la escena final.
    /// </summary>
    private void victoryAchieved()
    {
        UnityEngine.Debug.Log($"[GameManager] Victoria. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
        Invoke(nameof(loadVictoryScene), delayBeforeScene);
    }

    /// <summary>
    /// Carga la escena de victoria del juego.
    /// </summary>
    private void loadVictoryScene()
    {
        SceneManager.LoadScene(SceneNames.VictoryScene);
    }

    /// <summary>
    /// Registra en consola el estado del juego cuando el jugador muere.
    /// </summary>
    private void onPlayerDeath()
    {
        UnityEngine.Debug.Log($"[GameManager] Jugador muerto. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
    }

}



