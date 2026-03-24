using System;
using System.Collections.Generic;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Core.Utilities;

ModStudioLocalization.SetLanguage("zh-CN");
var graph = new BehaviorGraphDefinition
{
    GraphId = "test",
    Name = "test"
};
graph.Nodes.Clear();
graph.Nodes.Add(new BehaviorGraphNodeDefinition
{
    NodeId = graph.EntryNodeId,
    NodeType = "flow.entry",
    DisplayName = "Entry",
    Description = "Entry"
});
graph.Nodes.Add(new BehaviorGraphNodeDefinition
{
    NodeId = graph.ExitNodeId,
    NodeType = "flow.exit",
    DisplayName = "Exit",
    Description = "Exit"
});
graph.Nodes.Add(new BehaviorGraphNodeDefinition
{
    NodeId = "damage",
    NodeType = "combat.damage",
    DisplayName = "Damage",
    Properties = new Dictionary<string,string>(StringComparer.Ordinal) { ["amount"] = "8", ["target"] = "current_target", ["props"] = "none" }
});
graph.Nodes.Add(new BehaviorGraphNodeDefinition
{
    NodeId = "block",
    NodeType = "combat.gain_block",
    DisplayName = "Block",
    Properties = new Dictionary<string,string>(StringComparer.Ordinal) { ["amount"] = "5", ["target"] = "self", ["props"] = "none" }
});
graph.Connections.Clear();
graph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = graph.EntryNodeId, FromPortId = "next", ToNodeId = "damage", ToPortId = "in" });
graph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "damage", FromPortId = "out", ToNodeId = "block", ToPortId = "in" });
graph.Connections.Add(new BehaviorGraphConnectionDefinition { FromNodeId = "block", FromPortId = "out", ToNodeId = graph.ExitNodeId, ToPortId = "in" });
var generator = new GraphDescriptionGenerator();
var generated = generator.Generate(graph);
Console.WriteLine(generated.Description);
