using Mirror;
using UnityEngine;

[RequireComponent(typeof(ValidDropSpot))]
[DisallowMultipleComponent]
public class ValidDropSpotNet : NetworkBehaviour
{
    [SerializeField] ValidDropSpot spot;

    [SyncVar(hook = nameof(OnOccupantChanged))]
    GameObject _occupant;

    void Awake()
    {
        if (!spot) spot = GetComponent<ValidDropSpot>();
    }

    public GameObject Occupant => _occupant;
    public bool IsOccupied => _occupant != null;

    [Command(requiresAuthority = false)]
    public void CmdRequestClaim(GameObject coin, NetworkConnectionToClient sender = null)
    {
        if (!isServer || spot == null)
        {
            TargetClaimResult(sender, false, Vector3.zero);
            return;
        }

        if (!spot.enabledForPlacement || _occupant != null)
        {
            TargetClaimResult(sender, false, Vector3.zero);
            return;
        }

        var id = coin ? coin.GetComponent<NetworkIdentity>() : null;
        if (id == null || id.connectionToClient != sender)
        {
            TargetClaimResult(sender, false, Vector3.zero);
            return;
        }

        _occupant = coin;
        TargetClaimResult(sender, true, spot.GetCenterWorld());
    }

    [Command(requiresAuthority = false)]
    public void CmdReleaseIfOccupant(GameObject coin, NetworkConnectionToClient sender = null)
    {
        if (!isServer || _occupant == null) return;

        var id = coin ? coin.GetComponent<NetworkIdentity>() : null;
        if (id != null && id.connectionToClient == sender && _occupant == coin)
        {
            _occupant = null;
        }
    }

    [TargetRpc]
    void TargetClaimResult(NetworkConnection target, bool ok, Vector3 snapWorldPos)
    {
        OnClientClaimResult?.Invoke(ok, snapWorldPos);
        OnClientClaimResult = null;
    }

    public System.Action<bool, Vector3> OnClientClaimResult;

    void OnOccupantChanged(GameObject oldVal, GameObject newVal)
    {
        if (spot == null) spot = GetComponent<ValidDropSpot>();
        spot.occupant = newVal;
        spot.isOccupied = newVal != null;
        spot.enabledForPlacement = (newVal == null);
    }
}
