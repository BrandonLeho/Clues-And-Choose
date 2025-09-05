using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class MarkerDraggable2D : MonoBehaviour
{
    public enum DragMode { DragHold, ClickPickDrop }
    public enum PlacementMode { FreeAnywhere, SnapToBoard }   // NEW

    [Header("Refs")]
    public BoardGrid2D board;                 // still supported if you switch to SnapToBoard later
    public OccupancyGrid2D occupancy;         // still supported if you switch to SnapToBoard later
    public SpriteRenderer bodyForSorting;

    [Header("Behavior")]
    public DragMode dragMode = DragMode.DragHold;
    public PlacementMode placementMode = PlacementMode.FreeAnywhere;   // NEW default
    public bool snapWhileDragging = true;       // only used in SnapToBoard
    public bool requireFreeCellToDrop = true;   // only used in SnapToBoard

    [Header("Visuals")]
    public Transform hoverHighlight;
    public int dragSortingBoost = 10;

    Camera cam;
    Collider2D col;

    bool isDragging;
    bool picked;                   // for ClickPickDrop
    Vector2Int originalCell;
    Vector3 originalPos;
    int originalSortingOrder;

    void Awake()
    {
        cam = Camera.main;
        col = GetComponent<Collider2D>();
    }

    bool PointerOverMe(Vector3 mouseWorld) => col && col.OverlapPoint(mouseWorld);

    // --- Helper: get cursor in world at this marker's Z plane ---
    Vector3 MouseWorldAtMyZ()
    {
        if (!cam) return transform.position;
        float depth = cam.orthographic ? 0f : Mathf.Abs(cam.transform.position.z - transform.position.z);
        var mw = cam.ScreenToWorldPoint(new Vector3(PointerInput.ScreenPos.x, PointerInput.ScreenPos.y, depth));
        mw.z = transform.position.z;
        return mw;
    }

    void Update()
    {
        if (!cam) return;

        Vector3 mouseWorld = MouseWorldAtMyZ();

        // Input entry points (replace old OnMouseDown/Up)
        if (dragMode == DragMode.DragHold)
        {
            if (!isDragging)
            {
                if (PointerInput.LeftDown && PointerOverMe(mouseWorld)) BeginDrag();
            }
            else
            {
                if (PointerInput.LeftUp) TryDropAtCursor();
            }
        }
        else // ClickPickDrop
        {
            if (PointerInput.LeftDown && PointerOverMe(mouseWorld))
            {
                if (!picked) BeginDrag();
                else TryDropAtCursor();
            }
        }

        // While dragging: move marker
        if (isDragging)
        {
            if (placementMode == PlacementMode.FreeAnywhere)
            {
                // No snapping. Just follow the cursor.
                if (hoverHighlight) hoverHighlight.gameObject.SetActive(false);
                transform.position = mouseWorld;
            }
            else // SnapToBoard (old behavior)
            {
                if (snapWhileDragging && board && board.TryWorldToCell(mouseWorld, out var cell))
                {
                    if (hoverHighlight)
                    {
                        hoverHighlight.gameObject.SetActive(true);
                        hoverHighlight.position = board.CellCenter(cell);
                        hoverHighlight.localScale = board.CellSizeWorld();
                    }
                    transform.position = board.CellCenter(cell);
                }
                else
                {
                    if (hoverHighlight) hoverHighlight.gameObject.SetActive(false);
                    transform.position = mouseWorld;
                }
            }

            if (PointerInput.CancelPressed) CancelDrag();
        }
    }

    void BeginDrag()
    {
        originalPos = transform.position;

        // Only interact with occupancy if we're in SnapToBoard mode
        if (placementMode == PlacementMode.SnapToBoard && occupancy)
        {
            if (!occupancy.TryGetCell(this, out originalCell) && board)
            {
                if (board.TryWorldToCell(transform.position, out var c)) { occupancy.TryPlace(this, c); originalCell = c; }
            }
            occupancy.Release(this);
        }

        if (bodyForSorting)
        {
            originalSortingOrder = bodyForSorting.sortingOrder;
            bodyForSorting.sortingOrder = originalSortingOrder + dragSortingBoost;
        }
        if (hoverHighlight) hoverHighlight.gameObject.SetActive(placementMode == PlacementMode.SnapToBoard);

        isDragging = true;
        picked = true;
    }

    void TryDropAtCursor()
    {
        var mouseWorld = MouseWorldAtMyZ();

        if (placementMode == PlacementMode.FreeAnywhere)
        {
            // Just drop exactly where the cursor is.
            transform.position = mouseWorld;
            EndDrag();
            return;
        }

        // --- SnapToBoard path (previous logic) ---
        if (!board || !board.TryWorldToCell(mouseWorld, out var cell))
        {
            CancelDrag();
            return;
        }

        bool ok = (!requireFreeCellToDrop && occupancy && occupancy.InBounds(cell))
                  || (occupancy && !occupancy.IsOccupied(cell))
                  || cell == originalCell;

        if (occupancy && ok)
        {
            if (!occupancy.TryPlace(this, cell))
            {
                if (!occupancy.TryPlace(this, originalCell)) transform.position = originalPos;
            }
            EndDrag();
        }
        else
        {
            if (occupancy && !occupancy.TryPlace(this, originalCell))
                transform.position = originalPos;
            EndDrag();
        }
    }

    void CancelDrag()
    {
        if (placementMode == PlacementMode.SnapToBoard && occupancy)
        {
            if (!occupancy.TryPlace(this, originalCell))
                transform.position = originalPos;
        }
        else
        {
            transform.position = originalPos;
        }
        EndDrag();
    }

    void EndDrag()
    {
        isDragging = false;
        if (dragMode == DragMode.DragHold) picked = false;
        if (hoverHighlight) hoverHighlight.gameObject.SetActive(false);
        if (bodyForSorting) bodyForSorting.sortingOrder = originalSortingOrder;
    }

    // Called by Occupancy to snap this marker to a board cell.
    // Keeps scale unchanged (so FreeAnywhere mode isn't overridden).
    public void SetSnappedTransform(Vector3 worldCenter, Vector2 cellSize)
    {
        transform.position = worldCenter;
        // If you DO want auto-fit to the cell, uncomment the next line:
        // transform.localScale = Vector3.one * Mathf.Min(cellSize.x, cellSize.y) * 0.8f;
    }
}
