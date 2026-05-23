using Cysharp.Threading.Tasks;
using Definitions;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Combat
{
    public class ModContentImporter
    {
        public async UniTask ImportModAsync(string modDir)
        {
            var cardHandle = Addressables.LoadAssetsAsync<CardDef>("Cards", null);
            await cardHandle.Task;
            foreach (var card in cardHandle.Result)
            {
                if (card != null)
                    CardDatabase.RegisterCard(card);
            }

            var enchantHandle = Addressables.LoadAssetsAsync<EnchantmentData>("Enchantments", null);
            await enchantHandle.Task;
            foreach (var enchant in enchantHandle.Result)
            {
                if (enchant != null)
                    EnchantmentDatabase.RegisterEnchantment(enchant);
            }

            Debug.Log($"[ModContentImporter] Imported cards & enchantments from {modDir}");
        }
    }
}