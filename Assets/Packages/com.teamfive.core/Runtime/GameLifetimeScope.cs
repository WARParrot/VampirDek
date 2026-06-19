using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace Core
{
    public class GameLifetimeScope : LifetimeScope
    {
        private const int MinimumPlayableFrameRate = 30;

        [Header("Performance")]
        [SerializeField, Min(MinimumPlayableFrameRate)]
        private int targetFrameRate = 60;

        [SerializeField]
        private bool useVSync;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<EventBus>(Lifetime.Singleton);
            builder.Register<ISaveSystem, SaveSystem>(Lifetime.Singleton);
            builder.Register<DevConsole>(Lifetime.Singleton);
            builder.Register<GameDirector>(Lifetime.Singleton);
            builder.Register<InputController>(Lifetime.Singleton);
            builder.Register<ModManager>(Lifetime.Singleton);
        }

        private void Start()
        {
            ApplyFramePacing();
            GlobalServices.Resolver = Container;
            DontDestroyOnLoad(gameObject);
        }

        private void ApplyFramePacing()
        {
            QualitySettings.vSyncCount = useVSync ? 1 : 0;
            Application.targetFrameRate = useVSync ? -1 : Mathf.Max(MinimumPlayableFrameRate, targetFrameRate);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            targetFrameRate = Mathf.Max(MinimumPlayableFrameRate, targetFrameRate);
        }
#endif
    }
}
