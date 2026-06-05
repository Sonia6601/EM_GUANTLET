using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;
using System;
using System.Linq;


#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuButtonsHandler : NetworkBehaviour
{
    [Header("Map Configs disponibles")]
    [SerializeField] private MapConfig[] availableMaps;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown mapsDropdown;

    public TMP_InputField inputCode;

    /// <summary>
    /// Inicializa el dropdown de mapas al cargar el menú principal.
    /// </summary>
    private void Start()
    {
        initializeMapDropdown();
    }

    /// <summary>
    /// Libera la suscripción del dropdown al destruir el objeto.
    /// </summary>
    private void OnDestroy()
    {
        if (mapsDropdown != null)
            mapsDropdown.onValueChanged.RemoveListener(onMapDropdownChanged);
    }

    /// <summary>
    /// Navega a la escena de selección de personaje si hay mapa seleccionado.
    /// </summary>
    public void OnButtonPlayClicked()
    {
        if (GameManager.Instance?.SelectedMapConfig == null)
        {
            Debug.LogWarning("[MainMenu] No hay mapa seleccionado.");
            return;
        }

        //Aquí es donde al darle a jugar se pasa a la escena de seleccionar jugadores
        //Entonces, el primero que le de, poner que sea el host, es decir, Que el primero acceda al networkManager y active la opción de create Host
        //Y el resto serán clientes, acceda al networkManager y active el crear cliente

        SceneManager.LoadScene(SceneNames.CharSelection);
    }

    public void StartHost()
    {
        if (GameManager.Instance?.SelectedMapConfig == null)
        {
            Debug.LogWarning("[MainMenu] No hay mapa seleccionado.");
            return;
        }
        string localIP = GetLocalIPv4(); //Se coge la IPv4 del host para que los clientes se puedan conectar al host
        string codeRoom = GeneracionCodigoSala(); //Se genera el código de la sala
        Debug.Log("[HOST]: Sala creada con codigo: " + codeRoom);
        GameManager.Instance.RoomCode = codeRoom;

        // 1. Obtenemos el componente 'UnityTransport' del NetworkManager.
        // El 'UnityTransport' es el motor/protocolo de bajo nivel que usa Unity Netcode para enviar y recibir datos en red local o a través de internet (gestiona los sockets UDP)
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // 2. Le indicamos a UnityTransport en qué Dirección IP y Puerto (7777 es el estándar) debe "abrir sus puertas".
        // Como Host, esto significa: "Escucha a cualquier jugador que intente conectarse a mi IP local en el puerto 7777".
        transport.SetConnectionData(localIP, 7777);

        NetworkManager.Singleton.StartHost();
        GameManager.Instance.Code.Value = codeRoom;

        NetworkManager.Singleton.SceneManager.LoadScene(SceneNames.CharSelection, LoadSceneMode.Single);
    }

    private string GetLocalIPv4()
    {
        try
        {
            // 1. Dns.GetHostName(): Obtiene el nombre del ordenador local (ej: "PC-Juan").
            // 2. Dns.GetHostEntry(...): Busca en la red ese nombre y devuelve todas las interfaces de red de este PC.
            // 3. AddressList.First(...): Filtra la lista para quedarse con la primera dirección que sea de tipo 'InterNetwork' (es decir, IPv4). Filtrar es necesario porque también puede devolver direcciones IPv6 o de red virtual.
            // 4. ToString(): Convierte la IP encontrada a texto (ej: "192.168.1.33").
            var ip = Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(f => f.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);

            if (ip == null)
            {
                Debug.Log("[MAINMENU] no se encontró ninguna IP IPv4. Usando localHost");
                return "127.0.0.1";
            }

            return ip.ToString();
        }
        catch (Exception e)
        {
            Debug.LogError($"[MAINMENU] Error obteniendo IP: {e.Message}");
            return "127.0.0.1";
        }

    }


    // Método que ejecuta el jugador que intenta unirse (Cliente) a una sala ya creada.
    public void StartClient()
    {
        string codigoIntroducido = inputCode?.text.Trim().ToUpper(); //pone con un buen formato el codigo (por si hay espacio o se pone en minuscula)

        if (string.IsNullOrEmpty(codigoIntroducido))
        {
            Debug.Log("Introduce un código.");
            return;
        }

        // Guardamos el código introducido para enviarlo al host tras conectar
        GameManager.Instance.RoomCode = codigoIntroducido;

        // Recuperamos el componente de transporte para decirle al cliente a dónde debe conectarse.
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        // OJO: Al usar 'GetLocalIPv4()' aquí, el cliente está calculando y usando su *propia* IP.
        // Esto es útil SÓLO si probáis el juego abriendo dos ventanas en el mismo ordenador. 
        // Si vais a jugar desde ordenadores distintos, no funcionará así, habría que usar la lógica de IP a Código.
        var localIP = GetLocalIPv4();

        transport.SetConnectionData(localIP, 7777); //la ip está configurada para que se pruebe desde el mismo ordenador solo

        // Inicia los sistemas internos, conecta por UDP al servidor y sincroniza la partida actual.
        NetworkManager.Singleton.StartClient();
    }

    // [Rpc(SendTo.Server)] indica que este método es una llamada de red de Cliente a Servidor.
    // Aunque un Cliente llame a esta función en su propio código, la función se envía por red
    // y se EJECUTA ÚNICAMENTE EN EL ORDENADOR DEL HOST (Servidor).
    // Es el sustituto moderno del antiguo atributo [ServerRpc].
    [Rpc(SendTo.Server)]
    public void ValidarCodigoServerRpc(string codigoCliente, ulong clientId)
    {
        if (codigoCliente != GameManager.Instance.RoomCode)
        {
            Debug.Log($"Cliente {clientId} tiene código incorrecto");
        }
        else
        {
            Debug.Log($"Cliente {clientId} validado");
        }
    }

    private bool GUIcodigo()
    {
        //Comprobaciones varias para saber por qué no funcionaba        if (inputCode == null)
        {
            Debug.LogError("inputCode es NULL Asígnalo en el Inspector");
            return false;
        }


        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager.Instance es NULL Falta el GameManager en la escena");
            return false;
        
        }

        string codigoIntroducido = inputCode.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(codigoIntroducido))
        {
            Debug.Log("El campo de código está vacío.");
            return false;
        }

        string codigoSala = GameManager.Instance.RoomCode.ToString();

        if (codigoIntroducido == codigoSala)
        {
            Debug.Log("Código correcto.");
            return true;
        }
        else
        {
            Debug.Log($"Código incorrecto. Introducido: {codigoIntroducido} | Esperado: {codigoSala}");
            return false;
        }
    }
    /// <summary>
    /// Registra la acción del botón de opciones del menú principal.
    /// </summary>
    public void OnOptionsButtonClicked()
    {
        Debug.Log("Options button pressed");
    }

    /// <summary>
    /// Cierra la aplicación o detiene la ejecución en el editor.
    /// </summary>
    public void OnExitButtonClicked()
    {
        Debug.Log("Exit button pressed");
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    /// <summary>
    /// Configura las opciones del dropdown y establece el mapa inicial seleccionado.
    /// </summary>
    private void initializeMapDropdown()
    {
        if (mapsDropdown == null || availableMaps == null || availableMaps.Length == 0)
        {
            Debug.LogWarning("[MainMenu] Dropdown de mapas no configurado.");
            return;
        }

        mapsDropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (MapConfig map in availableMaps)
        {
            options.Add(new TMP_Dropdown.OptionData(map != null ? map.mapName : "Sin nombre"));
        }

        mapsDropdown.AddOptions(options);
        mapsDropdown.value = 0;
        mapsDropdown.RefreshShownValue();
        mapsDropdown.onValueChanged.AddListener(onMapDropdownChanged);
        applySelectedMap(0);
    }

    /// <summary>
    /// Aplica el mapa seleccionado cuando cambia el valor del dropdown.
    /// </summary>
    private void onMapDropdownChanged(int index)
    {
        applySelectedMap(index);
    }

    /// <summary>
    /// Guarda en GameManager el mapa correspondiente al índice indicado.
    /// </summary>
    private void applySelectedMap(int index)
    {
        if (availableMaps == null || index < 0 || index >= availableMaps.Length) return;
        if (GameManager.Instance == null) return;

        GameManager.Instance.SelectedMapConfig = availableMaps[index];
        Debug.Log($"[MainMenu] Mapa seleccionado: {availableMaps[index].mapName}");

        if (NetworkManager.Singleton.IsServer)
        {
            GameManager.Instance.MapConfigIdx.Value = index;
        }
    }

    private string GeneracionCodigoSala()
    {
        //Aquí se generará el código de la sala, el host se lo pasará a sus amiguitos para poder jugar juntos
        //El código debe aparecer en el canvas de la escena de selección de jugadores
        string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = ""; //no pongais un espacio que si no se inicializa con uno

        for (int i = 0; i < 6; i++)
        {
            int index = UnityEngine.Random.Range(0, characters.Length);
            code += characters[index];

        }

        return code;
    }
}