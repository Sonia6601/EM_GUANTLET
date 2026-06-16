using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using Unity.Netcode;
using UnityEngine.UI;
using NUnit.Framework;
using System.Collections.Generic;

public class CharSelectionMenuButtonsHandler : NetworkBehaviour
{
    [Header("Character Stats Assets")]
    [SerializeField] private PlayerStats greenCharacterStats;
    [SerializeField] private PlayerStats purpleCharacterStats;
    [SerializeField] private PlayerStats redCharacterStats;
    [SerializeField] private PlayerStats yellowCharacterStats;
    [SerializeField] private TextMeshProUGUI code;
    private List<int> characterPool = new List<int> { 0, 1, 2, 3 };
    private Button startButton;
    public Canvas canva;
    private int jugadoresListos = 0;

    [Header("UI")]
    [SerializeField] private TMP_Text connectedPlayersText;
    [SerializeField] private TMP_Text playerListText;

    public void Start()
    {
        if(GameManager.Instance != null)
        {
            code.text = GameManager.Instance.RoomCode;
        }

        UpdateConnectedClientsUI();

        // Suscribirse al NetworkVariable 'clientes' para recibir actualizaciones desde el servidor
        if (GameManager.Instance != null && GameManager.Instance.clientes != null)
        {
            GameManager.Instance.clientes.OnValueChanged += OnClientesChanged;
        }

        if (IsServer)
        {
            CreateButton();
        }

        // Debug: comprobar referencias
        if (connectedPlayersText == null)
        {
            Debug.LogWarning("[CharSelection] connectedPlayersText no asignado en el Inspector. Intentando localizar automáticamente...");
            TryAutoFindConnectedPlayersText();
        }
        else
        {
            Debug.Log($"[CharSelection] connectedPlayersText asignado: {connectedPlayersText.name}");
        }
    }

    private void Update()
    {
        UpdateConnectedClientsUI();
    }

    /// <summary>
    /// Actualiza la información de clientes conectados en el UI
    /// </summary>
    private void UpdateConnectedClientsUI()
    {
        if (NetworkManager.Singleton == null) return;

        // Obtener el número total de clientes conectados
        int totalClientes = NetworkManager.Singleton.ConnectedClients.Count;

        if (connectedPlayersText != null)
        {
            // Mostrar el número usando el NetworkManager como fallback
            connectedPlayersText.text = $"Clientes: {totalClientes}";
        }
        else
        {
            // Intentar encontrar automáticamente si no hay referencia
            TryAutoFindConnectedPlayersText();
        }

        // Obtener la lista de IDs de clientes conectados
        var connectedClientIds = NetworkManager.Singleton.ConnectedClientsIds;
        string clientListText = "Clientes conectados:\n";
        
        int clientNumber = 1;
        foreach (var clientId in connectedClientIds)
        {
            clientListText += $"Cliente {clientNumber}: {clientId}\n";
            clientNumber++;
        }

        if (playerListText != null)
        {
            playerListText.text = clientListText;
        }
    }

