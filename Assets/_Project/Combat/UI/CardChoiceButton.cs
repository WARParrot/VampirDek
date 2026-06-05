using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Definitions;
using Shared.Localization;
using Shared.UI;
using System;
using System.Collections.Generic;

namespace Combat.UI
{
    public class CardChoiceButton : MonoBehaviour
    {
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Transform container;
        [SerializeField] private Button button;

        private CardDef _card;
        private Action<CardDef> _callback;
        private GameObject _cardInstance;

        public void Setup(CardDef card, Action<CardDef> callback)
        {
            _card = card;
            _callback = callback;

            if (_cardInstance) Destroy(_cardInstance);

            if (cardPrefab != null && container != null)
            {
                _cardInstance = Instantiate(cardPrefab, container);

                var nameText = _cardInstance.transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
                if (nameText) nameText.text = LocalizationService.CardName(card);

                var costText = _cardInstance.transform.Find("CardCost")?.GetComponent<TextMeshProUGUI>();
                if (costText)
                {
                    var costStr = card.Costs != null
                        ? string.Join(" ", card.Costs.ConvertAll(CardRulesText.FormatCostText))
                        : string.Empty;
                    costText.text = costStr;
                }

                var btn = _cardInstance.GetComponent<Button>();
                if (btn == null)
                    btn = _cardInstance.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OnCardClicked);
            }
            else
            {
                Debug.LogError("CardChoiceButton: не назначен cardPrefab или container!");
            }
        }

        private void OnCardClicked()
        {
            Debug.Log($"[CardChoice] Clicked on {_card?.CardName}, callback is {(_callback != null ? "set" : "NULL")}");
            _callback?.Invoke(_card);
        }

        private void OnDestroy()
        {
            if (_cardInstance)
            {
                var btn = _cardInstance.GetComponent<Button>();
                if (btn) btn.onClick.RemoveListener(OnCardClicked);
            }
        }
    }
}