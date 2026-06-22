using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

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
    //[SerializeField] private GameObject _playerPrefab;

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

    private readonly HashSet<ulong> disconnectedClientsHandled = new HashSet<ulong>();
    public int SelectedCharacterIndex { get; set; } = 0;
    public static int DiamantesEncontrados {  get; set; }
    public static int LlavesSinUsar { get; set; }
    public static int EnemigosEliminados { get; set; }

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
        {
            _playerBall = _networkManager.NetworkConfig.Prefabs.Prefabs[0].Prefab;
        }

        _networkManager.OnServerStarted -= onServerStarted;
        _networkManager.OnServerStarted += onServerStarted;

        _networkManager.OnClientConnectedCallback -= onClientConnected;
        _networkManager.OnClientConnectedCallback += onClientConnected;

        _networkManager.OnClientDisconnectCallback -= onClientDisconnect;
        _networkManager.OnClientDisconnectCallback += onClientDisconnect;

        UnityEngine.Debug.Log("[GameManager] Start: Callbacks suscritos correctamente.");

    }

    private void onSceneLoadCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        List<ulong> clientsCompleted,
        List<ulong> clientsTimedOut)
    {
        if (_networkManager == null) return;
        if (!_networkManager.IsServer) return;
        UnityEngine.Debug.Log("[GAME MANAGER] Escena cargada: " + sceneName);

        if (sceneName == SceneNames.CharSelection)
        {

            foreach (ulong clientId in clientsCompleted)
            {
                if (!_networkManager.ConnectedClients.ContainsKey(clientId))
                {
                    continue;
                }

                var existing = _networkManager.ConnectedClients[clientId].PlayerObject;

                if (existing == null)
                {
                    var playerObject = Instantiate(_playerBall);
                    NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();

                    if (networkObject == null)
                    {
                        UnityEngine.Debug.LogError("[GameManager] El prefab _playerBall no tiene NetworkObject.");
                        Destroy(playerObject);
                        continue;
                    }

                    networkObject.SpawnAsPlayerObject(clientId);
                }

                //// Only skip if it already has PlayerState (i.e. it's our prefab)
                //if (existing != null && existing.GetComponent<PlayerState>() != null)
                //{
                //    UnityEngine.Debug.Log($"[GameManager] Cliente {clientId} ya tiene PlayerState, no se vuelve a spawnear.");
                //    continue;
                //}

                //// Despawn the wrong prefab if present
                //if (existing != null)
                //{
                //    UnityEngine.Debug.LogWarning($"[GameManager] Cliente {clientId} tiene PlayerObject sin PlayerState, despawneando.");
                //    existing.Despawn();
                //}

                //var playerObject = Instantiate(_playerBall);
                //NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
                //networkObject.SpawnAsPlayerObject(clientId);
                
                

            }
        }


        //else if (sceneName == SceneNames.PlaygroundLevel)
        //{

        //    foreach (ulong clientId in clientsCompleted)
        //    {
        //        var existing = _networkManager.ConnectedClients[clientId].PlayerObject;

        //        if (existing == null)
        //        {
        //            var playerObject = Instantiate(_playerBall);
        //            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
        //            networkObject.SpawnAsPlayerObject(clientId);
        //        }
        //    }
        //}
    }


    /// <summary>
    /// Libera suscripciones globales al destruir el gestor.
    /// </summary>
    public override void OnDestroy()
    {
        SceneManager.sceneUnloaded -= onSceneUnloaded;

        if (_networkManager != null)
        {
            _networkManager.OnServerStarted -= onServerStarted;
            _networkManager.OnClientConnectedCallback -= onClientConnected;
            _networkManager.OnClientDisconnectCallback -= onClientDisconnect;

            if (_networkManager.SceneManager != null)
            {
                _networkManager.SceneManager.OnLoadEventCompleted -= onSceneLoadCompleted;
            }
        }

        base.OnDestroy();
    }

    /// <summary>
    /// Si un cliente cierra la aplicación, intenta avisar al servidor antes de desconectarse.
    /// </summary>
    private void OnApplicationQuit()
    {
        if (NetworkManager.Singleton == null) return;

        if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer)
        {
            UnityEngine.Debug.Log("[GameManager] Cliente cerrando aplicación. Avisando al servidor...");

            if (IsSpawned)
            {
                NotifyClientLeavingServerRpc(NetworkManager.Singleton.LocalClientId);
            }
        }
    }

    /// <summary>
    /// Suscribe callbacks de eventos persistentes del juego.
    /// </summary>
   /* private void OnEnable()
    {
        GameEvents.OnPlayerDied += onPlayerDeath;
    }*/

    /// <summary>
    /// Desuscribe callbacks de eventos persistentes del juego.
    /// </summary>
    //private void OnDisable()
    //{
    //    GameEvents.OnPlayerDied -= onPlayerDeath;
    //}

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
        disconnectedClientsHandled.Clear();

        if (_networkManager != null && _networkManager.SceneManager != null)
        {
            _networkManager.SceneManager.OnLoadEventCompleted -= onSceneLoadCompleted;
            _networkManager.SceneManager.OnLoadEventCompleted += onSceneLoadCompleted;
        }
    }

    /// <summary>
    /// Evento cuando un cliente se ha conectado.
    /// </summary>
    private void onClientConnected(ulong clientId)
    {
        if (_networkManager == null) return;
        if (!_networkManager.IsServer) return;

        disconnectedClientsHandled.Remove(clientId);

        clientes.Value += 1;
        UnityEngine.Debug.Log("Clientes conectados: " + clientes.Value);

        // Solo spawnear si ya estamos en CharSelection
        // Si no, onSceneLoadCompleted lo hará al cargar la escena
        if (SceneManager.GetActiveScene().name == SceneNames.CharSelection)
        {
            var playerObject = Instantiate(_playerBall);
            NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();

            if (networkObject == null)
            {
                UnityEngine.Debug.LogError("[GameManager] El prefab _playerBall no tiene NetworkObject.");
                Destroy(playerObject);
                return;
            }

            networkObject.SpawnAsPlayerObject(clientId);
        }
    }

    /// <summary>
    /// Evento cuando un cliente o el host se desconecta.
    /// CLIENTE se desconecta: el servidor despawnea sus objetos y la partida sigue.
    /// HOST se desconecta: los clientes vuelven al menú principal.
    /// </summary>
    private void onClientDisconnect(ulong clientId)
    {
        UnityEngine.Debug.Log($"[DISCONNECT TEST] onClientDisconnect llamado. clientId={clientId}");

        if (_networkManager == null)
        {
            UnityEngine.Debug.LogWarning("[DISCONNECT TEST] _networkManager es null.");
            return;
        }

        UnityEngine.Debug.Log(
            $"[DISCONNECT TEST] IsServer={_networkManager.IsServer}, " +
            $"IsClient={_networkManager.IsClient}, " +
            $"LocalClientId={_networkManager.LocalClientId}"
        );

        if (!_networkManager.IsServer)
        {
            if (clientId == NetworkManager.ServerClientId)
            {
                HandleHostDisconnected();
            }

            return;
        }

        if (clientId != NetworkManager.ServerClientId)
        {
            HandleClientDisconnected(clientId);
        }
    }

    /// <summary>
    /// El cliente llama a este RPC antes de cerrarse para que el servidor gestione su salida.
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void NotifyClientLeavingServerRpc(ulong leavingClientId)
    {
        UnityEngine.Debug.Log($"[GameManager] NotifyClientLeavingServerRpc recibido. Cliente que se va: {leavingClientId}");

        HandleClientDisconnected(leavingClientId);
    }

    /// <summary>
    /// Gestiona la desconexión de un cliente normal.
    /// </summary>
    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (disconnectedClientsHandled.Contains(clientId))
        {
            UnityEngine.Debug.Log($"[GameManager] La desconexión del cliente {clientId} ya fue gestionada.");
            return;
        }

        disconnectedClientsHandled.Add(clientId);

        UnityEngine.Debug.Log($"[GameManager] Cliente desconectado: {clientId}");
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

        DespawnObjectsOwnedByClient(clientId);

        GameEvents.NetworkStatusMessage("Un jugador abandonó");
        ShowNetworkMessageClientRpc("Un jugador abandonó");
    }

    /// <summary>
    /// Gestiona la desconexión del host desde el punto de vista de los clientes.
    /// </summary>
    private void HandleHostDisconnected()
    {
        UnityEngine.Debug.Log("[GameManager] El host abandonó la partida.");

        GameEvents.NetworkStatusMessage("El host abandonó");

        StartCoroutine(ReturnToMainMenuAfterHostDisconnect());
    }

    /// <summary>
    /// Espera para que se vea el mensaje, cierra la conexión y vuelve al menú.
    /// </summary>
    private IEnumerator ReturnToMainMenuAfterHostDisconnect()
    {
        yield return new WaitForSeconds(2f);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(SceneNames.MainMenu);
    }

    /// <summary>
    /// Despawnea todos los objetos de red que pertenecían al cliente desconectado.
    /// </summary>
    private void DespawnObjectsOwnedByClient(ulong clientId)
    {
        if (_networkManager == null || _networkManager.SpawnManager == null) return;
        if (!_networkManager.IsServer) return;

        UnityEngine.Debug.Log($"[GameManager] Buscando objetos del cliente desconectado {clientId}");

        List<NetworkObject> objectsToDespawn = new List<NetworkObject>();

        foreach (NetworkObject networkObject in _networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (networkObject == null) continue;

            UnityEngine.Debug.Log(
                $"[GameManager] Revisando objeto: {networkObject.name} | " +
                $"OwnerClientId={networkObject.OwnerClientId} | " +
                $"IsPlayerObject={networkObject.IsPlayerObject}"
            );

            if (networkObject.OwnerClientId == clientId)
            {
                objectsToDespawn.Add(networkObject);
            }
        }

        foreach (NetworkObject networkObject in objectsToDespawn)
        {
            if (networkObject != null && networkObject.IsSpawned)
            {
                UnityEngine.Debug.Log($"[GameManager] Despawneando objeto del cliente {clientId}: {networkObject.name}");
                networkObject.Despawn(true);
            }
        }

        UnityEngine.Debug.Log($"[GameManager] Total objetos despawneados del cliente {clientId}: {objectsToDespawn.Count}");
    }

    /// <summary>
    /// Muestra un mensaje de red en todos los clientes.
    /// </summary>
    [ClientRpc]
    private void ShowNetworkMessageClientRpc(string message)
    {
        GameEvents.NetworkStatusMessage(message);
    }

    public void CheckAllReady()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject?.GetComponent<PlayerState>();
            if (player == null || !player.isReady.Value)
            {
                return; // Al menos uno no está listo
            }
        }

        // Todos están listos, cambiamos de escena

        StartCoroutine(DespawnAndLoadScene());
    }

    private IEnumerator DespawnAndLoadScene()
    {

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
        AddEnemyKillServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    public void AddEnemyKillServerRpc()
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
        if(LocalPlayerController != null)
        {
            return LocalPlayerController.Diamonds.Value;
        }
        return 0;
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
        if (!IsServer || playerState == null) return false; //El server solo puede cambiar de escena

        CalcularEstadisticasFinales(); //Se calculan las estadisticas globales

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.VictoryScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
            return true;
        } else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.VictoryScene);
        }

            victoryAchieved();
        return true;
    }

    private void CalcularEstadisticasFinales()
    {
        EnemigosEliminados = EnemiesKilled;

        int totalDiamantes = 0;
        int totalLlaves = 0;

        if(NetworkManager.Singleton != null)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClients)
            {
                if(client.Value.PlayerObject != null)
                {
                    PlayerController player = client.Value.PlayerObject.GetComponent<PlayerController>();
                    if (player != null)
                    {
                        totalDiamantes += player.Diamonds.Value;
                        totalLlaves += player.Keys.Value;
                    }
                }
            }
        } else if (LocalPlayerController != null)
        {
            totalDiamantes = LocalPlayerController.Diamonds.Value;
            totalLlaves = LocalPlayerController.Keys.Value;
        }

        DiamantesEncontrados = totalDiamantes;
        LlavesSinUsar = totalLlaves;

        UnityEngine.Debug.LogFormat("[STATS GLOBALES] Enemigos totales: {0} | Diamantes totales: {1} | Llaves sin usar: {2}", EnemigosEliminados, DiamantesEncontrados, LlavesSinUsar);
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
    //public void TriggerGameOver()
    //{
    //    UnityEngine.Debug.Log($"[TriggerGameOver] Llamado desde:\n{System.Environment.StackTrace}");
    //    UnityEngine.Debug.Log($"[GameManager] Game Over. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
    //    Invoke(nameof(ShowDeadUI), delayBeforeScene);
    //}

    private void ShowDeadUI()
    {

        // Opción rápida: Si tienes una pantalla de muerte en tu Canvas actual, actívala.
        // Ej: MenuMuerteUI.SetActive(true);

        // Disparamos un evento global para que el Canvas de tu escena PlaygroundLevel 
        // sepa que tiene que oscurecer la pantalla y mostrar los botones de "Salir".
        GameEvents.PlayerDied();
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
    //private void loadDeadScene()
    //{
    //    SceneManager.LoadScene(SceneNames.DeadScene);
    //}

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
    /*private void onPlayerDeath()
    {
        UnityEngine.Debug.Log($"[GameManager] Jugador muerto. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
    }*/

    [Header("Personajes Disponibles")]
    [SerializeField] private PlayerStats[] availableCharacters; // <-- Añade esto

    // Una función rápida para obtener el índice del personaje seleccionado
    public int GetSelectedCharacterIndex()
    {
        //if (SelectedCharacterStats == null) return 0;
        //for (int i = 0; i < availableCharacters.Length; i++)
        //{
        //    if (availableCharacters[i] == SelectedCharacterStats)
        //    {
        //        return i;
        //    }
        //}

        //return 0;
        return SelectedCharacterIndex;
    }

    // Una función para obtener las estadísticas usando el índice que viaja por la red
    public PlayerStats GetCharacterStatsByIndex(int index)
    {
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            UnityEngine.Debug.LogWarning("[GameManager] No hay personajes configurados en availableCharacters.");
            return SelectedCharacterStats;
        }

        if (index < 0 || index >= availableCharacters.Length)
        {
            return availableCharacters[0]; // Fallback al primero
        }

        return availableCharacters[index];
    }

}



