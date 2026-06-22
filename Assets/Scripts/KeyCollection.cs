using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(UniqueEntity))]
public class KeyCollection : NetworkBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private UniqueEntity uniqueEntity;
    private bool recogido = false;
    public string EntityId => uniqueEntity?.EntityId ?? "UNKNOWN";
    public EntityType EntityType => uniqueEntity?.Type ?? EntityType.Pickup_Key;

    /// <summary>
    /// Inicializa la referencia de entidad única y valida el tipo configurado.
    /// </summary>
    private void Awake()
    {
        uniqueEntity = GetComponent<UniqueEntity>();

        if (uniqueEntity != null && uniqueEntity.Type != EntityType.Pickup_Key)
        {
            Debug.LogWarning($"[KeyCollection] {gameObject.name} tiene tipo {uniqueEntity.Type} en lugar de Pickup_Key");
        }
    }

    /// <summary>
    /// Detecta la colisión con el jugador e intenta recoger la llave.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (recogido) return;
        if (!collision.gameObject.CompareTag(playerTag)) return;

        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return;
        if (GameManager.Instance == null) return;

        //if (GameManager.Instance.TryAddKey(player.EntityId, EntityId))
        //{
        //    Debug.Log($"[{EntityType}:{EntityId}] collected by [Player:{player.EntityId}]");
        //    Destroy(gameObject);
        //}

        RecogerLlaveServerRpc(player.OwnerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RecogerLlaveServerRpc(ulong playerId)
    {
        if (recogido) return;
        recogido = true;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client))
        {
            PlayerController player = client.PlayerObject?.GetComponent<PlayerController>();   
            if (player != null) 
            {
                player.AddKeyServer();
            }

        }

        NetworkObject.Despawn(true);
    }

}
