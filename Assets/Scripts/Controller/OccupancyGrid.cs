using System.Collections.Generic;
using UnityEngine;

public class OccupancyGrid2D : MonoBehaviour
{
    public BoardGrid2D board; // assign in Inspector

    // cell -> marker
    private readonly Dictionary<Vector2Int, MarkerDraggable2D> cellOwners = new();
    // marker -> cell
    private readonly Dictionary<MarkerDraggable2D, Vector2Int> markerCells = new();

    public bool InBounds(Vector2Int c) =>
        c.x >= 0 && c.y >= 0 && c.x < board.data.cols && c.y < board.data.rows;

    public bool IsOccupied(Vector2Int c) => cellOwners.ContainsKey(c);

    public MarkerDraggable2D OwnerAt(Vector2Int c) =>
        cellOwners.TryGetValue(c, out var m) ? m : null;

    public bool TryPlace(MarkerDraggable2D marker, Vector2Int cell)
    {
        if (!InBounds(cell) || IsOccupied(cell)) return false;

        // if marker already placed elsewhere, release that cell
        if (markerCells.TryGetValue(marker, out var prev))
        {
            cellOwners.Remove(prev);
        }
        cellOwners[cell] = marker;
        markerCells[marker] = cell;
        marker.SetSnappedTransform(board.CellCenter(cell), board.CellSizeWorld());
        return true;
    }

    public bool TryMove(MarkerDraggable2D marker, Vector2Int newCell)
    {
        if (!InBounds(newCell)) return false;
        if (markerCells.TryGetValue(marker, out var oldCell) && oldCell == newCell)
            return true; // already there

        if (IsOccupied(newCell)) return false;

        // release old
        if (markerCells.TryGetValue(marker, out var old))
            cellOwners.Remove(old);

        cellOwners[newCell] = marker;
        markerCells[marker] = newCell;
        marker.SetSnappedTransform(board.CellCenter(newCell), board.CellSizeWorld());
        return true;
    }

    public void Release(MarkerDraggable2D marker)
    {
        if (markerCells.TryGetValue(marker, out var cell))
        {
            markerCells.Remove(marker);
            cellOwners.Remove(cell);
        }
    }

    public bool TryGetCell(MarkerDraggable2D marker, out Vector2Int cell) =>
        markerCells.TryGetValue(marker, out cell);
}
