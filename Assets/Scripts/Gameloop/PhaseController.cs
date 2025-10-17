using Mirror;
using UnityEngine;

public sealed class PhaseController : NetworkBehaviour
{
    [Header("Rules")]
    [SyncVar] public bool reverseSecondCycleEnabled = true;
    [SerializeField, Min(1)] int gridRows = 16;
    [SerializeField] bool cardRowZeroIsBottom = true;

    [Header("Scoring")]
    [SerializeField, Min(0)] int pointsAtExact = 3;

    [Header("Clue Giver Proximity Scoring")]
    [SerializeField, Min(1)] int vicinitySize = 2;
    [SerializeField, Min(0)] int pointsPerNearbyCoinManyPlayers = 1;
    [SerializeField, Min(0)] int pointsPerNearbyCoinFewPlayers = 2;
    [SerializeField, Min(1)] int fewPlayersThreshold = 3;

    public static PhaseController Instance { get; private set; }
    void Awake() => Instance = this;

    [SyncVar] int cyclesCompleted = 0;
    [SyncVar] int targetCol = -1;
    [SyncVar] int targetRow = -1;
    [SyncVar] Color targetColor = Color.white;

    public static event System.Action<int, int, Color> OnClientTargetChosen;
    public bool ClientHasTarget => targetCol >= 0 && targetRow >= 0;

    bool _waitingForScoringBanner;
    bool _coinsReturnedThisRound;

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

        _waitingForScoringBanner = true;
        _coinsReturnedThisRound = false;
        RpcShowScoringBanner();

        if (targetCol < 0 || targetRow < 0)
        {
            Debug.LogWarning("[Scoring] No target set. Did the clue giver select a card choice?");
            return;
        }

        var board = BoardSpotsNet.Instance;
        if (!board)
        {
            Debug.LogWarning("[Scoring] BoardSpotsNet not found.");
            return;
        }

        string clueGiverName = RosterStore.CurrentClueGiverName;
        int playerCount = (RosterStore.Instance != null && RosterStore.Instance.Names != null)
            ? RosterStore.Instance.Names.Count : 0;
        int perNearbyCoin = (playerCount <= fewPlayersThreshold)
            ? pointsPerNearbyCoinFewPlayers : pointsPerNearbyCoinManyPlayers;

        int awardedTotal = 0;
        int nearbyCountForClueGiver = 0;

        foreach (var kv in board.occupancy)
        {
            int spotIndex = kv.Key;
            uint coinNetId = kv.Value;
            if (coinNetId == 0) continue;

            if (!board.TryGetSpotCoord(spotIndex, out int coinCol, out int coinRow))
                continue;

            int cellsAway = CellsAwayChebyshev(coinCol, coinRow, targetCol, targetRow);
            int points = Mathf.Max(0, pointsAtExact - cellsAway);

            string ownerName = null;
            ServerTryResolvePlayerName(coinNetId, out ownerName);

            if (points > 0 && !string.IsNullOrWhiteSpace(ownerName))
            {
                ScoreRegistry.AddScore(ownerName, points);
                awardedTotal += points;
                Debug.Log($"[Scoring] +{points} → {ownerName} [{cellsAway} away]");
            }
            else
            {
                Debug.Log($"[Scoring] +0 (no falloff points) [{cellsAway} away]");
            }

            if (!string.IsNullOrWhiteSpace(clueGiverName) &&
                !string.Equals(ownerName, clueGiverName) &&
                cellsAway < vicinitySize)
            {
                nearbyCountForClueGiver++;
            }
        }

        if (!string.IsNullOrWhiteSpace(clueGiverName) && nearbyCountForClueGiver > 0 && perNearbyCoin > 0)
        {
            int clueGiverBonus = nearbyCountForClueGiver * perNearbyCoin;
            ScoreRegistry.AddScore(clueGiverName, clueGiverBonus);
            Debug.Log($"[Scoring] Clue-giver bonus: +{clueGiverBonus} → {clueGiverName} " +
                      $"({nearbyCountForClueGiver} nearby coin(s) × {perNearbyCoin} each; " +
                      $"vicinitySize={vicinitySize}, playerCount={playerCount})");
            awardedTotal += clueGiverBonus;
        }
        else
        {
            Debug.Log($"[Scoring] Clue-giver bonus: +0 (nearby={nearbyCountForClueGiver}, perCoin={perNearbyCoin}, name set={(!string.IsNullOrWhiteSpace(clueGiverName))})");
        }

        Debug.Log($"[Scoring] Round total awarded: {awardedTotal} point(s).");

        var cgConn = GetClueGiverConnection();
        if (cgConn != null)
            TargetClueGiverSlideOutCardAndRespawn(cgConn);
    }

    [ClientRpc]
    void RpcShowScoringBanner()
    {
        var tn = FindFirstObjectByType<TurnNotification>();
        if (tn) tn.PlaySystemMessage("SCORING");
        GridDimmerOverlay.Instance?.FadeInDuringScoring();
        Debug.Log(GridDimmerOverlay.Instance);
    }

    [Command(requiresAuthority = false)]
    public void CmdNotifyScoringBannerFinished()
    {
        if (!_waitingForScoringBanner) return;
        _waitingForScoringBanner = false;

        if (!_coinsReturnedThisRound && RoundManager.Instance != null)
        {
            RoundManager.Instance.ServerResetAllCoinsToHome(tween: true);
            _coinsReturnedThisRound = true;
        }

        if (RoundManager.Instance)
            RoundManager.Instance.ServerAdvanceRound();
    }


    bool ServerTryResolvePlayerName(uint coinNetId, out string ownerName)
    {
        ownerName = null;

        if (!NetworkServer.spawned.TryGetValue(coinNetId, out var coinIdentity) || !coinIdentity)
            return false;

        var coinGO = coinIdentity.gameObject;
        var coin = coinGO.GetComponent<NetworkCoin>();
        if (!coin || coin.ownerNetId == 0)
        {
            ownerName = coinGO.name;
            return !string.IsNullOrWhiteSpace(ownerName);
        }

        if (!NetworkServer.spawned.TryGetValue(coin.ownerNetId, out var ownerIdentity) || !ownerIdentity)
            return false;

        var ownerGO = ownerIdentity.gameObject;

        var pns = ownerGO.GetComponent<PlayerNameSync>();
        if (pns != null && !string.IsNullOrWhiteSpace(pns.DisplayName))
        {
            ownerName = pns.DisplayName.Trim();
            return true;
        }

        var chat = ownerGO.GetComponent<NetworkChat>();
        if (chat != null && !string.IsNullOrWhiteSpace(chat.DisplayName))
        {
            ownerName = chat.DisplayName.Trim();
            return true;
        }

        ownerName = ownerGO.name;
        return !string.IsNullOrWhiteSpace(ownerName);
    }

    [TargetRpc]
    void TargetClueGiverSlideOutCardAndRespawn(NetworkConnection target)
    {
        var anim = FindFirstObjectByType<CardStackFlyInAnimator>();
        if (anim) anim.PlaySlideOutAndRespawn();
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

        RpcNotifyTargetChosen(targetCol, targetRow, targetColor);
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

    [ClientRpc]
    void RpcNotifyTargetChosen(int col, int row, Color color)
    {
        OnClientTargetChosen?.Invoke(col, row, color);
    }
}
