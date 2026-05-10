using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace Core
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<EventBus>(Lifetime.Singleton);
            builder.Register<ISaveSystem, SaveSystem>(Lifetime.Singleton);
            builder.Register<DevConsole>(Lifetime.Singleton);
            builder.Register<GameDirector>(Lifetime.Singleton);
            builder.Register<InputController>(Lifetime.Singleton);
        }

        private void Start()
        {
            GlobalServices.Resolver = Container;
            DontDestroyOnLoad(gameObject);
        }
    }
}