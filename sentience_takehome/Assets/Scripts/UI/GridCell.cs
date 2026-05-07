using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GridCell : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public int Row;
    public int Col;

    private Button button;
    private Image image;
    private Color baseColor = Color.white;

    public System.Action<Coordinate> OnCellClicked;
    public System.Action<Coordinate> OnCellHovered;
    public System.Action OnCellHoverExited;

    private void Awake()
    {
        button = GetComponent<Button>();
        image = GetComponent<Image>();

        button.onClick.AddListener(HandleClick);
    }

    public void Init(int row, int col, System.Action<Coordinate> onClick,
        System.Action<Coordinate> onHover = null, System.Action onHoverExit = null)
    {
        Row = row;
        Col = col;
        OnCellClicked = onClick;
        OnCellHovered = onHover;
        OnCellHoverExited = onHoverExit;
    }

    private void HandleClick()
    {
        Debug.Log($"[grid-click] {gameObject.name} row={Row} col={Col} onClick={(OnCellClicked != null)}");
        OnCellClicked?.Invoke(new Coordinate(Row, Col));
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    /// <summary>Stop placement preview hover after battle starts (handlers survive disabled scripts).</summary>
    public void ClearHoverHandlers()
    {
        OnCellHovered = null;
        OnCellHoverExited = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        OnCellHovered?.Invoke(new Coordinate(Row, Col));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        OnCellHoverExited?.Invoke();
    }

    public void SetColor(Color color)
    {
        image.color = color;
    }

    public Color GetColor()
    {
        return image.color;
    }
}
