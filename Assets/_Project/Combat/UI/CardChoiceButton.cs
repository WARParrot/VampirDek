using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Definitions;
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
                if (nameText) nameText.text = card.CardName;

                var costText = _cardInstance.transform.Find("CardCost")?.GetComponent<TextMeshProUGUI>();
                if (costText)
                {
                    string costStr = "";
                    if (card.Costs != null)
                    {
                        var parts = new List<string>();
                        foreach (var cost in card.Costs)
                        {
                            if (cost is HumanResourceCost hr)
                                parts.Add($"{hr.Amount} HR");
                            else if (cost is BloodCost blood)
                                parts.Add($"{blood.Amount} blood");
                            else
                                parts.Add(cost.GetType().Name);
                        }
                        costStr = string.Join(" ", parts);
                    }
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