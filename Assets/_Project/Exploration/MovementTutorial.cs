using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

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
        private float _targetAlpha = 0f;

        private ExplorationController _player;

        private void Awake()
        {
            if (_canvasGroup == null)
                _canvasGroup = GetComponent<CanvasGroup>();

            if (_tutorialPanel == null)
                _tutorialPanel = gameObject;

            _canvasGroup.alpha = 0f;
        }

        private void Start()
        {
            _player = FindObjectOfType<ExplorationController>();

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

            Debug.Log($"[MovementTutorial] Showing step {_currentStepIndex}: '{step.InstructionText}'");

            if (_instructionText != null)
            {
                _instructionText.text = step.InstructionText;
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

        private void CheckStepCompletion()
        {
            var step = _steps[_currentStepIndex];

            bool completed = step.CompletionType switch
            {
                MovementCompletionType.MoveForward => Keyboard.current != null && Keyboard.current.wKey.isPressed,
                MovementCompletionType.MoveBackward => Keyboard.current != null && Keyboard.current.sKey.isPressed,
                MovementCompletionType.MoveLeft => Keyboard.current != null && Keyboard.current.aKey.isPressed,
                MovementCompletionType.MoveRight => Keyboard.current != null && Keyboard.current.dKey.isPressed,
                MovementCompletionType.MouseLook => Mouse.current != null && (Mouse.current.delta.ReadValue().magnitude > 0.1f),
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
                Invoke(nameof(NextStep), step.DelayBeforeNext);
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
        [TextArea(2, 4)]
        public string InstructionText;
        public Sprite KeyHintSprite;
        public MovementCompletionType CompletionType;
        public float DelayBeforeNext = 1f;
    }

    /// <summary>
    /// Тип условия завершения шага обучения
    /// </summary>
    public enum MovementCompletionType
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        MouseLook,
        AnyMovement
    }
}
