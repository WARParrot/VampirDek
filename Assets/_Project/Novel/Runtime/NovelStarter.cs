using Cysharp.Threading.Tasks;
using UnityEngine;
using Core;

public class NovelStarter : MonoBehaviour
{
    [SerializeField] private NovelSceneAsset _startingScene;

    async void Start()
    {

        await UniTask.WaitUntil(() => GlobalServices.Resolver != null);

        var novelManager = new GameObject("NovelManager").AddComponent<NovelManager>();
        novelManager.Initialize(_startingScene);
        await GlobalServices.Director.PushModeAsync(novelManager);
    }
}