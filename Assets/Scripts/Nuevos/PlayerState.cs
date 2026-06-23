using Unity.Netcode;
using UnityEngine;

public class PlayerState : NetworkBehaviour
{
    
    public static PlayerState LocalInstance { get; private set; } // Referencia estática al PlayerState local

    public NetworkVariable<bool> isReady = new NetworkVariable<bool>(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            LocalInstance = this;
            Debug.Log($"[PlayerState] OnNetworkSpawn: LocalInstance asignado. IsOwner={IsOwner}, ClientId={OwnerClientId}");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            LocalInstance = null;
            Debug.Log("[PlayerState] OnNetworkDespawn: LocalInstance limpiado.");
        }
    }

    [Rpc(SendTo.Server)]
    public void SetReadyServerRpc(bool ready)
    {
        isReady.Value = ready;
        Debug.Log($"[PlayerState] SetReadyServerRpc: ClientId={OwnerClientId}, ready={ready}");
        GameManager.Instance.CheckAllReady();
    }
}