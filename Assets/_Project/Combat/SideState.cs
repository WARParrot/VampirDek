using System;
using System.Collections.Generic;
using System.Linq;
using Definitions;
using UnityEngine;
using Core;

namespace Combat
{
    public class SideState : IPlayerSide
    {
        public const int MaxHandSize = 7;
        public Board Board { get; private set; }
        public Deck Deck { get; set; }
        public int HumanResources { get; set; }
        public List<Card> Hand { get; } = new();
        public List<Card> Graveyard { get; } = new();
        public BoardCard Town => Board.TownSlot?.Occupant;


        public SideState(BoardLayoutData layout)
        {
            Board = new Board(layout);
            HumanResources = 0;
        }

        public void PayHumanResources(int amount) => HumanResources -= amount;

        public void DrawCards(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (Hand.Count >= MaxHandSize)
                {
                    Debug.Log("Рука переполнена – карта не взята.");
                    break;
                }

                var card = Deck.Draw();
                if (card != null)
                    AddCardToHand(card);
                else
                    break;
            }
        }

        public void DiscardRandomCards(int count)
        {
            var hand = new List<Card>(Hand);
            var rng = new System.Random();
            for (int i = 0; i < count && hand.Count > 0; i++)
            {
                int idx = rng.Next(hand.Count);
                var card = hand[idx];
                hand.RemoveAt(idx);
                Hand.Remove(card);
                Graveyard.Add(card);
            }
        }

        public void InitializeTown()
        {
            var townCard = Deck.Cards.FirstOrDefault(c => c.Def.Type == CardType.Town)
                           ?? throw new InvalidOperationException("Deck must contain exactly one Town card.");
            Deck.Remove(townCard);
            Board.PlaceTownCard(townCard.Def);
        }
        public void AddCardToHand(Card card)
        {
            Hand.Add(card);
            GlobalServices.EventBus.Publish(new CardDrawnEvent(this, card));
        }
        IBoard IPlayerSide.Board => Board;
        IGameEntity IPlayerSide.Town => Board.TownSlot?.Occupant;
        IReadOnlyList<ICard> IPlayerSide.Hand => Hand.AsReadOnly();
        IReadOnlyList<ICard> IPlayerSide.Deck => Deck?.Cards;
        IReadOnlyList<ICard> IPlayerSide.Graveyard => Graveyard.AsReadOnly();
    }
}