using UnityEngine;

/// <summary>
/// Clase base abstracta para las estadísticas de enemigos.
/// Las probabilidades y cantidades de drops se configuran en MapConfig,
/// no aquí. Solo se mantienen los prefabs de drop (referencias visuales).
/// </summary>
public abstract class EnemyStats : CharacterStats
{
    [Header("Enemy Base")]
    [Range(0.1f, 1.5f)]
    [Tooltip("Multiplicador de velocidad para enemigos")]
    public float speedPenalty = 0.75f;

    [Header("Drops")]
    [Tooltip("Prefabs que puede soltar al morir (índice 0 = diamante, índice 1 = llave)")]
    public GameObject[] dropPrefabs;
}