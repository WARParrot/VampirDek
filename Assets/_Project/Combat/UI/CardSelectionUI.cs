using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Definitions;

namespace Combat.UI
{
    public class CardSelectionUI : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private List<CardChoiceButton> choiceButtons;

        private UniTaskCompletionSource<CardDef> _tcs;

        public async UniTask<CardDef> ShowAsync(List<CardDef> choices)
        {
            panelRoot.SetActive(true);
            _tcs = new UniTaskCompletionSource<CardDef>();

            for (int i = 0; i < choiceButtons.Count && i < choices.Count; i++)
                choiceButtons[i].Setup(choices[i], OnCardChosen);

            return await _tcs.Task;
        }

        private void OnCardChosen(CardDef chosen)
        {
            panelRoot.SetActive(false);
            _tcs.TrySetResult(chosen);
        }
    }
}