using System.Collections.Generic;
using UnityEngine;

public class GuessAndScoreController : MonoBehaviour
{
    [Header("Refs")]
    public BoardGrid2D board;
    public ColorBoard2D data;

    // NEW: use this (assign your ConeMarker2D prefab in Inspector)
    public GameObject coneMarkerPrefab;

    [Header("Demo Setup")]
    public int players = 4;
    public int guessesPerPlayer = 2;

    [Header("Player Colors (optional)")]
    // Assign in Inspector, or we'll auto-generate if empty/short
    public Color[] playerColors;

    bool[,] occupied;
    readonly Dictionary<int, List<Vector2Int>> playerGuesses = new();
    int currentPlayer = 0;
    int guessesThisRound = 0;
    Vector2Int secretTarget;

    void Start()
    {
        occupied = new bool[data.cols, data.rows];
        for (int p = 0; p < players; p++) playerGuesses[p] = new List<Vector2Int>();

        // auto-fill colors if not provided
        EnsurePlayerColors();

        secretTarget = new Vector2Int(Random.Range(0, data.cols), Random.Range(0, data.rows));

        board.OnCellClicked.AddListener(HandleCellClicked);
    }

    void EnsurePlayerColors()
    {
        if (playerColors == null || playerColors.Length < players)
        {
            var list = new List<Color>();
            for (int i = 0; i < players; i++)
            {
                // evenly spaced hues
                float h = (i / (float)players);
                Color c = Color.HSVToRGB(h, 0.75f, 1f);
                list.Add(c);
            }
            playerColors = list.ToArray();
        }
    }

    void HandleCellClicked(Vector2Int cell)
    {
        if (occupied[cell.x, cell.y]) return;

        // OLD calls likely looked like this:
        // PlaceMarker(cell, currentPlayer);

        // Now we can keep that call by using the new 2-arg overload below:
        PlaceMarker(cell, currentPlayer);

        playerGuesses[currentPlayer].Add(cell);
        guessesThisRound++;

        currentPlayer = (currentPlayer + 1) % players;

        if (guessesThisRound >= players * guessesPerPlayer)
        {
            ScoreRound();
            Invoke(nameof(ResetDemo), 2f);
        }
    }

    // ---------- FIX 1: keep your old call sites working ----------
    // Overload that supplies a default color & label
    void PlaceMarker(Vector2Int cell, int playerIdx)
    {
        Color clr = (playerIdx >= 0 && playerIdx < playerColors.Length)
            ? playerColors[playerIdx]
            : Color.white;

        string label = (playerIdx + 1).ToString(); // optional number label
        PlaceMarker(cell, playerIdx, clr, label);
    }

    // ---------- FIX 2: this is the full version you already had ----------
    void PlaceMarker(Vector2Int cell, int playerIdx, Color playerColor, string labelText = null)
    {
        occupied[cell.x, cell.y] = true;

        // Make sure this field exists & is assigned in Inspector:
        var prefab = coneMarkerPrefab;
        if (!prefab)
        {
            Debug.LogError("coneMarkerPrefab is not assigned on GuessAndScoreController.");
            return;
        }

        var go = Instantiate(prefab, board.CellCenter(cell), Quaternion.identity, board.transform.parent);
        go.name = $"Marker_P{playerIdx}_({cell.x},{cell.y})";

        // If using the ConeMarker2D script:
        var cone = go.GetComponent<ConeMarker2D>();
        if (cone != null)
        {
            cone.Configure(playerColor, board, cell, labelText);

            // Optional: if using drag
            var drag = go.GetComponent<MarkerDraggable2D>();
            if (drag != null)
            {
                drag.board = board;
                var occ = FindFirstObjectByType<OccupancyGrid2D>();
                if (occ != null)
                {
                    drag.occupancy = occ;
                    // register initial occupancy with the grid system too (if using it)
                    occ.TryPlace(drag, cell);
                }
            }
        }
        else
        {
            // Fallback: tint a SpriteRenderer if prefab doesn’t have ConeMarker2D yet
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr) sr.color = playerColor;
            go.transform.localScale = board.CellSizeWorld() * 0.8f;
        }
    }

    void ScoreRound()
    {
        int Chebyshev(Vector2Int a, Vector2Int b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        int GuesserPoints(Vector2Int g)
        {
            int d = Chebyshev(g, secretTarget);
            if (d == 0) return 3;
            if (d == 1) return 2;
            if (d == 2) return 1;
            return 0;
        }

        DrawScoringFrame(secretTarget);

        for (int p = 0; p < players; p++)
        {
            int pts = 0;
            foreach (var g in playerGuesses[p]) pts += GuesserPoints(g);
            Debug.Log($"Player {p} scored {pts}");
        }
    }

    void DrawScoringFrame(Vector2Int center)
    {
        var frame = new GameObject("ScoreFrame");
        var sr = frame.AddComponent<SpriteRenderer>();
        sr.sprite = (board.boardRenderer.sprite);
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.color = new Color(1f, 1f, 1f, 0.15f);
        sr.sortingLayerName = "Overlay";
        frame.transform.position = board.CellCenter(center);
        Vector2 sz = board.CellSizeWorld() * 3f;
        frame.transform.localScale = new Vector3(sz.x, sz.y, 1f);
        Destroy(frame, 1.5f);
    }

    void ResetDemo()
    {
        foreach (Transform t in board.transform.parent)
            if (t.name.StartsWith("Marker_")) Destroy(t.gameObject);

        occupied = new bool[data.cols, data.rows];
        for (int p = 0; p < players; p++) playerGuesses[p].Clear();
        currentPlayer = 0;
        guessesThisRound = 0;
        secretTarget = new Vector2Int(Random.Range(0, data.cols), Random.Range(0, data.rows));
        Debug.Log("New demo round started.");
    }
}
