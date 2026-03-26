using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

var baseDir = Path.GetFullPath(@"tools/Stage59CoverageBaseline/bin/Debug/net9.0");
AssemblyLoadContext.Default.Resolving += (_, name) => {
    var candidate = Path.Combine(baseDir, name.Name + ".dll");
    return File.Exists(candidate) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(candidate) : null;
};
var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(baseDir, "STS2_Editor.dll"));
var serviceType = asm.GetType("STS2_Editor.Scripts.Editor.Graph.NativeBehaviorAutoGraphService");
var kindType = asm.GetType("STS2_Editor.Scripts.Editor.Core.Models.ModStudioEntityKind");
var resultType = asm.GetType("STS2_Editor.Scripts.Editor.Graph.NativeBehaviorAutoGraphResult");
var service = Activator.CreateInstance(serviceType!);
var tryCreate = serviceType!.GetMethod("TryCreateGraph");
var eventKind = Enum.Parse(kindType!, "Event");
foreach (var id in new[]{"DeprecatedEvent","DeprecatedAncientEvent","TheArchitect"})
{
    object? result = null;
    var args = new object?[]{ eventKind, id, null };
    var ok = (bool)tryCreate!.Invoke(service, args)!;
    result = args[2];
    Console.WriteLine($"{id}: ok={ok}, resultNull={result is null}");
    if (result != null)
    {
        var graphProp = resultType!.GetProperty("Graph");
        var summaryProp = resultType.GetProperty("Summary");
        var notesProp = resultType.GetProperty("Notes");
        var graph = graphProp!.GetValue(result);
        Console.WriteLine("  graph=" + (graph?.GetType().GetProperty("GraphId")?.GetValue(graph) ?? "<null>"));
        Console.WriteLine("  summary=" + (summaryProp!.GetValue(result) ?? ""));
        var notes = notesProp!.GetValue(result) as System.Collections.IEnumerable;
        if (notes != null)
        {
            foreach (var note in notes) Console.WriteLine("  note=" + note);
        }
    }
}
