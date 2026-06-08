using Definitions;
using TMPro;
using Shared.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Shared.UI
{
    public class CardView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _costText;
        [SerializeField] private Image _artwork;

        private ICard _model;

        public ICard Model => _model;

        public void Bind(ICard card)
        {
            _model = card;
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_model?.Def == null) return;
            CardDetailOverlay.Show(CardRulesText.FormatHandCardDetails(_model.Def), transform);
        }

        private void Refresh()
        {
            if (_model == null) return;
            AutoBindTextFields();

            if (_nameText != null)
            {
                ApplyReadableTextSettings(_nameText);
                _nameText.text = LocalizationService.CardName(_model.Def);
            }

            if (_costText != null)
            {
                ApplyReadableTextSettings(_costText);
                _costText.text = CardRulesText.FormatHandCardSummary(_model.Def);
            }
        }

        private static void ApplyReadableTextSettings(TextMeshProUGUI text)
        {
            if (text == null) return;
            // Russian localization frequently produces longer strings than the English originals.
            // Several prefabs were authored with WordWrapping off and Truncate overflow, which
            // caused single words to break across lines mid-glyph. Enforce a sensible default here.
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = true;
        }

        private void AutoBindTextFields()
        {
            _nameText ??= transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
            _costText ??= transform.Find("CardCost")?.GetComponent<TextMeshProUGUI>();

            var texts = GetComponentsInChildren<TextMeshProUGUI>(true);
            if (_nameText == null && texts.Length > 0) _nameText = texts[0];
            if (_costText == null && texts.Length > 1) _costText = texts[1];
        }
    }
}
