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
/// Collects all syntax nodes in a Bicep program.  The collector
/// builds a flat list of syntax nodes along with their parents
/// and depth in the tree.  This helper is used by SyntaxWriter
/// above and mirrors the behaviour of the original sample.
/// </summary>
public class SyntaxCollectorVisitor : CstVisitor
{
    public record SyntaxItem(SyntaxBase Syntax, SyntaxItem? Parent, int Depth)
    {
        public IEnumerable<SyntaxItem> GetAncestors()
        {
            var data = this;
            while (data.Parent is { } parent)
            {
                yield return parent;
                data = parent;
            }
        }
    }

    private readonly IList<SyntaxItem> syntaxList = new List<SyntaxItem>();
    private SyntaxItem? parent = null;
    private int depth = 0;

    private SyntaxCollectorVisitor()
    {
    }

    public static SyntaxItem[] Build(SyntaxBase syntax)
    {
        var visitor = new SyntaxCollectorVisitor();
        visitor.Visit(syntax);
        return [.. visitor.syntaxList];
    }

    protected override void VisitInternal(SyntaxBase syntax)
    {
        var syntaxItem = new SyntaxItem(Syntax: syntax, Parent: parent, Depth: depth);
        syntaxList.Add(syntaxItem);

        var prevParent = parent;
        parent = syntaxItem;
        depth++;
        base.VisitInternal(syntax);
        depth--;
        parent = prevParent;
    }
}
