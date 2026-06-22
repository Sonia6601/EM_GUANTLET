using TMPro;
using UnityEngine;
using Unity.Netcode;
using System;

public class VictoryScreenDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text enemigos;
    [SerializeField] private TMP_Text llaves;
    [SerializeField] private TMP_Text diamantes;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UpdateDisplay();
        GameManager.OnStatsSynced += UpdateDisplay;

    }

    private void UpdateDisplay()
    {
        if (enemigos != null)
        {
            enemigos.text = " " + GameManager.EnemigosEliminados;
        }

        if (diamantes != null)
        {
            diamantes.text = " " + GameManager.DiamantesEncontrados;
        }

        if (llaves != null)
        {
            llaves.text = " " + GameManager.LlavesSinUsar;
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
