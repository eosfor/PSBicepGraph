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

        // Enumerate all semantic models in the compilation.  Each
        // model corresponds to one Bicep file.  We print the file
        // URI, then emit a textual representation of the syntax
        // tree using the SyntaxWriter helper from the sample.
        var g = new PsBidirectionalGraph();
        foreach (var model in compilation.GetAllModels().OfType<SemanticModel>())
        {
            //Console.WriteLine($"\n===== {model.SourceFile.Uri} =====");
            var syntaxTree = model.SourceFile.ProgramSyntax;
            //SyntaxWriter.WriteSyntax(syntaxTree, Console.Out);
            SyntaxWriter.WriteSyntax(syntaxTree, g);
        }

        // Build a map of cross‑object dependencies.  The
        // DependencyCollectorVisitor walks each semantic model and
        // records which declared symbols (variables, resources,
        // parameters, outputs, modules, etc.) reference which
        // other declared symbols.  We merge the per‑file results
        // into a single dictionary keyed by the declaration.
        var dependencyMap = new Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>>();

        foreach (var model in compilation.GetAllModels().OfType<SemanticModel>())
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

        // Print the dependency graph.  For each declaration we
        // output its name, kind and the names of the symbols it
        // references.  This gives a simple view of the
        // cross‑object dependencies within the compilation.
        // Console.WriteLine("\n===== Cross‑object dependencies =====");
        // foreach (var kvp in dependencyMap)
        // {
        //     var declaringSymbol = kvp.Key;
        //     var dependencies = kvp.Value;
        //     var references = dependencies.Any()
        //         ? string.Join(", ", dependencies.Select(d => $"{d.Name} ({d.Kind})"))
        //         : "<none>";
        //     Console.WriteLine($"{declaringSymbol.Name} ({declaringSymbol.Kind}) -> {references}");
        // }
        var astGraph = new PsBidirectionalGraph();
        SyntaxWriter.WriteSyntax(dependencyMap, astGraph);
        WriteObject(astGraph);

        // -----------------------------------------------------------------
        // The following section demonstrates another approach to building
        // a dependency map using only the syntax tree.  The
        // ParameterDependencyVisitor walks the AST (derived from
        // AstVisitor) and, for each variable or parameter, records
        // the names of variables it references in its initializer.
        // Unlike DependencyCollectorVisitor, this visitor does not use
        // the semantic model and therefore cannot distinguish
        // between symbols with the same name in different files.  It
        // serves as an illustrative example of how to use
        // AstVisitor.
        //Console.WriteLine("\n===== Parameter and variable dependencies (AST only) =====");
        foreach (var model in compilation.GetAllModels().OfType<SemanticModel>())
        {
            var parameterVisitor = new ParameterDependencyVisitor();
            parameterVisitor.Visit(model.SourceFile.ProgramSyntax);
            //Console.WriteLine($"\n===== {model.SourceFile.Uri} =====");
            foreach (var decl in parameterVisitor.Dependencies)
            {
                var refs = decl.Value.Any()
                    ? string.Join(", ", decl.Value)
                    : "<none>";
                //Console.WriteLine($"{decl.Key} -> {refs}");
            }
        }


        WriteObject(g);
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

        // 1) PS-путь (учтёт драйвы, ~, Wildcards, etc)
        // var resolved = pi.GetResolvedPSPathFromPSPath(path);
        // if (resolved.Count > 0)
        // {
        //     return resolved[0].Path;
        // }

        // 2) Если абсолютный в файловой системе – просто возвращаем
        if (System.IO.Path.IsPathRooted(path))
        {
            return path;
        }

        // 3) Относительный – комбинируем и нормализуем через PS-интринсики
        var baseDir = baseFolder
            ?? pi.CurrentFileSystemLocation.ProviderPath;
        // Собираем путь (на том же провайдере, что и базовая папка)
        var combined = pi.Combine(baseDir, path);

        // TODO: тут остаются пути типа /aaa/bbb/ccc/../../ddd. надо чтобы все они были уже разрешенными корректно
        return System.IO.Path.GetFullPath(combined);
    }



}
