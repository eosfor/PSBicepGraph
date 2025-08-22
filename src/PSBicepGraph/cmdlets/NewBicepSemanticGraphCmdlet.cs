namespace PSBicepGraph.Commands;

using System.Linq;
using System.Management.Automation;
using Bicep.Core;
using Bicep.Core.Diagnostics;
using Bicep.Core.Extensions;
using Bicep.Core.Navigation;
using Bicep.Core.Parsing;
using Bicep.Core.Semantics;
using Bicep.Core.Semantics.Metadata;
using Bicep.Core.SourceGraph;
using Bicep.Core.Syntax;
using Bicep.IO.Abstraction;
using Bicep.IO.InMemory;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using PSGraph.Model;
using QuikGraph;
using SharpYaml.Tokens;

[Cmdlet(VerbsCommon.New, "BicepSemanticGraph")]
public class NewBicepSemanticGraphCmdlet : PSCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    public string Path { get; set; } = string.Empty;

    [Parameter(Mandatory = false, Position = 1)]
    public SymbolKind CollapseOn;

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
        Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> dependencyMap;
        Dictionary<SemanticModel, (HashSet<DeclaredSymbol>, HashSet<DeclaredSymbol>)> virtualNodes;
        Dictionary<DeclaredSymbol, HashSet<JToken>> armNodes;

        CreateDependencyMap(compilation, out dependencyMap, out virtualNodes, out armNodes);
        var fullGraph = GraphBuilderHelper.Build(dependencyMap, virtualNodes, armNodes);

        
        if (MyInvocation.BoundParameters.ContainsKey("CollapseOn"))
        {
            var alg = new SemanticGraphCondencationAlgorithm();
            PsBidirectionalGraph condencedGraph;
            condencedGraph = alg.Condence(fullGraph, CollapseOn);
            WriteObject(condencedGraph);
        }
        else
        {
            WriteObject(fullGraph);
        }
    }

    private void CreateDependencyMap(Compilation compilation, out Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> dependencyMap, out Dictionary<SemanticModel, (HashSet<DeclaredSymbol>, HashSet<DeclaredSymbol>)> virtualNodes, out Dictionary<DeclaredSymbol, HashSet<JToken>> armNodes)
    {
        dependencyMap = new Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>>();
        virtualNodes = new Dictionary<SemanticModel, (HashSet<DeclaredSymbol>, HashSet<DeclaredSymbol>)>();
        armNodes = new Dictionary<DeclaredSymbol, HashSet<JToken>>();

        var models = compilation.GetAllModels().OfType<SemanticModel>();
        foreach (var model in models)
        {
            var perFileDeps = DependencyCollectorVisitor.CollectDependencies(model);
            var sourcesAndSyncs = GetSourcesAndSinks(perFileDeps);
            var k = perFileDeps.SelectMany(v => v.Value).ToHashSet();
            var v = perFileDeps.Select(v => v.Key).ToHashSet().Union(k).ToHashSet();
            virtualNodes[model] = (v, new HashSet<DeclaredSymbol>());

            // (perFileDeps.SelectMany(v => v.Value).Where(e => e.Kind == SymbolKind.Parameter).ToHashSet(),
            // perFileDeps.SelectMany(v => v.Value).Where(e => e.Kind == SymbolKind.Output).ToHashSet()); //sourcesAndSyncs;

            foreach (var kvp in perFileDeps)
            {
                if (!dependencyMap.TryGetValue(kvp.Key, out var set))
                {
                    set = new HashSet<DeclaredSymbol>();
                    dependencyMap[kvp.Key] = set;

                    // for each module get it's source file path and then match it to a corresponding model
                    // extract resources from the model and add them as deps for the module symbol

                    if (kvp.Key.Kind == SymbolKind.Module)
                    {
                        var mdSyntax = kvp.Key.DeclaringSyntax as ModuleDeclarationSyntax;
                        ResultWithDiagnosticBuilder<ISourceFile> srcFileObj = kvp.Key.Context.SourceFileLookup.TryGetSourceFile(mdSyntax);
                        var srcFile = srcFileObj.Unwrap();
                        if (srcFile is BicepFile)
                        {
                            var targetModel = compilation.GetSemanticModel(srcFile) as SemanticModel;
                            var resources = targetModel
                                .Root.ResourceDeclarations
                                .Select(r => r)
                                .ToHashSet();
                            set.UnionWith(resources);
                        }
                        else if (srcFile is ArmTemplateFile armTemplate)
                        {
                            var armSemanticModel = compilation.GetSemanticModel(srcFile) as ArmTemplateSemanticModel;
                            if (armSemanticModel != null)
                            {
                                var res = armSemanticModel.SourceFile.TemplateObject["resources"].ToHashSet();
                                if (res.Count > 0)
                                {
                                    armNodes[kvp.Key] = res;
                                }
                            }
                        }

                    }
                }
                set.UnionWith(kvp.Value);
            }
        }
    }

    private (HashSet<DeclaredSymbol> sources, HashSet<DeclaredSymbol> sinks) GetSourcesAndSinks(Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> deps)
    {
        // Все вершины, в которые кто-то ссылается (есть входящее)
        var targets = deps.Values
                          .SelectMany(v => v ?? Enumerable.Empty<DeclaredSymbol>())
                          .ToHashSet();

        // Все вершины, у которых есть хотя бы одно исходящее
        var withOut = deps.Where(kvp => kvp.Value != null && kvp.Value.Count > 0)
                          .Select(kvp => kvp.Key)
                          .ToHashSet();

        // Полная вселенная вершин = ключи ∪ targets
        var all = new HashSet<DeclaredSymbol>(deps.Keys);
        all.UnionWith(targets);

        // Источники: in-degree == 0
        var sources = new HashSet<DeclaredSymbol>(all);
        sources.ExceptWith(targets);

        // Стоки: out-degree == 0 (включая ключи с пустыми множествами и чистые "targets")
        var sinks = new HashSet<DeclaredSymbol>(all);
        sinks.ExceptWith(withOut);

        return (sources, sinks);
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
                if (!trimmed.StartsWith("br/public:avm"))
                    queue.Enqueue(new Uri(resolved));
            }

        }
        return compiler.CreateCompilation(entrypointUri).GetAwaiter().GetResult();
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
