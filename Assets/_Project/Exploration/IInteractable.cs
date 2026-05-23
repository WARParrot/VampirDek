namespace Exploration
{
    public interface IInteractable
    {
        void Interact(ExplorationController player);
        string PromptText { get; }
    }
}