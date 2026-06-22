using UnityEngine;
using TMPro; // Asegúrate de incluir TMPro para los textos

public class DeathUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject deathUIPanel;

    [Header("Stats References")]
    [SerializeField] private TMP_Text enemigos;
    [SerializeField] private TMP_Text llaves;
    [SerializeField] private TMP_Text diamantes;

    private void OnEnable()
    {
        // Nos suscribimos al evento de muerte y a la sincronización de red
        GameEvents.OnPlayerDied += MostrarPantallaMuerte;
        GameManager.OnStatsSynced += UpdateStatsDisplay;
    }

    private void OnDisable()
    {
        // Nos desuscribimos para evitar errores de memoria
        GameEvents.OnPlayerDied -= MostrarPantallaMuerte;
        GameManager.OnStatsSynced -= UpdateStatsDisplay;
    }

    private void MostrarPantallaMuerte()
    {
        if (deathUIPanel != null)
        {
            deathUIPanel.SetActive(true);
            // ✅ No hace falta llamar a UpdateStatsDisplay aquí,
            // porque el evento llega ya con los datos listos.
            UpdateStatsDisplay();
        }
    }


    private void UpdateStatsDisplay()
    {
        if (deathUIPanel != null && deathUIPanel.activeSelf)
        {
            if (enemigos != null)
                enemigos.text = " " + GameManager.EnemigosEliminados;

            if (diamantes != null)
                diamantes.text = " " + GameManager.DiamantesEncontrados;   // ← estática, NO LocalPlayerController

            if (llaves != null)
                llaves.text = " " + GameManager.LlavesSinUsar;              // ← estática, NO LocalPlayerController
        }
    }
}