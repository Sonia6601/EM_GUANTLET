using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(UniqueEntity))]
public class ChestController : MonoBehaviour
{

    private bool collected = false;
    

    private UniqueEntity uniqueEntity;
    

    public string EntityId => uniqueEntity?.EntityId ?? "UNKNOWN";
    public EntityType EntityType => uniqueEntity?.Type ?? EntityType.Interactive_Chest;
    
    /// <summary>
    /// Inicializa la referencia de entidad única y valida su tipo configurado.
    /// </summary>
    private void Awake()
    {

        uniqueEntity = GetComponent<UniqueEntity>();
        
        // Validación del tipo correcto
        if (uniqueEntity != null && uniqueEntity.Type != EntityType.Interactive_Chest)
        {
            Debug.LogWarning($"[ChestController] {gameObject.name} tiene tipo {uniqueEntity.Type} en lugar de Interactive_Chest");
        }
    }

    /// <summary>
    /// Detecta la interacción con el jugador e intenta activar la victoria una sola vez.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collected) return;
        if (!collision.gameObject.CompareTag("Player")) return;

        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null) return;

        Debug.Log($"[{EntityType}:{EntityId}] opened by [Player:{player.EntityId}]");

        if (GameManager.Instance != null && GameManager.Instance.TryTriggerVictory(player.EntityId, EntityId))
        {
            collected = true;
        }
    }


}
