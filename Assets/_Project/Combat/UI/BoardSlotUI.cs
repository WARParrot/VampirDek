using Combat;
using Combat.UI;
using Definitions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class BoardSlotUI : MonoBehaviour, IPointerClickHandler
{
    public Image HighlightImage;
    public bool IsValidDropTarget { get; set; }

    public Board Board;
    public Definitions.RowType RowType;
    public int Index;

    public BoardCard Occupant => Board?.GetSlot(RowType, Index)?.Occupant;

    public void SetHighlight(bool on)
    {
        if (HighlightImage != null)
        {
            HighlightImage.enabled = true; // ← добавить
            HighlightImage.color = on ? new Color(0, 1, 0, 0.3f) : new Color(1, 1, 1, 0);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[Slot] Clicked: {RowType}[{Index}]");
        PlanningPhaseController.Instance?.HandleSlotClick(this);
    }
}