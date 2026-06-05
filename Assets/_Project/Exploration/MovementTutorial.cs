using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Shared.Localization;

namespace Exploration
{
    /// <summary>
    /// Система обучения движению с визуальными подсказками
    /// </summary>
    public class MovementTutorial : MonoBehaviour
    {
        [Header("Tutorial UI")]
        [SerializeField] private GameObject _tutorialPanel;
        [SerializeField] private Text _instructionText;
        [SerializeField] private Image _keyHintImage;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Tutorial Steps")]
        [SerializeField] private TutorialMovementStep[] _steps;

        [Header("Settings")]
        [SerializeField] private float _fadeSpeed = 2f;
        [SerializeField] private bool _startOnAwake = true;

        private int _currentStepIndex = 0;
        private bool _tutorialActive = false;
        private bool _stepCompleted = false;
        private bool _actionPromptShown = false;
        private float _targetAlpha = 0f;
        private float _stepStartedAt = 0f;

        private ExplorationController _player;

        public bool BlocksDuelStart => _tutorialActive;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_tutorialPanel == null)
                _tutorialPanel = gameObject;

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }

            ConfigureTutorialPanel();
        }

        private void Start()
        {
            _player = FindObjectOfType<ExplorationController>();
            EnsureUsefulDefaultSteps();

            if (_startOnAwake && _steps != null && _steps.Length > 0)
            {
                StartTutorial();
            }
            else if (_steps == null || _steps.Length == 0)
            {
                Debug.LogWarning("[MovementTutorial] No tutorial steps configured. Add steps in Inspector.");
            }
        }

        private void Update()
        {
            if (!_tutorialActive) return;

            // Плавное появление/исчезновение
            if (_canvasGroup.alpha != _targetAlpha)
            {
                _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
            }

            // Проверка выполнения текущего шага
            if (!_stepCompleted && _currentStepIndex < _steps.Length)
            {
                CheckStepCompletion();
            }
        }

        private void ConfigureTutorialPanel()
        {
            if (_tutorialPanel != null)
            {
                var rect = _tutorialPanel.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.sizeDelta = new Vector2(Mathf.Max(rect.sizeDelta.x, 620f), Mathf.Max(rect.sizeDelta.y, 220f));
                }
            }

            if (_instructionText != null)
            {
                _instructionText.alignment = TextAnchor.MiddleCenter;
                _instructionText.horizontalOverflow = HorizontalWrapMode.Wrap;
                _instructionText.verticalOverflow = VerticalWrapMode.Truncate;
                _instructionText.resizeTextForBestFit = true;
                _instructionText.resizeTextMinSize = 14;
                _instructionText.resizeTextMaxSize = Mathf.Max(_instructionText.fontSize, 22);
            }
        }

        private void EnsureUsefulDefaultSteps()
        {
            if (!NeedsDefaultSteps()) return;

            _steps = CreateDefaultExplorationSteps();
            Debug.Log("[MovementTutorial] Using built-in exploration onboarding steps because the serialized steps are missing or too sparse.");
        }

        private bool NeedsDefaultSteps()
        {
            if (_steps == null || _steps.Length < 5) return true;

            foreach (var step in _steps)
            {
                if (step != null && !string.IsNullOrWhiteSpace(step.InstructionText) && step.InstructionText.Contains("Exploration"))
                {
                    return false;
                }
            }

            return true;
        }

        private static TutorialMovementStep[] CreateDefaultExplorationSteps()
        {
            return new[]
            {
                new TutorialMovementStep
                {
                    InstructionKey = "tutorial.exploration_intro",
                    InstructionText = "Exploration is where the run breathes. Between duels, you decide what is worth risking: a fight, a reward, or a retreat.",
                    CompletionType = MovementCompletionType.TimeElapsed,
                    DelayBeforeNext = 4.5f
                },
                new TutorialMovementStep
                {
                    InstructionKey = "tutorial.exploration_move_intro",
                    InstructionText = "First, get your bearings. Doors, encounters, secrets, and escape routes only matter once you can reach them.",
                    ActionPromptKey = "tutorial.exploration_move_prompt",
                    ActionPrompt = "Move with WASD or the arrow keys.",
                    CompletionType = MovementCompletionType.AnyMovement,
                    MinimumReadSeconds = 2.25f,
                    DelayBeforeNext = 0.65f
                },
                new TutorialMovementStep
                {
                    InstructionKey = "tutorial.exploration_look_intro",
                    InstructionText = "Your gaze is how you ask the world questions. Look around to find what can be inspected, used, avoided, or fought.",
                    ActionPromptKey = "tutorial.exploration_look_prompt",
                    ActionPrompt = "Move the mouse to look around.",
                    CompletionType = MovementCompletionType.MouseLook,
                    MinimumReadSeconds = 2.25f,
                    DelayBeforeNext = 0.65f
                },
                new TutorialMovementStep
                {
                    InstructionKey = "tutorial.exploration_interact_intro",
                    InstructionText = "When the world answers, interact. This is how you inspect objects and open paths before you commit to a duel.",
                    ActionPromptKey = "tutorial.exploration_interact_prompt",
                    ActionPrompt = "Face an interactable prompt, then press E.",
                    CompletionType = MovementCompletionType.Interact,
                    MinimumReadSeconds = 2.5f,
                    DelayBeforeNext = 0.75f
                },
                new TutorialMovementStep
                {
                    InstructionKey = "tutorial.exploration_deck_intro",
                    InstructionText = "Before you accept danger, check what you are carrying. Your deck is not a menu footnote; it is your plan for surviving the next duel.",
                    ActionPromptKey = "tutorial.exploration_deck_prompt",
                    ActionPrompt = "Press Esc to open the menu and review your current deck.",
                    CompletionType = MovementCompletionType.EscapeMenu,
                    MinimumReadSeconds = 3f,
                    DelayBeforeNext = 0.75f
                },
                new TutorialMovementStep
                {
                    InstructionKey = "tutorial.exploration_outro",
                    InstructionText = "That is Exploration: read the room, check the deck, then choose the fight. Curiosity builds the run; haste can end it.",
                    CompletionType = MovementCompletionType.TimeElapsed,
                    DelayBeforeNext = 4.5f
                }
            };
        }

        /// <summary>
        /// Запускает обучение движению
        /// </summary>
        public void StartTutorial()
        {
            if (_steps == null || _steps.Length == 0)
            {
                Debug.LogWarning("[MovementTutorial] Cannot start tutorial - no steps configured!");
                return;
            }

            _tutorialActive = true;
            _currentStepIndex = 0;

            if (_tutorialPanel != null)
                _tutorialPanel.SetActive(true);

            ShowCurrentStep();
            Debug.Log("[MovementTutorial] Tutorial started!");
        }

        private void ShowCurrentStep()
        {
            if (_currentStepIndex >= _steps.Length)
            {
                EndTutorial();
                return;
            }

            var step = _steps[_currentStepIndex];
            _stepStartedAt = Time.time;

            Debug.Log($"[MovementTutorial] Showing step {_currentStepIndex}: '{step.InstructionText}'");

            if (_instructionText != null)
            {
                _actionPromptShown = step.CompletionType == MovementCompletionType.TimeElapsed || step.MinimumReadSeconds <= 0f;
                ApplyStepText(step, _actionPromptShown);
                Debug.Log($"[MovementTutorial] Text set to: '{_instructionText.text}'");
            }
            else
            {
                Debug.LogError("[MovementTutorial] _instructionText is NULL!");
            }

            if (_keyHintImage != null)
            {
                if (step.KeyHintSprite != null)
                {
                    _keyHintImage.sprite = step.KeyHintSprite;
                    _keyHintImage.enabled = true;
                }
                else
                {
                    _keyHintImage.enabled = false;
                }
            }

            _targetAlpha = 1f;
            _stepCompleted = false;

            Debug.Log($"[MovementTutorial] Target alpha set to 1, current alpha: {_canvasGroup?.alpha}");
        }

        private void ApplyStepText(TutorialMovementStep step, bool showActionPrompt)
        {
            if (_instructionText == null || step == null) return;

            var instruction = ResolveStepText(step.InstructionKey, step.InstructionText);
            var actionPrompt = ResolveStepText(step.ActionPromptKey, step.ActionPrompt);

            if (showActionPrompt && !string.IsNullOrWhiteSpace(actionPrompt))
            {
                _instructionText.text = $"{instruction}\n\n<size=18><color=#FFD36A>{actionPrompt}</color></size>";
            }
            else
            {
                _instructionText.text = instruction;
            }

            _instructionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _instructionText.verticalOverflow = VerticalWrapMode.Truncate;
            _instructionText.resizeTextForBestFit = true;
            _instructionText.resizeTextMinSize = 14;
            _instructionText.resizeTextMaxSize = Mathf.Max(_instructionText.fontSize, 22);
            _instructionText.supportRichText = true;
        }

        private static string ResolveStepText(string key, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(key)) return LocalizationService.T(key, fallback);
            var inferredKey = TutorialKeyFromEnglish(fallback);
            return LocalizationService.T(inferredKey, fallback);
        }

        private static string TutorialKeyFromEnglish(string text)
        {
            return text switch
            {
                "Exploration is where the run breathes. Between duels, you decide what is worth risking: a fight, a reward, or a retreat." => "tutorial.exploration_intro",
                "First, get your bearings. Doors, encounters, secrets, and escape routes only matter once you can reach them." => "tutorial.exploration_move_intro",
                "Move with WASD or the arrow keys." => "tutorial.exploration_move_prompt",
                "Your gaze is how you ask the world questions. Look around to find what can be inspected, used, avoided, or fought." => "tutorial.exploration_look_intro",
                "Move the mouse to look around." => "tutorial.exploration_look_prompt",
                "When the world answers, interact. This is how you inspect objects, open paths, and commit to nearby encounter points." => "tutorial.exploration_interact_intro",
                "When the world answers, interact. This is how you inspect objects and open paths before you commit to a duel." => "tutorial.exploration_interact_intro",
                "Face an interactable prompt or encounter, then press E." => "tutorial.exploration_interact_prompt",
                "Face an interactable prompt, then press E." => "tutorial.exploration_interact_prompt",
                "Before you accept danger, check what you are carrying. Your deck is not a menu footnote; it is your plan for surviving the next duel." => "tutorial.exploration_deck_intro",
                "Press Esc to open the menu and review your current deck." => "tutorial.exploration_deck_prompt",
                "That is Exploration: read the room, check the deck, then choose the fight. Curiosity builds the run; haste can end it." => "tutorial.exploration_outro",
                _ => string.Empty
            };
        }

        private void CheckStepCompletion()
        {
            var step = _steps[_currentStepIndex];
            var secondsOnStep = Time.time - _stepStartedAt;

            if (!_actionPromptShown && secondsOnStep >= step.MinimumReadSeconds)
            {
                _actionPromptShown = true;
                ApplyStepText(step, true);
            }

            if (step.CompletionType != MovementCompletionType.TimeElapsed && secondsOnStep < step.MinimumReadSeconds)
            {
                return;
            }

            bool completed = step.CompletionType switch
            {
                MovementCompletionType.TimeElapsed => Time.time - _stepStartedAt >= step.DelayBeforeNext,
                MovementCompletionType.MoveForward => Keyboard.current != null && Keyboard.current.wKey.isPressed,
                MovementCompletionType.MoveBackward => Keyboard.current != null && Keyboard.current.sKey.isPressed,
                MovementCompletionType.MoveLeft => Keyboard.current != null && Keyboard.current.aKey.isPressed,
                MovementCompletionType.MoveRight => Keyboard.current != null && Keyboard.current.dKey.isPressed,
                MovementCompletionType.MouseLook => Mouse.current != null && (Mouse.current.delta.ReadValue().magnitude > 0.1f),
                MovementCompletionType.Interact => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame,
                MovementCompletionType.EscapeMenu => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame,
                MovementCompletionType.AnyMovement => Keyboard.current != null && (
                    Keyboard.current.wKey.isPressed ||
                    Keyboard.current.sKey.isPressed ||
                    Keyboard.current.aKey.isPressed ||
                    Keyboard.current.dKey.isPressed),
                _ => false
            };

            if (completed)
            {
                _stepCompleted = true;
                var delay = step.CompletionType == MovementCompletionType.TimeElapsed ? 0f : step.DelayBeforeNext;
                Invoke(nameof(NextStep), delay);
            }
        }

        private void NextStep()
        {
            _currentStepIndex++;
            ShowCurrentStep();
        }

        private void EndTutorial()
        {
            _tutorialActive = false;
            _targetAlpha = 0f;
            Invoke(nameof(HideTutorialPanel), 1f / _fadeSpeed);
        }

        private void HideTutorialPanel()
        {
            _tutorialPanel?.SetActive(false);
        }

        /// <summary>
        /// Принудительно завершает обучение
        /// </summary>
        public void SkipTutorial()
        {
            EndTutorial();
        }
    }

    /// <summary>
    /// Один шаг обучения движению
    /// </summary>
    [System.Serializable]
    public class TutorialMovementStep
    {
        public string InstructionKey;
        [TextArea(2, 5)]
        public string InstructionText;
        public string ActionPromptKey;
        [TextArea(1, 2)]
        public string ActionPrompt;
        public Sprite KeyHintSprite;
        public MovementCompletionType CompletionType;
        [Min(0f)] public float MinimumReadSeconds = 0f;
        public float DelayBeforeNext = 1f;
    }

    /// <summary>
    /// Тип условия завершения шага обучения
    /// </summary>
    public enum MovementCompletionType
    {
        // Keep the original numeric values stable for scene-serialized tutorial steps.
        MoveForward = 0,
        MoveBackward = 1,
        MoveLeft = 2,
        MoveRight = 3,
        MouseLook = 4,
        AnyMovement = 5,
        TimeElapsed = 6,
        Interact = 7,
        EscapeMenu = 8
    }
}
