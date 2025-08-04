namespace PSBicepGraph.Commands;

using System.Management.Automation;
using Bicep.Core;
using Bicep.Core.Extensions;
using Bicep.Core.Navigation;
using Bicep.Core.Parsing;
using Bicep.Core.Semantics;
using Bicep.Core.SourceGraph;
using Bicep.Core.Syntax;
using Bicep.IO.Abstraction;
using Bicep.IO.InMemory;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using PSGraph.Model;

[Cmdlet(VerbsCommon.New, "BicepSemanticGraph")]
public class NewBicepSemanticGraphCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    private ServiceProvider? services;

    protected override void BeginProcessing()
    {
        var fileExplorer = new InMemoryFileExplorer();

        services = new ServiceCollection()
                .AddBicepCore(fileExplorer)
                .BuildServiceProvider();

        base.BeginProcessing();
    }

    protected override void ProcessRecord()
    {

        var resolvedpath = ResolveFullPath(Path);
        var compilation = CollectAllBicepFiles(new Uri(resolvedpath));

        // Build a map of cross‑object dependencies.  The
        // DependencyCollectorVisitor walks each semantic model and
        // records which declared symbols (variables, resources,
        // parameters, outputs, modules, etc.) reference which
        // other declared symbols.  We merge the per‑file results
        // into a single dictionary keyed by the declaration.
        var dependencyMap = new Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>>();

        var models = compilation.GetAllModels().OfType<SemanticModel>();
        foreach (var model in models)
        {
            var perFileDeps = DependencyCollectorVisitor.CollectDependencies(model);
            foreach (var kvp in perFileDeps)
            {
                if (!dependencyMap.TryGetValue(kvp.Key, out var set))
                {
                    set = new HashSet<DeclaredSymbol>();
                    dependencyMap[kvp.Key] = set;
                }
                set.UnionWith(kvp.Value);
            }
        }

        var graph = new PsBidirectionalGraph();
        SyntaxWriter.WriteSyntax(dependencyMap, graph);
        WriteObject(graph);
    }

    public Compilation CollectAllBicepFiles(Uri entrypointUri)
    {
        var visited = new HashSet<Uri>();
        var result = new List<Uri>();
        var queue = new Queue<Uri>();

        var fileExplorer = services.GetRequiredService<IFileExplorer>();
        var compiler = services.GetRequiredService<BicepCompiler>();
        var srcFileFactory = services.GetRequiredService<ISourceFileFactory>();

        queue.Enqueue(entrypointUri);

        while (queue.Count > 0)
        {
            var uri = queue.Dequeue();
            if (!visited.Add(uri))
                continue;

            result.Add(uri);

            // TODO: before this we need to make sure that the file is a .bicep file
            var text = File.ReadAllText(uri.AbsolutePath);

            var sourceFile = srcFileFactory.CreateBicepFile(uri, text);
            fileExplorer
                .GetFile(IOUri.FromLocalFilePath(uri.AbsolutePath))
                .Write(text);

            var artifacts = sourceFile.ProgramSyntax.Children
                                .Where(e => e is ModuleDeclarationSyntax or CompileTimeImportDeclarationSyntax)
                                .OfType<IArtifactReferenceSyntax>()
                                .ToList();


            foreach (var item in artifacts)
            {
                var raw = item.TryGetPath()?.ToString();
                var trimmed = raw?
                        .Trim()              // на всякий случай с краёв обрезаем пробелы
                        .Trim('\'', '"');    // убираем ' или ";
                var resolved = ResolveFullPath(trimmed, uri.GetParentUri().AbsolutePath);

                // TODO: hack, solve for this somehow. we only need loval paths
                if (!resolved.Contains("br/public:avm"))
                    queue.Enqueue(new Uri(resolved));
            }

        }
        return compiler.CreateCompilation(entrypointUri).GetAwaiter().GetResult(); ;
    }

    private string ResolveFullPath(string path, string? baseFolder = null)
    {
        var pi = this.SessionState.Path;

        if (System.IO.Path.IsPathRooted(path))
        {
            return path;
        }

        var baseDir = baseFolder
            ?? pi.CurrentFileSystemLocation.ProviderPath;
        var combined = pi.Combine(baseDir, path);

        // TODO: тут остаются пути типа /aaa/bbb/ccc/../../ddd. надо чтобы все они были уже разрешенными корректно
        return System.IO.Path.GetFullPath(combined);
    }



}
