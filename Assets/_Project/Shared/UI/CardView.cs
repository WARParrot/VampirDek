using Definitions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Shared.UI
{
    public class CardView : MonoBehaviour
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

        private void Refresh()
        {
            if (_model == null) return;

            if (_nameText != null)
                _nameText.text = _model.Def.CardName;

            if (_costText != null)
            {
                var costStrings = _model.Def.Costs.ConvertAll(c => c.GetCostText());
                _costText.text = string.Join(" ", costStrings);
            }
        }
    }
}