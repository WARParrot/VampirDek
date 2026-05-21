using VContainer;

namespace Core
{
    public static class GlobalServices
    {
        public static IObjectResolver Resolver { get; set; }
        public static GameDirector Director => Resolver.Resolve<GameDirector>();
        public static EventBus EventBus => Resolver.Resolve<EventBus>();
        public static ISaveSystem SaveSystem => Resolver.Resolve<ISaveSystem>();
        public static DevConsole DevConsole => Resolver.Resolve<DevConsole>();
        public static PersistentPlayerData PlayerData { get; set; }
        public static IProgressionService Progression { get; set; }
        public static IGameStateService GameStateService { get; set; }
    }
}
