using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridManager : MonoBehaviour
{
    public GridCell cellPrefab;
    public Transform gridParent;

    [Header("Ship hulls (placement + your board in battle)")]
    [Tooltip("UI prefab with RectTransform + Image (raycast disabled at runtime).")]
    public GameObject shipHullPrefab;
    public Sprite carrierSprite;
    public Sprite battleshipSprite;
    public Sprite cruiserSprite;
    public Sprite submarineSprite;
    public Sprite destroyerSprite;

    private GridCell[,] cells = new GridCell[10, 10];

    private RectTransform _frontShipLayer;
    private readonly List<HullRecord> _hullRecords = new();

    private sealed class HullRecord
    {
        public GameObject GameObject;
        public Image Image;
        public ShipType ShipType;
        public HashSet<Coordinate> Coordinates;
    }

    private static readonly Color SunkHullColor = new Color(0.42f, 0.42f, 0.48f, 0.52f);
    private GameObject _placementPreviewInstance;

    private static readonly Vector3[] CornerScratch = new Vector3[4];

    public void SetVisible(bool visible)
    {
        if (gridParent != null)
        {
            gridParent.gameObject.SetActive(visible);
        }

        if (_frontShipLayer != null)
        {
            _frontShipLayer.gameObject.SetActive(visible);
        }

        if (!visible)
        {
            HidePlacementPreview();
        }
    }

    public void GenerateGrid(System.Action<Coordinate> onCellClick,
        System.Action<Coordinate> onCellHover = null,
        System.Action onCellHoverExit = null)
    {
        for (int row = 0; row < 10; row++)
        {
            for (int col = 0; col < 10; col++)
            {
                var cell = Instantiate(cellPrefab, gridParent);
                cell.Init(row, col, onCellClick, onCellHover, onCellHoverExit);
                cells[row, col] = cell;
            }
        }
    }

    public void SetCellColor(Coordinate coord, Color color)
    {
        cells[coord.Row, coord.Col].SetColor(color);
    }

    public Color GetCellColor(Coordinate coord)
    {
        return cells[coord.Row, coord.Col].GetColor();
    }

    public void ClearGrid(Color color)
    {
        for (int row = 0; row < BattleshipRules.BoardSize; row++)
        {
            for (int col = 0; col < BattleshipRules.BoardSize; col++)
            {
                SetCellColor(new Coordinate(row, col), color);
            }
        }
    }

    public void SetInteractable(bool interactable)
    {
        for (int row = 0; row < BattleshipRules.BoardSize; row++)
        {
            for (int col = 0; col < BattleshipRules.BoardSize; col++)
            {
                cells[row, col].SetInteractable(interactable);
            }
        }
    }

    public void ClearHoverHandlers()
    {
        for (int row = 0; row < BattleshipRules.BoardSize; row++)
        {
            for (int col = 0; col < BattleshipRules.BoardSize; col++)
            {
                cells[row, col].ClearHoverHandlers();
            }
        }
    }

    /// <summary>Clears hull sprites and placement ghost. Does not reset cell colors.</summary>
    public void ClearBattleDecorations()
    {
        HidePlacementPreview();
        foreach (var rec in _hullRecords)
        {
            if (rec.GameObject != null)
            {
                Destroy(rec.GameObject);
            }
        }

        _hullRecords.Clear();
    }

    public void RenderShipHullsFromBoard(Board board)
    {
        if (board == null || shipHullPrefab == null)
        {
            return;
        }

        foreach (var ship in board.Ships)
        {
            var sprite = SpriteForShipType(ship.Type);
            if (sprite == null)
            {
                continue;
            }

            if (!TryGetSpanInFrontLayer(ship.Coordinates, out var min, out var max))
            {
                continue;
            }

            var orientation = InferOrientation(ship);
            var go = Instantiate(shipHullPrefab, FrontShipLayer);
            var img = go.GetComponent<Image>() ?? go.GetComponentInChildren<Image>();
            var rt = go.transform as RectTransform;
            if (img == null || rt == null)
            {
                Destroy(go);
                continue;
            }

            img.raycastTarget = false;
            ConfigureHullVisual(rt, img, sprite, min, max, orientation, Color.white);

            var coordSet = new HashSet<Coordinate>();
            foreach (var c in ship.Coordinates)
            {
                coordSet.Add(c);
            }

            _hullRecords.Add(new HullRecord
            {
                GameObject = go,
                Image = img,
                ShipType = ship.Type,
                Coordinates = coordSet
            });
        }
    }

    /// <summary>Dims the hull overlay for a ship that was just sunk (matched by type and hit cell).</summary>
    public void DimPlayerHullForSunkShip(ShipType shipType, Coordinate hitCell)
    {
        foreach (var rec in _hullRecords)
        {
            if (rec.ShipType == shipType && rec.Coordinates.Contains(hitCell))
            {
                rec.Image.color = SunkHullColor;
                return;
            }
        }
    }

    public void UpdatePlacementPreview(ShipType shipType, Coordinate start, Orientation orientation, bool valid)
    {
        HidePlacementPreview();
        if (shipHullPrefab == null)
        {
            return;
        }

        var sprite = SpriteForShipType(shipType);
        if (sprite == null)
        {
            return;
        }

        var coords = BattleshipRules.GetShipCoordinates(start, shipType, orientation);
        if (!TryGetSpanInFrontLayer(coords, out var min, out var max))
        {
            return;
        }

        _placementPreviewInstance = Instantiate(shipHullPrefab, FrontShipLayer);
        var img = _placementPreviewInstance.GetComponent<Image>() ??
                  _placementPreviewInstance.GetComponentInChildren<Image>();
        var rt = _placementPreviewInstance.transform as RectTransform;
        if (img == null || rt == null)
        {
            Destroy(_placementPreviewInstance);
            _placementPreviewInstance = null;
            return;
        }

        img.raycastTarget = false;
        // Semitransparent so green/red cell highlights show through under the ghost.
        var tint = valid ? new Color(0.35f, 1f, 0.45f, 0.42f) : new Color(1f, 0.35f, 0.35f, 0.42f);
        ConfigureHullVisual(rt, img, sprite, min, max, orientation, tint);
    }

    public void HidePlacementPreview()
    {
        if (_placementPreviewInstance != null)
        {
            Destroy(_placementPreviewInstance);
            _placementPreviewInstance = null;
        }
    }

    private RectTransform FrontShipLayer
    {
        get
        {
            if (_frontShipLayer != null)
            {
                return _frontShipLayer;
            }

            var gp = gridParent as RectTransform;
            if (gp == null || gp.parent == null)
            {
                return gp;
            }

            var go = new GameObject("ShipAndShotOverlay", typeof(RectTransform), typeof(CanvasGroup));
            _frontShipLayer = go.GetComponent<RectTransform>();
            var overlayGroup = go.GetComponent<CanvasGroup>();
            overlayGroup.blocksRaycasts = false;
            overlayGroup.interactable = false;
            _frontShipLayer.SetParent(gp.parent, false);
            CopyRectTransform(gp, _frontShipLayer);
            _frontShipLayer.SetSiblingIndex(gp.GetSiblingIndex() + 1);
            return _frontShipLayer;
        }
    }

    private static void CopyRectTransform(RectTransform src, RectTransform dst)
    {
        dst.localScale = Vector3.one;
        dst.anchorMin = src.anchorMin;
        dst.anchorMax = src.anchorMax;
        dst.pivot = src.pivot;
        dst.anchoredPosition = src.anchoredPosition;
        dst.sizeDelta = src.sizeDelta;
    }

    private Sprite SpriteForShipType(ShipType type)
    {
        return type switch
        {
            ShipType.Carrier => carrierSprite,
            ShipType.Battleship => battleshipSprite,
            ShipType.Cruiser => cruiserSprite,
            ShipType.Submarine => submarineSprite,
            ShipType.Destroyer => destroyerSprite,
            _ => null
        };
    }

    private static Orientation InferOrientation(Ship ship)
    {
        if (ship.Coordinates.Count < 2)
        {
            return Orientation.Horizontal;
        }

        var a = ship.Coordinates[0];
        var b = ship.Coordinates[1];
        return a.Row == b.Row ? Orientation.Horizontal : Orientation.Vertical;
    }

    private bool TryGetSpanInFrontLayer(IReadOnlyList<Coordinate> coords, out Vector2 min, out Vector2 max)
    {
        min = max = Vector2.zero;
        var has = false;
        foreach (var c in coords)
        {
            if (!BattleshipRules.IsWithinBounds(c))
            {
                continue;
            }

            ExpandCellBounds(c, ref min, ref max, ref has);
        }

        return has;
    }

    private void ExpandCellBounds(Coordinate coord, ref Vector2 min, ref Vector2 max, ref bool hasAny)
    {
        var cellRt = cells[coord.Row, coord.Col].transform as RectTransform;
        cellRt.GetWorldCorners(CornerScratch);
        var layer = FrontShipLayer;
        for (var i = 0; i < 4; i++)
        {
            var p = (Vector2)layer.InverseTransformPoint(CornerScratch[i]);
            if (!hasAny)
            {
                min = max = p;
                hasAny = true;
            }
            else
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
        }
    }

    private void ConfigureHullVisual(RectTransform rt, Image img, Sprite sprite, Vector2 min, Vector2 max,
        Orientation orientation, Color color)
    {
        img.sprite = sprite;
        img.color = color;
        // Stretch to the exact N-cell span (carrier 5 … destroyer 2); aspect is from the grid, not the texture.
        img.preserveAspect = false;
        var center = (min + max) * 0.5f;
        var span = max - min;
        const float pad = 2f;
        var sx = Mathf.Max(1f, span.x - pad);
        var sy = Mathf.Max(1f, span.y - pad);
        // Vertical-authored art: long axis is RectTransform height (local Y). Horizontal ships swap so
        // local Y matches the multi-cell span, then we rotate +90° to align with the row.
        float rectW;
        float rectH;
        if (orientation == Orientation.Horizontal)
        {
            rectW = sy;
            rectH = sx;
        }
        else
        {
            rectW = sx;
            rectH = sy;
        }

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = center;
        rt.sizeDelta = new Vector2(rectW, rectH);
        // Sprites are authored vertical (tall); horizontal placements lay the hull on its side.
        var zRot = orientation == Orientation.Horizontal ? 90f : 0f;
        rt.localRotation = Quaternion.Euler(0f, 0f, zRot);
        rt.SetAsLastSibling();
    }
}
