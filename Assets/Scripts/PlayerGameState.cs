using System;

/// <summary>
/// Representa el estado persistente de recursos del jugador durante la partida.
/// </summary>
[Serializable]
public class PlayerGameState
{
    public const int MAX_KEYS = 9;
    public const int MAX_DIAMONDS = 999;

    public string playerId;

    public int keys = 0;
    public int diamonds = 0;

    /// <summary>
    /// Inicializa el estado del jugador con su identificador único.
    /// </summary>
    public PlayerGameState(string entityId)
    {
        playerId = entityId;
    }

    /// <summary>
    /// Obtiene o establece las llaves actuales aplicando límites válidos.
    /// </summary>
    public int Keys
    {
        get => keys;
        set
        {
            keys = UnityEngine.Mathf.Clamp(value, 0, MAX_KEYS);
            //GameEvents.KeysChanged(keys);
        }
    }

    /// <summary>
    /// Obtiene o establece los diamantes actuales aplicando límites válidos.
    /// </summary>
    public int Diamonds
    {
        get => diamonds;
        set
        {
            diamonds = UnityEngine.Mathf.Clamp(value, 0, MAX_DIAMONDS);
            //GameEvents.DiamondsChanged(diamonds);
        }
    }

    /// <summary>
    /// Incrementa una llave si no se ha alcanzado el máximo permitido.
    /// </summary>
    public void AddKey()
    {
        if (keys < MAX_KEYS)
        {
            Keys++;
            //GameEvents.KeysChanged();
        }
    }

    /// <summary>
    /// Incrementa un diamante si no se ha alcanzado el máximo permitido.
    /// </summary>
    public void AddDiamond()
    {
        if (diamonds < MAX_DIAMONDS)
        {
            Diamonds++;
            //GameEvents.DiamondsChanged();
        }
    }

    /// <summary>
    /// Consume una llave si hay disponibilidad y devuelve si la operación tuvo éxito.
    /// </summary>
    public bool UseKey()
    {
        if (keys > 0)
        {
            Keys--;
            //GameEvents.KeysChanged(keys);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Restablece a cero los recursos del jugador y notifica al HUD.
    /// </summary>
    public void ResetState()
    {
        keys = 0;
        diamonds = 0;
        //GameEvents.KeysChanged(keys);
        //GameEvents.DiamondsChanged(diamonds);
    }
}