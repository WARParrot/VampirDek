using System.Linq;
using UnityEditor;
using Definitions;
using UnityEngine;

namespace Core.Editor
{
    public static class CardValidator
    {
        [MenuItem("Tools/Validate All Cards")]
        public static void ValidateAllCards()
        {
            var cardGuids = AssetDatabase.FindAssets("t:CardDef");
            int errors = 0;
            foreach (var guid in cardGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardDef>(path);
                if (card == null) continue;

                if (string.IsNullOrWhiteSpace(card.CardName))
                {
                    Debug.LogError($"Card at {path}: Name is empty.", card);
                    errors++;
                }
                if (card.Artwork == null)
                    Debug.LogWarning($"Card {card.CardName}: Missing artwork.", card);
                /* if ((card.Type == CardType.Vanguard ||
                     card.Type == CardType.Building ||
                     card.Type == CardType.Human) && (card.AllowedSlotTypes == null || card.AllowedSlotTypes.Count == 0))
                    Debug.LogWarning($"Creature card {card.CardName}: No allowed slot types.", card); */
            }

            var encounterGuids = AssetDatabase.FindAssets("t:CombatEncounter");
            foreach (var guid in encounterGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var enc = AssetDatabase.LoadAssetAtPath<CombatEncounter>(path);
                if (enc.OpponentDeck.Count(c => c.Type == CardType.Town) != 1)
                {
                    Debug.LogError($"Encounter {enc.EncounterId}: Opponent deck must have exactly one Town.", enc);
                    errors++;
                }
            }

            Debug.Log($"Validation finished. Errors: {errors}");
        }
    }
}
