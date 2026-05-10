using Cysharp.Threading.Tasks;
using Core;
using Exploration;
using UnityEngine;

public class GameBootstrap : MonoBehaviour
{
    async void Start()
    {
        await UniTask.WaitUntil(() => GlobalServices.Resolver != null);
        var explorationMode = new ExplorationMode("TestWorld");
        await GlobalServices.Director.PushModeAsync(explorationMode);
    }
}