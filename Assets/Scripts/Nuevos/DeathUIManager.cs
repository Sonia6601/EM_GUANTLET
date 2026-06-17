using UnityEngine;

public class DeathUIManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject deathUIPanel; 

    private void OnEnable()
    {
        // Nos suscribimos al evento de muerte
        GameEvents.OnPlayerDied += MostrarPantallaMuerte;
    }

    private void OnDisable()
    {
        // Nos desuscribimos para evitar errores al cambiar de escena
        GameEvents.OnPlayerDied -= MostrarPantallaMuerte;
    }

    private void MostrarPantallaMuerte()
    {
        if (deathUIPanel != null)
        {
            deathUIPanel.SetActive(true);
            Debug.Log("[UI] Pantalla de muerte activada para el jugador local.");
        }
    }

}
