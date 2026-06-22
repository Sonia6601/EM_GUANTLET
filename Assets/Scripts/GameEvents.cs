using System;

public static class GameEvents
{
    public static event Action<int> OnHealthChanged;
    public static event Action<int> OnKeysChanged;
    public static event Action<int> OnDiamondsChanged;
    public static event Action<int> OnEnemyKilled;
    public static event Action<PlayerController> OnLocalPlayerRegistered;
    public static event Action OnPlayerDied;
    public static event Action OnVictory;
    public static event Action OnLocalPlayerDied;
    public static event Action<string> OnNetworkStatusMessage;

    /// <summary>
    /// Notifica un cambio en la salud del jugador.
    /// </summary>
    public static void HealthChanged(int newHealth)
    {
        OnHealthChanged?.Invoke(newHealth);
    }

    /// <summary>
    /// Notifica un cambio en el n�mero de llaves del jugador.
    /// </summary>
    public static void KeysChanged(int newKeys)
    {
        OnKeysChanged?.Invoke(newKeys);
    }

    /// <summary>
    /// Notifica un cambio en el n�mero de diamantes del jugador.
    /// </summary>
    public static void DiamondsChanged(int newDiamonds)
    {
        OnDiamondsChanged?.Invoke(newDiamonds);
    }

    /// <summary>
    /// Notifica el total actualizado de enemigos eliminados.
    /// </summary>
    public static void EnemyKilled(int totalKills)
    {
        OnEnemyKilled?.Invoke(totalKills);
    }

    /// <summary>
    /// Notifica que el jugador local ha sido registrado en el sistema.
    /// </summary>
    public static void LocalPlayerRegistered(PlayerController player)
    {
        OnLocalPlayerRegistered?.Invoke(player);
    }

    public static void LocalPlayerDied()
    {
        OnLocalPlayerDied?.Invoke();
    }


    /// <summary>
    /// Notifica que el jugador ha muerto.
    /// </summary>
    public static void PlayerDied()
    {
        OnPlayerDied?.Invoke();
    }

    /// <summary>
    /// Notifica que se ha alcanzado la condici�n de victoria.
    /// </summary>
    public static void Victory()
    {
        OnVictory?.Invoke();
    }



    /// <summary>
    /// Notifica un mensaje de estado de red para mostrarlo en pantalla.
    /// </summary>
    public static void NetworkStatusMessage(string message)
    {
        OnNetworkStatusMessage?.Invoke(message);
    }

    /// <summary>
    /// Limpia los eventos asociados al ciclo de vida de una escena.
    /// </summary>
    public static void ClearSceneEvents()
    {
        OnHealthChanged = null;
        OnKeysChanged = null;
        OnDiamondsChanged = null;
        OnEnemyKilled = null;
        OnLocalPlayerRegistered = null;
        OnNetworkStatusMessage = null;
    }

    /// <summary>
    /// Limpia todos los eventos registrados en el sistema global.
    /// </summary>
    public static void ClearAllEvents()
    {
        ClearSceneEvents();
        OnPlayerDied = null;
        OnVictory = null;
        OnLocalPlayerDied = null;
    }
}