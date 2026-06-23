using UnityEngine;

public class LocalDeathScreen : MonoBehaviour
{
    [SerializeField] private GameObject canvasLocalDeath;  // CanvasLocalDeath
    [SerializeField] private GameObject panelStats;        // Panel dentro de Canvas

    private void OnEnable()
    {
        GameEvents.OnLocalPlayerDied += MostrarPantallaIndividual;
        GameEvents.OnPlayerDied += MostrarPantallaStats;
    }

    private void OnDisable()
    {
        GameEvents.OnLocalPlayerDied -= MostrarPantallaIndividual;
        GameEvents.OnPlayerDied -= MostrarPantallaStats;
    }

    private void MostrarPantallaIndividual()
    {
        if (canvasLocalDeath != null)
            canvasLocalDeath.SetActive(true);
    }

    private void MostrarPantallaStats()
    {
        if (canvasLocalDeath != null)
            canvasLocalDeath.SetActive(false);   // oculta la individual

        if (panelStats != null)
            panelStats.SetActive(true);          // muestra la global
    }
}