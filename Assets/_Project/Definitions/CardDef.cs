using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Definitions
{
    public enum CardType { Vanguard, Building, Human, Town }

    public enum RowType { Vanguard, Building, Human, Town }

    [CreateAssetMenu(menuName = "Card Definition")]
    public class CardDef : ScriptableObject
    {
        public string CardName;
        [Tooltip("Stable localization key for the display name. Leave empty to use card.<CardName>.name with CardName as fallback.")]
        public string CardNameKey;
        [TextArea(2, 6)]
        [Tooltip("Flavor / rules description shown in the card-details overlay.")]
        public string Description;
        public AssetReferenceSprite Artwork;
        public CardType Type;
        public RowType RowType;
        public int MinSpeed;
        public int MaxSpeed;
        public int Health;
        public int Attack;
        public List<CardCost> Costs;
        public List<EffectActionDefinition> Effects;
        public List<EnchantmentData> InnateEnchantments;

        [Tooltip("Optional combat VFX profile id used by CombatVFX for card-specific attack/clash presentation. Examples: blood, shadow, ritual, town. Leave empty for default.")]
        public string CombatVfxProfileId;

        [Tooltip("Optional combat VFX accent color. Transparent/clear means CombatVFX chooses a profile/default color.")]
        public Color CombatVfxTint = Color.clear;
    }
}
