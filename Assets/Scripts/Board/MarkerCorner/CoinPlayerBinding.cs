using UnityEngine;

public class CoinPlayerBinding : MonoBehaviour
{
    public uint ownerNetId;
    public CoinMakerUI ui;

    void Awake()
    {
        if (!ui) ui = GetComponent<CoinMakerUI>();
    }

    public void RefreshColor()
    {
        var reg = ColorLockRegistry.Instance;
        if (!reg || ui == null) return;

        if (reg.colorByOwner.TryGetValue(ownerNetId, out var c))
            ui.SetPlayerColor(c);
    }
}
