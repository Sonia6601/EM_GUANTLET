using UnityEngine;

[CreateAssetMenu(fileName = "NewPlayerStats", menuName = "Game/Stats/Player Stats")]
public class PlayerStats : CharacterStats
{
    [Header("Player Specific")]
    [Range(0.1f, 3f)]
    [Tooltip("Multiplicador de velocidad para el jugador")]
    public float speedBonus = 1.25f;
    
    [Range(0.1f, 2f)]
    [Tooltip("Cooldown entre ataques en segundos")]
    public float attackCooldown = 0.5f;
    public bool select = false;
}