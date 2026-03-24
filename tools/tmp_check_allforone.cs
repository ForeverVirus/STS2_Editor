using System;
using System.Linq;
using STS2_Editor.Scripts.Editor.Graph;
using STS2_Editor.Scripts.Editor.Core.Models;
using MegaCrit.Sts2.Core.Models;

var importer = new NativeBehaviorGraphAutoImporter();
foreach (var id in new[]{"ALLFORONE","ALL_FOR_ONE","AllForOne"})
{
    if (importer.TryCreateGraph(ModStudioEntityKind.Card, id, out var result))
    {
        Console.WriteLine($"ID={id} supported={result.IsSupported} partial={result.IsPartial}");
        Console.WriteLine(result.Summary);
        if (result.Graph != null)
        {
            foreach (var node in result.Graph.Nodes)
            {
                Console.WriteLine($"  node: {node.NodeType} :: {string.Join(", ", node.Properties.Select(p => p.Key+"="+p.Value))}");
            }
        }
    }
    else
    {
        Console.WriteLine($"ID={id} -> no graph");
    }
}
