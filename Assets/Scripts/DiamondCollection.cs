using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(UniqueEntity))]
public class DiamondCollection : NetworkBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private UniqueEntity uniqueEntity;
    private bool recogido = false;
    public string EntityId => uniqueEntity?.EntityId ?? "UNKNOWN";
    public EntityType EntityType => uniqueEntity?.Type ?? EntityType.Pickup_Diamond;

    /// <summary>
    /// Inicializa la referencia de entidad única y valida el tipo configurado.
    /// </summary>
    private void Awake()
    {
        uniqueEntity = GetComponent<UniqueEntity>();

        if (uniqueEntity != null && uniqueEntity.Type != EntityType.Pickup_Diamond)
        {
            Debug.LogWarning($"[DiamondCollection] {gameObject.name} tiene tipo {uniqueEntity.Type} en lugar de Pickup_Diamond");
        }
    }

    /// <summary>
    /// Detecta la colisión con el jugador e intenta recoger el diamante.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (recogido) return;
        if (!collision.gameObject.CompareTag(playerTag)) return;

        PlayerController player = collision.gameObject.GetComponent<PlayerController>();
        if (player == null || !player.IsOwner) return;
        if (GameManager.Instance == null) return;

        //if (GameManager.Instance.TryAddDiamond(player.EntityId, EntityId))
        //{
        //    Debug.Log($"[{EntityType}:{EntityId}] collected by [Player:{player.EntityId}]");
        //    Destroy(gameObject);
        //}

        RecogerDiamantesServerRpc(player.OwnerClientId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RecogerDiamantesServerRpc(ulong playerId)
    {
        //if (recogido) return; //Si ha sido recogido, no hagas nada
        //recogido = true; //se marca como recogido

        //if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var cliente)) //si el jugador existe
        //{
        //    PlayerController player = cliente.PlayerObject?.GetComponent<PlayerController>();
        //    if (player == null) return;
        //    GameManager.Instance.TryAddDiamond(player.EntityId, EntityId); //lo intenta recoger llamando a TryAddDiamon
        //}

        //NotificarClienteClientRpc(playerId); //Notifica
        //NetworkObject.Despawn(true);

        if (recogido) return;
        recogido = true;

        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(playerId, out var client))
        {
            PlayerController player = client.PlayerObject?.GetComponent<PlayerController>();
            if (player != null)
            {
                player.AddDiamondServer();
            }
        }

        NetworkObject.Despawn(true);
    }

    
}
