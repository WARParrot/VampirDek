using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using Combat;

namespace Combat.UI
{
    /// <summary>
    /// Улучшенное отображение полной колоды игрока
    /// Показывает все карты в колоде с группировкой и статистикой
    /// </summary>
    public class DeckViewerUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject _deckPanel;
        [SerializeField] private Transform _cardListContainer;
        [SerializeField] private GameObject _cardEntryPrefab;
        [SerializeField] private TextMeshProUGUI _deckStatsText;
        [SerializeField] private Button _closeButton;

        [Header("Filters")]
        [SerializeField] private Toggle _showVanguardToggle;
        [SerializeField] private Toggle _showBuildingToggle;
        [SerializeField] private Toggle _showHumanToggle;

        private DuelManager _duelManager;
        private List<GameObject> _cardEntries = new List<GameObject>();

        private void Awake()
        {
            _closeButton?.onClick.AddListener(Hide);
            _showVanguardToggle?.onValueChanged.AddListener(_ => RefreshDisplay());
            _showBuildingToggle?.onValueChanged.AddListener(_ => RefreshDisplay());
            _showHumanToggle?.onValueChanged.AddListener(_ => RefreshDisplay());

            _deckPanel?.SetActive(false);
        }

        /// <summary>
        /// Показывает окно просмотра колоды
        /// </summary>
        public void Show()
        {
            _duelManager = FindObjectOfType<DuelManager>();
            if (_duelManager?.CurrentDuelState == null)
            {
                Debug.LogWarning("[DeckViewerUI] No active duel state");
                return;
            }

            _deckPanel?.SetActive(true);
            RefreshDisplay();
        }

        /// <summary>
        /// Скрывает окно просмотра колоды
        /// </summary>
        public void Hide()
        {
            _deckPanel?.SetActive(false);
        }

        private void RefreshDisplay()
        {
            ClearCardEntries();

            if (_duelManager?.CurrentDuelState == null) return;

            var playerSide = _duelManager.CurrentDuelState.PlayerSide;
            var deck = playerSide.Deck;
            var hand = playerSide.Hand;
            var discardPile = playerSide.Graveyard;

            // Группируем карты по типу
            var cardGroups = new Dictionary<string, CardGroup>();

            // Карты в колоде
            foreach (var card in deck.Cards)
            {
                if (!ShouldShowCard(card.Def.RowType)) continue;
                AddCardToGroup(cardGroups, card.Def, CardLocation.Deck);
            }

            // Карты в руке
            foreach (var card in hand)
            {
                if (!ShouldShowCard(card.Def.RowType)) continue;
                AddCardToGroup(cardGroups, card.Def, CardLocation.Hand);
            }

            // Карты в сбросе
            foreach (var card in discardPile)
            {
                if (!ShouldShowCard(card.Def.RowType)) continue;
                AddCardToGroup(cardGroups, card.Def, CardLocation.Discard);
            }

            // Отображаем группы карт
            foreach (var group in cardGroups.Values)
            {
                CreateCardEntry(group);
            }

            // Обновляем статистику
            UpdateDeckStats(deck.Cards.Count, hand.Count, discardPile.Count);
        }

        private bool ShouldShowCard(Definitions.RowType rowType)
        {
            if (rowType == Definitions.RowType.Vanguard)
                return _showVanguardToggle == null || _showVanguardToggle.isOn;
            if (rowType == Definitions.RowType.Building)
                return _showBuildingToggle == null || _showBuildingToggle.isOn;
            if (rowType == Definitions.RowType.Human)
                return _showHumanToggle == null || _showHumanToggle.isOn;
            return true;
        }

        private void AddCardToGroup(Dictionary<string, CardGroup> groups, Definitions.CardDef cardDef, CardLocation location)
        {
            string key = cardDef.CardName;

            if (!groups.ContainsKey(key))
            {
                groups[key] = new CardGroup
                {
                    CardDef = cardDef,
                    InDeck = 0,
                    InHand = 0,
                    InDiscard = 0
                };
            }

            var group = groups[key];
            switch (location)
            {
                case CardLocation.Deck: group.InDeck++; break;
                case CardLocation.Hand: group.InHand++; break;
                case CardLocation.Discard: group.InDiscard++; break;
            }
        }

        private void CreateCardEntry(CardGroup group)
        {
            if (_cardEntryPrefab == null || _cardListContainer == null) return;

            var entry = Instantiate(_cardEntryPrefab, _cardListContainer);
            _cardEntries.Add(entry);

            // Заполняем информацию о карте
            var nameText = entry.transform.Find("CardName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
                nameText.text = group.CardDef.CardName;

            var statsText = entry.transform.Find("Stats")?.GetComponent<TextMeshProUGUI>();
            if (statsText != null)
                statsText.text = $"{group.CardDef.Attack}/{group.CardDef.Health}";

            var costText = entry.transform.Find("Cost")?.GetComponent<TextMeshProUGUI>();
            if (costText != null)
            {
                var costs = group.CardDef.Costs.ConvertAll(c => c.GetCostText());
                costText.text = string.Join(" ", costs);
            }

            var locationText = entry.transform.Find("Location")?.GetComponent<TextMeshProUGUI>();
            if (locationText != null)
            {
                var parts = new List<string>();
                if (group.InDeck > 0) parts.Add($"Колода: {group.InDeck}");
                if (group.InHand > 0) parts.Add($"Рука: {group.InHand}");
                if (group.InDiscard > 0) parts.Add($"Сброс: {group.InDiscard}");
                locationText.text = string.Join(" | ", parts);
            }

            var rowTypeText = entry.transform.Find("RowType")?.GetComponent<TextMeshProUGUI>();
            if (rowTypeText != null)
                rowTypeText.text = GetRowTypeDisplayName(group.CardDef.RowType);
        }

        private void UpdateDeckStats(int deckCount, int handCount, int discardCount)
        {
            if (_deckStatsText == null) return;

            int total = deckCount + handCount + discardCount;
            _deckStatsText.text = $"Всего карт: {total} | В колоде: {deckCount} | В руке: {handCount} | В сбросе: {discardCount}";
        }

        private void ClearCardEntries()
        {
            foreach (var entry in _cardEntries)
            {
                if (entry != null)
                    Destroy(entry);
            }
            _cardEntries.Clear();
        }

        private string GetRowTypeDisplayName(Definitions.RowType rowType)
        {
            return rowType switch
            {
                Definitions.RowType.Vanguard => "Авангард",
                Definitions.RowType.Building => "Здание",
                Definitions.RowType.Human => "Люди",
                Definitions.RowType.Town => "Город",
                _ => "Неизвестно"
            };
        }

        private class CardGroup
        {
            public Definitions.CardDef CardDef;
            public int InDeck;
            public int InHand;
            public int InDiscard;
        }

        private enum CardLocation
        {
            Deck,
            Hand,
            Discard
        }
    }
}
