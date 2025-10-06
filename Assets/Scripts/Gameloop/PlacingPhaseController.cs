using Mirror;
using UnityEngine;

public sealed class PlacingPhaseController : NetworkBehaviour
{
    [Header("Rules")]
    [SyncVar] public bool reverseSecondCycleEnabled = true;

    public static PlacingPhaseController Instance { get; private set; }
    void Awake() => Instance = this;

    [SyncVar] int cyclesCompleted = 0;
    [SyncVar] int targetCol = -1;
    [SyncVar] int targetRow = -1;
    [SyncVar] Color targetColor = Color.white;


    public override void OnStartServer()
    {
        base.OnStartServer();
        CoinPlacementTurnManager.OnServerFirstCycleCompleted += HandleServerCycleCompleted;
    }
    public override void OnStopServer()
    {
        CoinPlacementTurnManager.OnServerFirstCycleCompleted -= HandleServerCycleCompleted;
        base.OnStopServer();
    }

    [Command(requiresAuthority = false)]
    public void CmdStartPlacingPhase()
    {
        cyclesCompleted = 0;
        if (CoinPlacementTurnManager.Instance)
            CoinPlacementTurnManager.Instance.ServerBeginCycleAtFirst(false);

        RpcEnablePerTurnLocks();
    }

    [Server]
    void HandleServerCycleCompleted()
    {
        cyclesCompleted++;

        if (cyclesCompleted == 1)
        {
            RpcEndFirstCycle_LockAll();

            var cgConn = GetClueGiverConnection();
            if (cgConn != null)
                TargetShowEndRoundPrompt(cgConn);
        }
        else
        {
            ServerBeginScoring();
        }
    }

    [Command(requiresAuthority = false)]
    public void CmdClueGiverChoose(bool endNow, NetworkConnectionToClient sender = null)
    {
        if (!IsConnectionClueGiver(sender)) return;

        RpcAnnounceRoundDecision(endNow);

        RpcHideEndRoundPrompt();

        if (endNow)
        {
            ServerBeginScoring();
        }
        else
        {
            if (CoinPlacementTurnManager.Instance)
                CoinPlacementTurnManager.Instance.ServerBeginCycleAtFirst(reverseSecondCycleEnabled);

            RpcEnablePerTurnLocks();
        }
    }

    [Server]
    bool IsConnectionClueGiver(NetworkConnectionToClient conn)
    {
        if (conn == null || conn.identity == null) return false;
        uint cg = RoundManager.Instance ? RoundManager.Instance.ServerGetClueGiverNetIdUnsafe() : 0u;
        return conn.identity.netId == cg;
    }

    [Server]
    NetworkConnectionToClient GetClueGiverConnection()
    {
        if (!RoundManager.Instance) return null;
        uint cg = RoundManager.Instance.ServerGetClueGiverNetIdUnsafe();
        if (cg == 0 || !NetworkServer.spawned.TryGetValue(cg, out var id)) return null;
        return id.connectionToClient;
    }

    [ClientRpc]
    void RpcEnablePerTurnLocks()
    {
        var binder = FindFirstObjectByType<CoinTurnLockBinder>();
        if (binder) binder.SetModeActive(true);
    }

    [ClientRpc]
    void RpcEndFirstCycle_LockAll()
    {
        var binder = FindFirstObjectByType<CoinTurnLockBinder>();
        if (binder) binder.SetModeActive(false);

        var mgr = CoinRoundLockManager.Instance;
        if (mgr) mgr.LockAllCoins();

        Debug.Log("[Phase] First placement cycle finished → All players locked. Waiting for clue giver decision…");
    }

    [TargetRpc]
    void TargetShowEndRoundPrompt(NetworkConnection target)
    {
        EndRoundPromptUI.Instance?.Show();
        Debug.Log("[Phase] End Round prompt shown (clue giver only).");
    }

    [ClientRpc]
    void RpcHideEndRoundPrompt()
    {
        EndRoundPromptUI.Instance?.Hide();
    }

    [ClientRpc]
    void RpcAnnounceRoundDecision(bool endNow)
    {
        Debug.Log(endNow ? "[Phase] Clue giver chose: END ROUND" : "[Phase] Clue giver chose: ANOTHER CYCLE");
    }

    [Server]
    void ServerBeginScoring()
    {
        Debug.Log("[Phase] Begin SCORING");

        if (targetCol < 0 || targetRow < 0)
        {
            Debug.LogWarning("[Scoring] No target set yet. Did the clue giver select a card choice?");
            return;
        }

        Debug.Log($"[Scoring] Chosen target → col={(targetCol + 1)}, row={RowLetters(targetRow)} (0-based: c={targetCol}, r={targetRow}) color={ColorToHex(targetColor)}");

        var board = BoardSpotsNet.Instance;
        if (board == null)
        {
            Debug.LogWarning("[Scoring] BoardSpotsNet not found.");
            return;
        }

        foreach (var kv in board.occupancy)
        {
            int spotIndex = kv.Key;
            uint coinNetId = kv.Value;
            if (coinNetId == 0) continue;

            if (!board.TryGetSpotCoord(spotIndex, out int coinCol, out int coinRow))
                continue;

            int dx = Mathf.Abs(coinCol - targetCol);
            int dy = Mathf.Abs(coinRow - targetRow);
            int manhattan = dx + dy;
            float euclid = Mathf.Sqrt(dx * dx + dy * dy);

            string rowLabel = RowLetters(coinRow);
            string ownerStr = "";
            if (NetworkServer.spawned.TryGetValue(coinNetId, out var id))
            {
                uint owner = id.connectionToClient?.identity ? id.connectionToClient.identity.netId : 0u;
                ownerStr = owner != 0 ? $" owner={owner}" : "";
            }

            Debug.Log($"[Scoring] Coin netId={coinNetId}{ownerStr} at (col={(coinCol + 1)}, row={rowLabel}) " +
                    $"→ dx={dx}, dy={dy}, manhattan={manhattan}, euclid={euclid:0.###}");
        }
    }

    // Just in case I want to change the reverse order during runtime
    public void CmdSetReverseSecondCycle(bool value)
    {
        reverseSecondCycleEnabled = value;
        RpcAnnounceReverseToggle(value);
    }
    [ClientRpc]
    void RpcAnnounceReverseToggle(bool value)
    {
        Debug.Log($"[Phase] Reverse second cycle: {(value ? "ON" : "OFF")}");
    }

    [Command(requiresAuthority = false)]
    public void CmdSetChosenTarget(int col, int row, Color color, NetworkConnectionToClient sender = null)
    {
        if (!IsConnectionClueGiver(sender)) return;
        targetCol = col;
        targetRow = row;
        targetColor = color;

        Debug.Log($"[Scoring] Target set by clue giver → col={(col + 1)}, row={RowLetters(row)} color={ColorToHex(color)}");
    }

    static string RowLetters(int idx)
    {
        idx = Mathf.Max(0, idx);
        string s = "";
        while (idx >= 0) { int r = idx % 26; s = (char)('A' + r) + s; idx = idx / 26 - 1; }
        return s;
    }
    static string ColorToHex(Color c)
    {
        Color32 c32 = c;
        return $"#{c32.r:X2}{c32.g:X2}{c32.b:X2}{c32.a:X2}";
    }

}
