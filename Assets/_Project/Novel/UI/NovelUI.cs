using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using Cysharp.Threading.Tasks;
public class NovelUI : MonoBehaviour
{
    public static NovelUI Instance { get; private set; }

    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private TextMeshProUGUI _speakerName;
    [SerializeField] private Image _portraitImage;
    [SerializeField] private TypewriterEffect _dialogueText;
    [SerializeField] private Transform _choicesContainer;
    [SerializeField] private GameObject _choiceButtonPrefab;
    [SerializeField] private Button _advanceButton;

    private NovelManager _manager;

    void Awake()
    {
        Instance = this;

        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }

    public void Show()
    {
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
    }

    public void Hide()
    {
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }
    public void SetManager(NovelManager manager)
    {
        _manager = manager;
        _advanceButton.onClick.RemoveAllListeners();
        _advanceButton.onClick.AddListener(() => _manager.AdvanceDialog());
    }

    public void UpdateDisplay(DialogueNode node)
    {
        _speakerName.text = node.SpeakerName;
        _dialogueText.ShowText(node.Text);
        LoadSprite(node.SpeakerPortraitName, _portraitImage);
        LoadSprite(node.BackgroundName, _backgroundImage);
    }

    public void SetChoices(ChoiceOption[] choices)
    {
        foreach (Transform child in _choicesContainer)
            Destroy(child.gameObject);

        _advanceButton.gameObject.SetActive(choices.Length == 0);

        foreach (var choice in choices)
        {
            var go = Instantiate(_choiceButtonPrefab, _choicesContainer);
            go.GetComponentInChildren<TextMeshProUGUI>().text = choice.DisplayText;
            go.GetComponent<Button>().onClick.AddListener(() => _manager.MakeChoice(choice));
        }
    }

    private void LoadSprite(string spriteName, Image target)
    {
        if (string.IsNullOrEmpty(spriteName)) return;

        Sprite sprite = Resources.Load<Sprite>(spriteName);
        if (sprite != null)
        {
            target.sprite = sprite;
            return;
        }

        Texture2D tex = Resources.Load<Texture2D>(spriteName);
        if (tex != null)
        {
            Sprite newSprite = Sprite.Create(
                tex,
                new Rect(0, 0, tex.width, tex.height),
                new Vector2(0.5f, 0.5f) 
            );
            target.sprite = newSprite;
            return;
        }

        Debug.LogError($"Не удалось загрузить спрайт или текстуру: {spriteName}");
    }
}