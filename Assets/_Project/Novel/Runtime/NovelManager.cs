using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core;
using UnityEngine.SceneManagement;

public class NovelManager : MonoBehaviour, IGameMode
{
    private NovelSceneAsset _scene;
    private DialogueNode _currentNode;
    private bool _isActive;

    public void Initialize(NovelSceneAsset scene) => _scene = scene;

    public async UniTask EnterAsync(object context)
    {
        _isActive = true;
        NovelUI.Instance.Show();
        NovelUI.Instance.SetManager(this);
        JumpToNode(_scene.StartingNode);
    }

    public async UniTask ExitAsync()
    {
        _isActive = false;
        NovelUI.Instance.Hide();
    }

    public UniTask OnPauseAsync() => UniTask.CompletedTask;
    public UniTask OnResumeAsync() => UniTask.CompletedTask;

    public void JumpToNode(DialogueNode node)
    {
        if (node == null)
        {
            GlobalServices.EventBus.Publish(new NovelSceneEndedEvent(_scene));
            GlobalServices.Director.PopModeAsync().Forget();
            SceneManager.LoadSceneAsync("Core");
            return;
        }
        _currentNode = node;
        NovelUI.Instance.UpdateDisplay(node);
        NovelUI.Instance.SetChoices(node.Choices);
        GlobalServices.EventBus.Publish(new DialogueLineShownEvent(node));
    }

    public void AdvanceDialog(DialogueNode nextNode = null)
    {
        if (nextNode == null && _currentNode.Choices.Length == 0)
            nextNode = _currentNode.NextNode;
        if (_currentNode.Choices.Length > 0 && nextNode == null)
            return;
        JumpToNode(nextNode);
    }

    public void MakeChoice(ChoiceOption option) => AdvanceDialog(option.NextNode);
}
