using Mirror;
using UnityEngine;

public sealed class PlacingPhaseController : NetworkBehaviour
{
    [Header("Rules")]
    [SyncVar] public bool reverseSecondCycleEnabled = true;
    [SerializeField, Min(1)] int gridRows = 16;
    [SerializeField] bool cardRowZeroIsBottom = true;

    [Header("Scoring")]
    [SerializeField, Min(0)] int pointsAtExact = 3;

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
            Debug.LogWarning("[Scoring] No target set. Did the clue giver select a card choice?");
            return;
        }

        Debug.Log($"[Scoring] Target → col={(targetCol + 1)}, row={RowLetters(targetRow)} color={ColorToHex(targetColor)}");

        var board = BoardSpotsNet.Instance;
        if (!board)
        {
            Debug.LogWarning("[Scoring] BoardSpotsNet not found.");
            return;
        }

        int awardedTotal = 0;
        foreach (var kv in board.occupancy)
        {
            int spotIndex = kv.Key;
            uint coinNetId = kv.Value;
            if (coinNetId == 0) continue;

            if (!board.TryGetSpotCoord(spotIndex, out int coinCol, out int coinRow))
                continue;

            int cellsAway = CellsAwayChebyshev(coinCol, coinRow, targetCol, targetRow);
            int points = Mathf.Max(0, pointsAtExact - cellsAway);

            if (points > 0 && ServerTryResolvePlayerName(coinNetId, out string ownerName))
            {
                ScoreRegistry.AddScore(ownerName, points);
                awardedTotal += points;
                Debug.Log($"[Scoring] +{points} → {ownerName} (coin {coinNetId}) at (col={(coinCol + 1)}, row={RowLetters(coinRow)}) " +
                        $"[{cellsAway} cell(s) away]");
            }
            else
            {
                Debug.Log($"[Scoring] Coin {coinNetId} at (col={(coinCol + 1)}, row={RowLetters(coinRow)}) → {cellsAway} cell(s) away → +0");
            }
        }

        Debug.Log($"[Scoring] Round total awarded: {awardedTotal} point(s).");
    }


    bool ServerTryResolvePlayerName(uint coinNetId, out string ownerName)
    {
        ownerName = null;
        if (!NetworkServer.spawned.TryGetValue(coinNetId, out var id) || id == null)
            return false;

        var go = id.gameObject;
        var comp1 = go.GetComponent("PlayerNameTag");
        if (comp1 != null)
        {
            var field = comp1.GetType().GetField("playerName");
            if (field != null) { ownerName = field.GetValue(comp1) as string; if (!string.IsNullOrWhiteSpace(ownerName)) return true; }
        }

        var comp2 = go.GetComponent("RegistryNameProvider");
        if (comp2 != null)
        {
            var prop = comp2.GetType().GetProperty("PlayerName");
            if (prop != null) { ownerName = prop.GetValue(comp2) as string; if (!string.IsNullOrWhiteSpace(ownerName)) return true; }
        }

        Transform t = go.transform;
        for (int i = 0; i < 4 && t != null; i++, t = t.parent)
        {
            var comps = t.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c == null) continue;
                var ty = c.GetType();
                var f = ty.GetField("ownerName") ?? ty.GetField("playerName");
                if (f != null)
                {
                    var v = f.GetValue(c) as string;
                    if (!string.IsNullOrWhiteSpace(v)) { ownerName = v; return true; }
                }
                var p = ty.GetProperty("OwnerName") ?? ty.GetProperty("PlayerName");
                if (p != null)
                {
                    var v = p.GetValue(c) as string;
                    if (!string.IsNullOrWhiteSpace(v)) { ownerName = v; return true; }
                }
            }
        }

        ownerName = go.name;
        return !string.IsNullOrWhiteSpace(ownerName);
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
    public void CmdSetChosenTarget(int colFromCard, int rowFromCard, Color color, NetworkConnectionToClient sender = null)
    {
        if (!IsConnectionClueGiver(sender)) return;

        int normalizedRow = cardRowZeroIsBottom
            ? (gridRows - 1 - rowFromCard)
            : rowFromCard;

        normalizedRow = Mathf.Clamp(normalizedRow, 0, gridRows - 1);

        targetCol = colFromCard;
        targetRow = normalizedRow;
        targetColor = color;

        Debug.Log($"[Scoring] Target set → col={(targetCol + 1)}, row={RowLetters(targetRow)} " +
                $"(raw card row={rowFromCard}) color={ColorToHex(targetColor)}");
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
    static int CellsAwayChebyshev(int colA, int rowA, int colB, int rowB)
        => Mathf.Max(Mathf.Abs(colA - colB), Mathf.Abs(rowA - rowB));

}
