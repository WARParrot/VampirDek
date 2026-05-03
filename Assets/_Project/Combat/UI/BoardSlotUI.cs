using UnityEngine;
using UnityEngine.UI;

public class BoardSlotUI : MonoBehaviour
{
    public Image HighlightImage;
    public bool IsValidDropTarget { get; set; }

    public void SetHighlight(bool on)
    {
        if (HighlightImage != null)
            HighlightImage.color = on ? new Color(0, 1, 0, 0.3f) : new Color(1, 1, 1, 0);
    }
}