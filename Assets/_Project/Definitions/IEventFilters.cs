using Core;

namespace Definitions
{
    public interface IPhasedEvent : IGameEvent
    {
        string PhaseId { get; }
    }

    public interface ISubjectEvent : IGameEvent
    {
        IGameEntity Subject { get; }
    }
}
