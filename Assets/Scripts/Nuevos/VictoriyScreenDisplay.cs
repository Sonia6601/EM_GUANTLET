using TMPro;
using UnityEngine;
using Unity.Netcode;

public class VictoriyScreenDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text enemigos;
    [SerializeField] private TMP_Text llaves;
    [SerializeField] private TMP_Text diamantes;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (enemigos != null)
        {
            enemigos.text = "Enemigos eliminados: " + GameManager.EnemigosEliminados;
        }

        if (llaves != null)
        {
            diamantes.text = "Joyas encontradas: " + GameManager.DiamantesEncontrados;
        }

        if (llaves != null)
        {
            llaves.text = "Llaves sin usar: " + GameManager.LlavesSinUsar;
        }
    }

    public void VolverMenu()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        UnityEngine.SceneManagement.SceneManager.LoadScene(SceneNames.MainMenu);
    }
}
