using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System;
using System.Collections;

public class GameOverCanvasHandler : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI jewelsValueText;
    [SerializeField] private TextMeshProUGUI keysValueText;
    [SerializeField] private TextMeshProUGUI enemiesKilledText;

    /// <summary>
    /// Inicializa la pantalla mostrando las estadísticas finales de la partida.
    /// </summary>
    private void Start()
    {
        displayGameStats();
    }

    private void OnEnable()
    {
        GameManager.OnStatsSynced += displayGameStats;
    }

    private void OnDisable()
    {
        GameManager.OnStatsSynced -= displayGameStats;
    }


    /// <summary>
    /// Carga el menú principal al pulsar el botón de volver.
    /// </summary>
    public void OnBackButtonClicked()
    {
        StartCoroutine(ShutdownAndReturnToMenu());
    }

    private IEnumerator ShutdownAndReturnToMenu()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();

            // Esperamos a que el shutdown termine de verdad antes de cambiar de escena
            yield return new WaitUntil(() => NetworkManager.Singleton == null || !NetworkManager.Singleton.ShutdownInProgress);
            yield return null; // un frame extra por si acaso
        }

        SceneManager.LoadScene(SceneNames.MainMenu);

    }

    /// <summary>
    /// Actualiza los textos del panel con diamantes, llaves y enemigos eliminados.
    /// </summary>
    private void displayGameStats()
    {
        if (jewelsValueText != null)
            jewelsValueText.text = GameManager.DiamantesEncontrados.ToString();

        if (keysValueText != null)
            keysValueText.text = GameManager.LlavesSinUsar.ToString();

        if (enemiesKilledText != null)
            enemiesKilledText.text = GameManager.EnemigosEliminados.ToString();
    }
}
