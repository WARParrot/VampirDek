using System.Collections;
using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshProUGUI))]
public class TypewriterEffect : MonoBehaviour
{
    private TextMeshProUGUI _text;
    private Coroutine _typingCoroutine;

    void Awake() => _text = GetComponent<TextMeshProUGUI>();

    public void ShowText(string fullText)
    {
        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypeText(fullText));
    }

    private IEnumerator TypeText(string fullText)
    {
        _text.text = "";
        foreach (char c in fullText)
        {
            _text.text += c;
            yield return new WaitForSeconds(0.05f);
        }
    }
}