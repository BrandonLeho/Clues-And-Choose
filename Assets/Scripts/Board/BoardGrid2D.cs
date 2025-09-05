using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(SpriteRenderer))]
public class BoardGrid2D : MonoBehaviour
{
    public ColorBoard2D data;
    public SpriteRenderer boardRenderer;
    public GameObject hoverHighlightPrefab;

    public UnityEvent<Vector2Int> OnCellClicked;
    public UnityEvent<Vector2Int> OnCellHovered;

    Bounds _bounds;
    Vector2 _cellSizeWorld;
    Transform _hover;

    void Awake()
    {
        if (!boardRenderer) boardRenderer = GetComponent<SpriteRenderer>();
        _bounds = boardRenderer.bounds;
        _cellSizeWorld = new Vector2(_bounds.size.x / data.cols, _bounds.size.y / data.rows);

        if (hoverHighlightPrefab)
        {
            _hover = Instantiate(hoverHighlightPrefab, transform.parent).transform;
            _hover.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        if (!Camera.main) return;
        var cam = Camera.main;

        Vector3 mouseWorld = cam.ScreenToWorldPoint(
            new Vector3(PointerInput.ScreenPos.x, PointerInput.ScreenPos.y,
                        Mathf.Abs(cam.transform.position.z - transform.position.z)));
        // keep z at board plane
        mouseWorld.z = transform.position.z;

        Vector2Int cell;
        if (TryWorldToCell(mouseWorld, out cell))
        {
            OnCellHovered?.Invoke(cell);
            if (_hover)
            {
                _hover.gameObject.SetActive(true);
                _hover.position = CellCenter(cell);
                _hover.localScale = _cellSizeWorld;
            }

            if (PointerInput.LeftDown)
                OnCellClicked?.Invoke(cell);
        }
        else if (_hover) _hover.gameObject.SetActive(false);
    }


    public bool TryWorldToCell(Vector3 world, out Vector2Int cell)
    {
        cell = default;
        if (!_bounds.Contains(new Vector3(world.x, world.y, _bounds.center.z))) return false;

        float nx = Mathf.InverseLerp(_bounds.min.x, _bounds.max.x, world.x);
        float ny = Mathf.InverseLerp(_bounds.min.y, _bounds.max.y, world.y);

        int x = Mathf.Clamp(Mathf.FloorToInt(nx * data.cols), 0, data.cols - 1);
        int y = Mathf.Clamp(Mathf.FloorToInt(ny * data.rows), 0, data.rows - 1);

        cell = new Vector2Int(x, y);
        return true;
    }

    public Vector3 CellCenter(Vector2Int cell)
    {
        float cx = Mathf.Lerp(_bounds.min.x, _bounds.max.x, (cell.x + 0.5f) / data.cols);
        float cy = Mathf.Lerp(_bounds.min.y, _bounds.max.y, (cell.y + 0.5f) / data.rows);
        return new Vector3(cx, cy, transform.position.z);
    }

    public Vector2 CellSizeWorld() => _cellSizeWorld;
}
