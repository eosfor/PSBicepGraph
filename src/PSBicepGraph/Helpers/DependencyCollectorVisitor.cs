using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO.Abstractions;
using Bicep.Core;
using Bicep.Core.Analyzers.Interfaces;
using Bicep.Core.Analyzers.Linter;
using Bicep.Core.Configuration;
using Bicep.Core.Emit;
using Bicep.Core.Features;
using Bicep.Core.FileSystem;
using Bicep.Core.Parsing;
using Bicep.Core.Registry;
using Bicep.Core.Syntax;
using Bicep.Core.Text;
using Bicep.Core.Registry.Auth;
using Bicep.Core.Semantics;
using Bicep.Core.Semantics.Namespaces;
using Bicep.Core.TypeSystem.Providers;
using Bicep.Core.Utils;
using Bicep.IO.Abstraction;
using Bicep.IO.InMemory;
using Microsoft.Extensions.DependencyInjection;
using Environment = Bicep.Core.Utils.Environment;
using Bicep.Core.SourceGraph;
using Bicep.Core.Extensions;
using Bicep.Core.Registry.Catalog.Implementation;

/// <summary>
/// Visits a Bicep syntax tree and builds a mapping of declared
/// symbols to the symbols they reference.  It is inspired by
/// CyclicCheckVisitor from Bicep.Core/TypeSystem, but instead of
/// detecting cycles it exposes the full dependency graph.  A
/// semantic model is used to resolve each syntax node into a
/// Symbol.  Only DeclaredSymbol instances are stored in the
/// dependency map.
/// </summary>
public class DependencyCollectorVisitor : CstVisitor
{
    private readonly SemanticModel model;
    private readonly Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> dependencies = new();
    private readonly Stack<DeclaredSymbol> declarationStack = new();

    private DependencyCollectorVisitor(SemanticModel model)
    {
        this.model = model;
    }


    /// <summary>
    /// Collects dependencies for the given semantic model.  The
    /// returned dictionary maps each declaration symbol to the set
    /// of declared symbols it references somewhere in its body.
    /// </summary>
    public static Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> CollectDependencies(SemanticModel model)
    {
        var visitor = new DependencyCollectorVisitor(model);
        visitor.Visit(model.SourceFile.ProgramSyntax);
        return visitor.dependencies;
    }

    private void PushDeclaration(DeclaredSymbol? symbol)
    {
        if (symbol is null)
        {
            return;
        }
        declarationStack.Push(symbol);
        // Ensure there is an entry in the dependency map for this symbol
        if (!dependencies.ContainsKey(symbol))
        {
            dependencies[symbol] = new HashSet<DeclaredSymbol>();
        }
    }

    private void PopDeclaration()
    {
        if (declarationStack.Count > 0)
        {
            declarationStack.Pop();
        }
    }


    // Handle various declarations.  When visiting a declaration
    // syntax node we resolve its symbol, push it onto the
    // declaration stack, visit its children and then pop it off.
    public override void VisitVariableDeclarationSyntax(VariableDeclarationSyntax syntax)
    {
        var symbol = model.GetSymbolInfo(syntax) as DeclaredSymbol;
        PushDeclaration(symbol);
        base.VisitVariableDeclarationSyntax(syntax);
        PopDeclaration();
    }

    public override void VisitParameterDeclarationSyntax(ParameterDeclarationSyntax syntax)
    {
        var symbol = model.GetSymbolInfo(syntax) as DeclaredSymbol;
        PushDeclaration(symbol);
        base.VisitParameterDeclarationSyntax(syntax);
        PopDeclaration();
    }

    public override void VisitResourceDeclarationSyntax(ResourceDeclarationSyntax syntax)
    {
        var symbol = model.GetSymbolInfo(syntax) as DeclaredSymbol;
        PushDeclaration(symbol);
        base.VisitResourceDeclarationSyntax(syntax);
        PopDeclaration();
    }

    public override void VisitModuleDeclarationSyntax(ModuleDeclarationSyntax syntax)
    {
        var symbol = model.GetSymbolInfo(syntax) as DeclaredSymbol;
        PushDeclaration(symbol);
        base.VisitModuleDeclarationSyntax(syntax);
        PopDeclaration();
    }

    public override void VisitOutputDeclarationSyntax(OutputDeclarationSyntax syntax)
    {
        var symbol = model.GetSymbolInfo(syntax) as DeclaredSymbol;
        PushDeclaration(symbol);
        base.VisitOutputDeclarationSyntax(syntax);
        PopDeclaration();
    }

    public override void VisitFunctionDeclarationSyntax(FunctionDeclarationSyntax syntax)
    {
        var symbol = model.GetSymbolInfo(syntax) as DeclaredSymbol;
        PushDeclaration(symbol);
        base.VisitFunctionDeclarationSyntax(syntax);
        PopDeclaration();
    }

    public override void VisitTypeDeclarationSyntax(TypeDeclarationSyntax syntax)
    {
        var symbol = model.GetSymbolInfo(syntax) as DeclaredSymbol;
        PushDeclaration(symbol);
        base.VisitTypeDeclarationSyntax(syntax);
        PopDeclaration();
    }

    // Record references when we encounter an access syntax.  If we
    // are currently inside a declaration (declarationStack is not
    // empty) we resolve the accessed symbol and, if it is a
    // declared symbol, add it to the dependency set for the
    // current declaration.  Self references are ignored.
    public override void VisitVariableAccessSyntax(VariableAccessSyntax syntax)
    {
        RecordReference(syntax);
        base.VisitVariableAccessSyntax(syntax);
    }

    public override void VisitResourceAccessSyntax(ResourceAccessSyntax syntax)
    {
        RecordReference(syntax);
        base.VisitResourceAccessSyntax(syntax);
    }

    public override void VisitTypeVariableAccessSyntax(TypeVariableAccessSyntax syntax)
    {
        RecordReference(syntax);
        base.VisitTypeVariableAccessSyntax(syntax);
    }

    public override void VisitFunctionCallSyntax(FunctionCallSyntax syntax)
    {
        // Record the called function as a dependency, if it is
        // user‑declared.  Built‑in functions return null from
        // GetSymbolInfo and are therefore ignored.
        RecordReference(syntax);
        base.VisitFunctionCallSyntax(syntax);
    }

    public override void VisitPropertyAccessSyntax(PropertyAccessSyntax syntax)
    {
        // Попробуем разрешить i.myExportObject или i.myExportObject.one
        RecordReference(syntax);
        base.VisitPropertyAccessSyntax(syntax);
    }


    private void RecordReference(SyntaxBase syntax)
    {
        if (declarationStack.Count == 0)
        {
            // We are not inside a declaration, so nothing to record.
            return;
        }

        var referenced = model.GetSymbolInfo(syntax) as DeclaredSymbol;
        if (referenced is null)
        {
            return;
        }

        var currentDeclaration = declarationStack.Peek();
        if (!EqualityComparer<DeclaredSymbol>.Default.Equals(referenced, currentDeclaration))
        {
            dependencies[currentDeclaration].Add(referenced);
        }
    }
}
