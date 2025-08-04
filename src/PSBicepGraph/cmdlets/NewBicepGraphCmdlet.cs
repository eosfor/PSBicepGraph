namespace PSBicepGraph.Commands;

using System.Management.Automation;
using Bicep.Core.Parsing;
using Bicep.Core.Syntax;
using PSGraph.Model;

[Cmdlet(VerbsCommon.New, "BicepGraph")]
public class NewBicepGraphCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    protected override void ProcessRecord()
    {
        string fullPath = System.IO.Path.GetFullPath(Path);
        if (!File.Exists(fullPath))
        {
            ThrowTerminatingError(
                new ErrorRecord(
                    new FileNotFoundException($"File {fullPath} not found."),
                    "FileNotFound",
                    ErrorCategory.InvalidArgument,
                    fullPath));
            return;
        }

        string contents = File.ReadAllText(Path);

        var parser = new Parser(contents);
        ProgramSyntax program = parser.Program();

        var visitor = new ParameterDependencyVisitor();
        visitor.Visit(program);

        var graph = new PsBidirectionalGraph();

        foreach (var kvp in visitor.Dependencies.OrderBy(kvp => kvp.Key))
        {
            var s = new PSVertex(kvp.Key);
            graph.AddVertex(s);

            foreach (var referenced in kvp.Value)
            {
                var t = new PSVertex(referenced);
                graph.AddVerticesAndEdge(new PSEdge(s, t, new PSEdgeTag("none")));
            }
        }

        WriteObject(graph);
    }

}
