using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

// Nombres de las escenas del juego, para no escribirlos a mano y evitar errores de tipeo
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
    // Singleton: solo puede haber un GameManager en toda la partida
    public static GameManager Instance { get; private set; }

    public NetworkManager _networkManager;

    [SerializeField] private GameObject _playerBall; // prefab del jugador que se spawnea en red

    public PlayerController LocalPlayerController { get; private set; }
    public Transform LocalPlayerTransform => LocalPlayerController != null ? LocalPlayerController.transform : null;
    public UniqueEntity LocalPlayerEntity { get; private set; }

    public int EnemiesKilled { get; private set; }
    public PlayerStats SelectedCharacterStats { get; set; }
    public MapConfig SelectedMapConfig { get; set; }
    public string RoomCode { get; set; }

    // Semilla de generación de mapa, la fija el server y todos la pueden leer
    public NetworkVariable<int> seed = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Índice del mapa seleccionado, sincronizado por red (-1 = sin asignar)
    public NetworkVariable<int> mapConfigNetwork = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int SelectedMapIdx { get; set; } = 0;

    [SerializeField] public MapConfig[] availableMaps;

    // Código de sala, solo lo puede escribir el server
    public NetworkVariable<Unity.Collections.FixedString64Bytes> Code = new NetworkVariable<Unity.Collections.FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    [SerializeField] private float delayBeforeScene = 0.5f; // tiempo de espera antes de cambiar de escena
    public static event System.Action OnStatsSynced;

    private PlayerGameState playerState;
    private int jugadoresVivos = 0;

    public NetworkVariable<int> clientes = new NetworkVariable<int>(); // contador de clientes conectados

    private readonly HashSet<ulong> disconnectedClientsHandled = new HashSet<ulong>(); // evita procesar dos veces la misma desconexión
    public int SelectedCharacterIndex { get; set; } = 0;
    public static int DiamantesEncontrados { get; set; }
    public static int LlavesSinUsar { get; set; }
    public static int EnemigosEliminados { get; set; }
    public static bool LocalPlayerHasDied { get; set; } = false;

    // Awake se ejecuta antes que Start, aquí montamos el singleton
    private void Awake()
    {
        // Si ya existe otra instancia, esta se destruye (no puede haber dos GameManagers)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // que sobreviva al cambiar de escena

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

        // Tomamos el primer prefab registrado como el prefab del jugador
        if (_networkManager.NetworkConfig.Prefabs.Prefabs.Count > 0)
        {
            _playerBall = _networkManager.NetworkConfig.Prefabs.Prefabs[0].Prefab;
        }

        // Nos suscribimos a los eventos de red (quitando antes por si ya estaba suscrito)
        _networkManager.OnServerStarted -= onServerStarted;
        _networkManager.OnServerStarted += onServerStarted;

        _networkManager.OnClientConnectedCallback -= onClientConnected;
        _networkManager.OnClientConnectedCallback += onClientConnected;

        _networkManager.OnClientDisconnectCallback -= onClientDisconnect;
        _networkManager.OnClientDisconnectCallback += onClientDisconnect;

        UnityEngine.Debug.Log("[GameManager] Start: Callbacks suscritos correctamente.");
    }

    // Cuenta cuántos jugadores hay vivos al empezar la partida
    public void InicializarJugadoresVivos()
    {
        if (!IsServer) return;
        jugadoresVivos = NetworkManager.Singleton.ConnectedClientsList.Count;
        UnityEngine.Debug.Log($"[GameManager] Jugadores vivos al inicio: {jugadoresVivos}");
    }

    // Cada jugador avisa al morir (cuando no quede ninguno termina la partida)
    [ServerRpc(RequireOwnership = false)]
    public void NotificarMuerteServerRpc()
    {
        jugadoresVivos--;
        UnityEngine.Debug.Log($"[GameManager] Un jugador murió. Quedan vivos: {jugadoresVivos}");

        if (jugadoresVivos <= 0)
        {
            SincronizarEstadisticasFinDePartida();
        }
    }

    // Se llama cuando todos los clientes terminaron de cargar una escena
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
            // Por cada cliente que terminó de cargar, le creamos su jugador si todavía no lo tiene
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
            }
        }
        else if (sceneName == SceneNames.PlaygroundLevel)
        {
            InicializarJugadoresVivos();
        }
    }

    // Al destruir el GameManager hay que desuscribirse de todos los eventos
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

    // Si el cliente cierra la app, intenta avisar al server antes de irse
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

    // Par de RPCs de prueba para comprobar la comunicación cliente servidor
    [Rpc(SendTo.ClientsAndHost)]
    private void ClientAndHostRpc(int value, ulong sourceNetworkObjectId)
    {
        UnityEngine.Debug.Log($"Client Received the RPC #{value} on NetworkObject #{sourceNetworkObjectId}");
        if (IsOwner)
        {
            ServerOnlyRpc(value + 1, sourceNetworkObjectId);
        }
    }

    [Rpc(SendTo.Server)]
    private void ServerOnlyRpc(int value, ulong sourceNetworkObjectId)
    {
        UnityEngine.Debug.Log($"Server received RPC #{value} on NetworkObject #{sourceNetworkObjectId}");
        ClientAndHostRpc(value, sourceNetworkObjectId);
    }

    // Se dispara cuando el servidor arranca, reseteamos contadores
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

    // Un cliente nuevo se conectó
    private void onClientConnected(ulong clientId)
    {
        if (_networkManager == null) return;
        if (!_networkManager.IsServer) return;

        disconnectedClientsHandled.Remove(clientId);

        clientes.Value += 1;
        UnityEngine.Debug.Log("Clientes conectados: " + clientes.Value);

        // Si ya estamos en la escena de selección de personaje, lo spawneamos ya mismo
        // (si no, se hará después en onSceneLoadCompleted)
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

    // Un cliente (o el host) se desconectó
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

        // Si no soy el server, solo me interesa saber si se fue el host
        if (!_networkManager.IsServer)
        {
            if (clientId == NetworkManager.ServerClientId)
            {
                HandleHostDisconnected();
            }

            return;
        }

        // Si soy el server, gestiono la salida de un cliente normal
        if (clientId != NetworkManager.ServerClientId)
        {
            HandleClientDisconnected(clientId);
        }
    }

    // El cliente llama esto antes de cerrarse para que el server lo gestione bien
    [ServerRpc(RequireOwnership = false)]
    private void NotifyClientLeavingServerRpc(ulong leavingClientId)
    {
        UnityEngine.Debug.Log($"[GameManager] NotifyClientLeavingServerRpc recibido. Cliente que se va: {leavingClientId}");
        HandleClientDisconnected(leavingClientId);
    }

    // Limpia todo lo relacionado a un cliente que se desconectó
    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        // Evitamos procesar la misma desconexión dos veces
        if (disconnectedClientsHandled.Contains(clientId))
        {
            UnityEngine.Debug.Log($"[GameManager] La desconexión del cliente {clientId} ya fue gestionada.");
            return;
        }

        disconnectedClientsHandled.Add(clientId);

        UnityEngine.Debug.Log($"[GameManager] Cliente desconectado: {clientId}");

        clientes.Value = Mathf.Max(0, clientes.Value - 1);
        UnityEngine.Debug.Log("Clientes conectados: " + clientes.Value);

        DespawnObjectsOwnedByClient(clientId);

        GameEvents.NetworkStatusMessage("Un jugador abandonó");
        ShowNetworkMessageClientRpc("Un jugador abandonó");
    }

    // Si el host se va, los clientes vuelven al menú principal
    private void HandleHostDisconnected()
    {
        UnityEngine.Debug.Log("[GameManager] El host abandonó la partida.");
        GameEvents.NetworkStatusMessage("El host abandonó");
        StartCoroutine(ReturnToMainMenuAfterHostDisconnect());
    }

    // Espera un poco para que se vea el mensaje, cierra la red y vuelve al menú
    private IEnumerator ReturnToMainMenuAfterHostDisconnect()
    {
        yield return new WaitForSeconds(2f);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(SceneNames.MainMenu);
    }

    // Busca y elimina (despawnea) los objetos de red que pertenecían a un cliente desconectado
    private void DespawnObjectsOwnedByClient(ulong clientId)
    {
        if (_networkManager == null || _networkManager.SpawnManager == null) return;
        if (!_networkManager.IsServer) return;

        UnityEngine.Debug.Log($"[GameManager] Buscando objetos del cliente desconectado {clientId}");

        List<NetworkObject> objectsToDespawn = new List<NetworkObject>();

        foreach (NetworkObject networkObject in _networkManager.SpawnManager.SpawnedObjectsList)
        {
            if (networkObject == null) continue;

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

    // Muestra un mensaje de estado de red en todos los clientes
    [ClientRpc]
    private void ShowNetworkMessageClientRpc(string message)
    {
        GameEvents.NetworkStatusMessage(message);
    }

    // Comprueba si todos los jugadores conectados están listos para empezar
    public void CheckAllReady()
    {
        if (!IsServer) return;

        if (NetworkManager.Singleton.ConnectedClientsList.Count < 2) return;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var player = client.PlayerObject?.GetComponent<PlayerState>();
            if (player == null || !player.isReady.Value)
            {
                return; // al menos uno no está listo, no seguimos
            }
        }

        // Todos listos, cambiamos de escena
        StartCoroutine(DespawnAndLoadScene());
    }

    private IEnumerator DespawnAndLoadScene()
    {
        yield return null; // esperamos un frame antes de cargar

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

    // Guarda referencia al jugador local y avisa al resto del sistema que ya está registrado
    public void RegisterLocalPlayer(PlayerController player, UniqueEntity entity)
    {
        LocalPlayerController = player;
        LocalPlayerEntity = entity;
        SetPlayerData(entity);
        GameEvents.LocalPlayerRegistered(player);
    }

    // Crea el estado de partida del jugador usando su id de entidad
    public void SetPlayerData(UniqueEntity playerEntity)
    {
        if (playerEntity == null || string.IsNullOrEmpty(playerEntity.EntityId)) return;
        playerState = new PlayerGameState(playerEntity.EntityId);
    }

    // Reinicia los datos al empezar una partida nueva
    public void ResetGameData()
    {
        playerState?.ResetState();
        EnemiesKilled = 0;
    }

    // El jugador mata un enemigo, lo notificamos al server
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

    public int GetKeys()
    {
        if (LocalPlayerController != null)
        {
            return LocalPlayerController.Keys.Value;
        }
        return 0;
    }

    public int GetDiamonds()
    {
        if (LocalPlayerController != null)
        {
            return LocalPlayerController.Diamonds.Value;
        }
        return 0;
    }

    // Intenta entregarle una llave al jugador indicado
    public bool TryAddKey(string playerEntityId, string keyEntityId)
    {
        if (!IsServer) return false;

        var player = FindPlayerByEntityId(playerEntityId);
        if (player == null) return false;

        player.AddKeyServer();
        return true;
    }

    // Intenta entregarle un diamante al jugador indicado
    public bool TryAddDiamond(string playerEntityId, string diamondEntityId)
    {
        if (!IsServer) return false;

        var player = FindPlayerByEntityId(playerEntityId);
        if (player == null) return false;

        player.AddDiamondServer();
        return true;
    }

    // Intenta abrir una puerta gastando una llave del jugador
    public bool TryOpenDoor(string playerEntityId, string doorEntityId)
    {
        if (!IsServer) return false;

        var player = FindPlayerByEntityId(playerEntityId);
        if (player == null) return false;

        return player.UseKeyServer();
    }

    // Intenta disparar la condición de victoria (al abrir el cofre final)
    public bool TryTriggerVictory(string playerEntityId, string chestEntityId)
    {
        if (!IsServer || playerState == null) return false; // solo el server cambia de escena

        CalcularEstadisticasFinales();

        // Sincronizamos las estadísticas con todos los clientes antes de cambiar de escena
        SincronizarEstadisticasClientRpc(DiamantesEncontrados, LlavesSinUsar, EnemigosEliminados);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.SceneManager != null)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.VictoryScene, UnityEngine.SceneManagement.LoadSceneMode.Single);
            return true;
        }
        else
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.VictoryScene);
        }

        victoryAchieved();
        return true;
    }

    // Los clientes reciben las estadísticas finales antes de cambiar de escena
    [ClientRpc]
    private void SincronizarEstadisticasClientRpc(int diamantes, int llaves, int enemigos)
    {
        DiamantesEncontrados = diamantes;
        LlavesSinUsar = llaves;
        EnemigosEliminados = enemigos;

        OnStatsSynced?.Invoke();

        // Los jugadores que ya murieron muestran su panel ahora, con los datos finales
        if (GameManager.LocalPlayerHasDied)
        {
            GameEvents.PlayerDied();
        }
    }

    public void SincronizarEstadisticasFinDePartida()
    {
        if (!IsServer) return;
        CalcularEstadisticasFinales();
        SincronizarEstadisticasClientRpc(DiamantesEncontrados, LlavesSinUsar, EnemigosEliminados);
    }

    [ServerRpc(RequireOwnership = false)]
    public void SolicitarSincronizacionFinDePartidaServerRpc()
    {
        SincronizarEstadisticasFinDePartida();
    }

    // Recorre a todos los jugadores conectados y suma sus diamantes y llaves
    private void CalcularEstadisticasFinales()
    {
        EnemigosEliminados = EnemiesKilled;

        int totalDiamantes = 0;
        int totalLlaves = 0;

        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            var playerObj = client.PlayerObject;
            if (playerObj == null) continue;

            var pc = playerObj.GetComponent<PlayerController>();
            if (pc == null) continue;

            totalDiamantes += pc.Diamonds.Value;
            totalLlaves += pc.Keys.Value;
        }

        DiamantesEncontrados = totalDiamantes;
        LlavesSinUsar = totalLlaves;

        UnityEngine.Debug.LogFormat("[STATS GLOBALES] Enemigos: {0} | Diamantes: {1} | Llaves: {2}", EnemigosEliminados, DiamantesEncontrados, LlavesSinUsar);
    }

    // Guarda el personaje elegido, resetea datos y carga el nivel jugable
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

    // Genera la semilla del mapa (solo el server) y arranca la partida
    public void StartGame(PlayerStats selectedCharacter)
    {
        if (IsServer)
        {
            seed.Value = Random.Range(1, int.MaxValue); // 0 significa sin asignar
        }
        StartGame_(selectedCharacter);
    }

    private void ShowDeadUI()
    {
        // Avisamos a la escena que tiene que oscurecer la pantalla y mostrar los botones de salir
        GameEvents.PlayerDied();
    }

    // Al descargar el nivel jugable, limpiamos los eventos asociados a esa escena
    private void onSceneUnloaded(Scene scene)
    {
        if (scene.name == SceneNames.PlaygroundLevel)
        {
            GameEvents.ClearSceneEvents();
        }
    }

    // Loggea la victoria y, tras un pequeño delay, carga la escena de victoria
    private void victoryAchieved()
    {
        UnityEngine.Debug.Log($"[GameManager] Victoria. Keys: {GetKeys()}, Diamonds: {GetDiamonds()}, Enemies: {EnemiesKilled}");
        Invoke(nameof(loadVictoryScene), delayBeforeScene);
    }

    private void loadVictoryScene()
    {
        SceneManager.LoadScene(SceneNames.VictoryScene);
    }

    // Busca el PlayerController correspondiente a un EntityId dado
    private PlayerController FindPlayerByEntityId(string playerEntityId)
    {
        if (NetworkManager.Singleton == null) return null;

        foreach (var client in NetworkManager.Singleton.ConnectedClients)
        {
            var playerObj = client.Value.PlayerObject;
            if (playerObj == null) continue;

            var entity = playerObj.GetComponent<UniqueEntity>();
            if (entity != null && entity.EntityId == playerEntityId)
            {
                return playerObj.GetComponent<PlayerController>();
            }
        }
        return null;
    }

    [Header("Personajes Disponibles")]
    [SerializeField] private PlayerStats[] availableCharacters;

    public int GetSelectedCharacterIndex()
    {
        return SelectedCharacterIndex;
    }

    // Devuelve las estadísticas del personaje según su índice (viaja por la red)
    public PlayerStats GetCharacterStatsByIndex(int index)
    {
        if (availableCharacters == null || availableCharacters.Length == 0)
        {
            UnityEngine.Debug.LogWarning("[GameManager] No hay personajes configurados en availableCharacters.");
            return SelectedCharacterStats;
        }

        if (index < 0 || index >= availableCharacters.Length)
        {
            return availableCharacters[0]; // si el índice no es válido, usamos el primero
        }

        return availableCharacters[index];
    }
}