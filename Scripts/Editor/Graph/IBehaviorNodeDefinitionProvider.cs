namespace STS2_Editor.Scripts.Editor.Graph;

public interface IBehaviorNodeDefinitionProvider
{
    IEnumerable<BehaviorGraphNodeDefinitionDescriptor> GetDefinitions();
}
