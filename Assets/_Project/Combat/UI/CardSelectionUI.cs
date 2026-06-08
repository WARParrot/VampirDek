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
        private UniTaskCompletionSource<List<int>> _multiTcs;
        private readonly List<int> _pickedIndices = new();
        private readonly List<CardDef> _pickedDefs = new();
        private readonly HashSet<int> _disabledIndices = new();
        private int _picksAllowed = 1;
        private List<CardDef> _candidateRefs;
        private string _mandatoryCardName;

        /// <summary>
        /// Single-pick reward flow. Preserved for backward compatibility with the loot system.
        /// </summary>
        public async UniTask<CardDef> ShowAsync(List<string> choices)
        {
            panelRoot.SetActive(true);
            _tcs = new UniTaskCompletionSource<CardDef>();

            for (int i = 0; i < choiceButtons.Count && i < choices.Count; i++)
                choiceButtons[i].Setup(CardDatabase.GetCard(choices[i]), OnCardChosen);

            return await _tcs.Task;
        }

        /// <summary>
        /// Multi-pick draft flow. Returns once <paramref name="picksAllowed"/> cards have been chosen.
        /// </summary>
        public async UniTask<List<int>> ShowDraftAsync(List<CardDef> candidates, int picksAllowed, string mandatoryCardName = null)
        {
            panelRoot.SetActive(true);
            _multiTcs = new UniTaskCompletionSource<List<int>>();
            _pickedIndices.Clear();
            _pickedDefs.Clear();
            _disabledIndices.Clear();
            _picksAllowed = Mathf.Clamp(picksAllowed, 1, candidates.Count);
            _candidateRefs = candidates;
            // Only honor the mandatory hint if the candidate list actually contains that card.
            _mandatoryCardName = !string.IsNullOrEmpty(mandatoryCardName) &&
                                 candidates.Exists(c => c != null && c.CardName == mandatoryCardName)
                                 ? mandatoryCardName
                                 : null;

            for (int i = 0; i < choiceButtons.Count; i++)
            {
                if (i < candidates.Count && candidates[i] != null)
                {
                    int capturedIndex = i;
                    choiceButtons[i].gameObject.SetActive(true);
                    // Reset the visual "picked" state from the previous draft so the new candidates
                    // are fully interactive — otherwise buttons stay dimmed and unclickable.
                    var canvasGroup = choiceButtons[i].GetComponent<CanvasGroup>();
                    if (canvasGroup != null)
                    {
                        canvasGroup.alpha = 1f;
                        canvasGroup.interactable = true;
                        canvasGroup.blocksRaycasts = true;
                    }
                    choiceButtons[i].Setup(candidates[i], def => OnDraftCardChosen(capturedIndex, def));
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }

            return await _multiTcs.Task;
        }

        private void OnCardChosen(CardDef chosen)
        {
            panelRoot.SetActive(false);
            _tcs.TrySetResult(chosen);
        }

        private void OnDraftCardChosen(int index, CardDef chosen)
        {
            if (_disabledIndices.Contains(index)) return;
            if (chosen == null) return;

            // Hard-gate the mandatory card: if the player still needs to pick it and this is
            // their last available slot, block any other choice.
            if (!string.IsNullOrEmpty(_mandatoryCardName) && chosen.CardName != _mandatoryCardName)
            {
                bool mandatoryAlreadyPicked = _pickedDefs.Exists(c => c != null && c.CardName == _mandatoryCardName);
                int picksRemainingAfterThis = _picksAllowed - (_pickedDefs.Count + 1);
                if (!mandatoryAlreadyPicked && picksRemainingAfterThis <= 0)
                {
                    Debug.Log($"[Draft] Pick blocked: '{_mandatoryCardName}' is mandatory and must be selected.");
                    return;
                }
            }

            _disabledIndices.Add(index);
            _pickedIndices.Add(index);
            _pickedDefs.Add(chosen);

            // Visually disable the button so the player can see what they already locked in.
            if (index >= 0 && index < choiceButtons.Count && choiceButtons[index] != null)
            {
                var canvasGroup = choiceButtons[index].GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = choiceButtons[index].gameObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0.35f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (_pickedIndices.Count >= _picksAllowed)
            {
                panelRoot.SetActive(false);
                var result = new List<int>(_pickedIndices);
                _pickedIndices.Clear();
                _pickedDefs.Clear();
                _disabledIndices.Clear();
                _multiTcs.TrySetResult(result);
            }
        }
    }
}
