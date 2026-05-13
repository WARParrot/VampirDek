using Core;

public readonly struct DialogueLineShownEvent : IGameEvent
{
    public readonly DialogueNode Node;
    public DialogueLineShownEvent(DialogueNode node) => Node = node;
}

public readonly struct ChoiceSelectedEvent : IGameEvent
{
    public readonly ChoiceOption Option;
    public readonly DialogueNode SourceNode;
    public ChoiceSelectedEvent(ChoiceOption option, DialogueNode sourceNode)
    {
        Option = option;
        SourceNode = sourceNode;
    }
}

public readonly struct NovelSceneEndedEvent : IGameEvent
{
    public readonly NovelSceneAsset Scene;
    public NovelSceneEndedEvent(NovelSceneAsset scene) => Scene = scene;
}