    private void OnClientesChanged(int previousValue, int newValue)
    {
        if (connectedPlayersText != null)
        {
            connectedPlayersText.text = $"Clientes: {newValue}";
        }
        else
        {
            Debug.Log($"[CharSelection] OnClientesChanged pero connectedPlayersText es NULL. nuevo valor={newValue}");
        }
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null && GameManager.Instance.clientes != null)
        {
            GameManager.Instance.clientes.OnValueChanged -= OnClientesChanged;
        }
    }

    private void TryAutoFindConnectedPlayersText()
    {
        // Buscar componentes TMP en la escena y elegir el que contenga la palabra "Cliente" o "Clientes" en su texto o nombre
        var all = FindObjectsOfType<TMPro.TMP_Text>(true);
        foreach (var t in all)
        {
            if (t == null) continue;
            string lowerName = t.name.ToLower();
            string lowerText = (t.text ?? string.Empty).ToLower();
            if (lowerName.Contains("cliente") || lowerText.Contains("cliente") || lowerText.Contains("clientes"))
            {
                connectedPlayersText = t;
                Debug.Log($"[CharSelection] connectedPlayersText localizado automáticamente: {t.name}");
                return;
            }
        }

        Debug.LogWarning("[CharSelection] No se pudo localizar automáticamente connectedPlayersText. Asigna el campo en el Inspector.");
    }


    public void AsignarPersonaje()
    {
        Barajar(characterPool);

        var clientes = NetworkManager.Singleton.ConnectedClientsIds;

        int i = 0;
        foreach (var client in clientes)
        {
            int personajeAsignado = characterPool[i];
            RecibirPersonajeClientRpc(personajeAsignado,
                new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new[] { client }
                    }
                }
            );
            i++;
        }
    }

    [ClientRpc]
    private void RecibirPersonajeClientRpc(int personajeId, ClientRpcParams rpcParams = default)
    {
        

        switch(personajeId){
            case 0:
                OnYellowButtonClicked();
                Debug.Log($"Me asignaron el personaje: {personajeId} -> color AMARILLO");
                break;
            case 1:
                OnGreenButtonClicked();
                Debug.Log($"Me asignaron el personaje: {personajeId} -> color VERDE");
                break;
            case 2:
                OnRedButtonClicked();
                Debug.Log($"Me asignaron el personaje: {personajeId} -> color ROJO");
                break;
            case 3:
                OnPurpleButtonClicked();
                Debug.Log($"Me asignaron el personaje: {personajeId} -> color MORADO");
                break;
        }

        ListosServerRpc();
        
    }

    [ServerRpc(RequireOwnership = false)]
    private void ListosServerRpc()
    {
        jugadoresListos++;
        int totalJugadores = NetworkManager.Singleton.ConnectedClients.Count;

        Debug.Log($"[SERVER] Jugadores Listos: {jugadoresListos}/{totalJugadores}");

        if (jugadoresListos >= totalJugadores)
        {
            jugadoresListos = 0;
            GameManager.Instance.StartGame(GameManager.Instance.SelectedCharacterStats);
        }
        }

    private void Barajar(List<int> list)
    {
        for (int i = list.Count-1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public void CreateButton()
    {
        GameObject buttonGO = new GameObject("StartButton", typeof(RectTransform));
        buttonGO.transform.SetParent(canva.transform, false);

        startButton = buttonGO.AddComponent<Button>();

        //Añadir componente RectTranform para que cree el rectangulo
        RectTransform rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(220f, 70f);

        rect.anchoredPosition = new Vector2(-20f, 20f);

        //Añadir componente Image para que tenga color de fondo
        Image img = buttonGO.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 0.2f, 1f);

        //Crear gameObject para el texto
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = rect.sizeDelta;
        textRect.anchoredPosition = Vector2.zero;

        //Añador componente Text para ponerle texto
        TextMeshProUGUI texto = textGO.AddComponent<TextMeshProUGUI>();
        texto.text = "START";
        texto.fontSize = 24;
        texto.color = Color.white;
        texto.alignment = TextAlignmentOptions.Center;

        startButton.onClick.AddListener(OnRandomButtonClicked);
    }

    
    /// <summary>
    /// Vuelve al menú principal desde la pantalla de selección de personaje.
    /// </summary>
    public void OnBackButtonClicked()
    {
        
        SceneManager.LoadScene(SceneNames.MainMenu);
    }

    /// <summary>
    /// Selecciona el personaje verde e inicia la partida.
    /// </summary>
    /// 
    public void OnRandomButtonClicked()
    {

        AsignarPersonaje();

        // Ahora usamos la instancia local directa, sin pasar por NetworkManager
        //var localPlayer = PlayerState.LocalInstance;

        //if (localPlayer == null)
        //{
        //    Debug.LogError("[CharSelection] PlayerState.LocalInstance es null. " +
        //                   "El prefab del jugador no tiene PlayerState, o aún no se spawneó.");
        //    return;
        //}

        //Debug.Log($"[CharSelection] Marcando ready. Estado actual: {localPlayer.isReady.Value}");
        //localPlayer.SetReadyServerRpc(!localPlayer.isReady.Value);



    }


    public void OnGreenButtonClicked()
    {
        //selectCharacterAndStartGame(greenCharacterStats);
        GameManager.Instance.SelectedCharacterStats = greenCharacterStats;
        greenCharacterStats.select = true;

    }

    /// <summary>
    /// Selecciona el personaje morado e inicia la partida.
    /// </summary>
    public void OnPurpleButtonClicked()
    {
        GameManager.Instance.SelectedCharacterStats = purpleCharacterStats;
        purpleCharacterStats.select = true;

    }

    /// <summary>
    /// Selecciona el personaje rojo e inicia la partida.
    /// </summary>
    public void OnRedButtonClicked()
    {
        GameManager.Instance.SelectedCharacterStats = redCharacterStats;
        redCharacterStats.select = true;

    }

    /// <summary>
    /// Selecciona el personaje amarillo e inicia la partida.
    /// </summary>
    public void OnYellowButtonClicked()
    {
        GameManager.Instance.SelectedCharacterStats = yellowCharacterStats;
        yellowCharacterStats.select = true;

    }

    /// <summary>
    /// Valida la selección del personaje y delega el inicio de partida en GameManager.
    /// 
    /// 
    /// Para futuras iteraciones, en esta función se puede hacer que se compruebe si todos los persoanjes tienen uno asignado, de mometno no nos hace falta, ya que la asignación de persoanjes se va a hacer de forma directa
    /// </summary>
    //private void selectCharacterAndStartGame(PlayerStats characterStats)
    //{
    //    //if (characterStats == null)
    //    //{
    //    //    Debug.LogError("[CharSelection] No se ha asignado PlayerStats para este personaje");
    //    //    return;
    //    //}

    //    GameManager.Instance?.StartGame(characterStats);
    //}

}