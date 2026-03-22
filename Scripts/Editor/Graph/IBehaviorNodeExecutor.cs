namespace STS2_Editor.Scripts.Editor.Graph;

public interface IBehaviorNodeExecutor
{
    string NodeType { get; }

    bool CanExecute(BehaviorGraphNodeDefinition node);

    Task ExecuteAsync(BehaviorGraphNodeDefinition node, BehaviorGraphExecutionContext context);
}
