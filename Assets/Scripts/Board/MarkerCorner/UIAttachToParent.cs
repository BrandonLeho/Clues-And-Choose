using Mirror;
using UnityEngine;

public class UiAttachToParent : NetworkBehaviour
{
    [SerializeField] string parentObjectName = "CoinsContainer";

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!string.IsNullOrEmpty(parentObjectName))
        {
            var parentGO = GameObject.Find(parentObjectName);
            if (parentGO != null)
            {
                transform.SetParent(parentGO.transform, worldPositionStays: false);
            }
            else
            {
                Debug.LogWarning($"UiAttachToParent: Could not find parent '{parentObjectName}' on client {netId}.");
            }
        }
    }
}
