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

        if (IsServer)
        {
            CreateButton();
        }
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
        GameObject buttonGO = new GameObject("StartButton");
        buttonGO.transform.SetParent(canva.transform, false);


        

        startButton = buttonGO.AddComponent<Button>();

        //Añadir componente RectTranform para que cree el rectangulo
        RectTransform rect = buttonGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(200, 60);
        rect.anchoredPosition = new Vector2(230, -150); 

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
        Text texto = textGO.AddComponent<Text>();
        texto.text = "START";
        texto.fontSize = 24;
        texto.color = Color.white;
        texto.alignment = TextAnchor.MiddleCenter;


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
    private void selectCharacterAndStartGame(PlayerStats characterStats)
    {
        //if (characterStats == null)
        //{
        //    Debug.LogError("[CharSelection] No se ha asignado PlayerStats para este personaje");
        //    return;
        //}

        GameManager.Instance?.StartGame(characterStats);
    }

}