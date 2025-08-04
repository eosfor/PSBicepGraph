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
using PSGraph.Model;
using Microsoft.WindowsAzure.ResourceStack.Common.Extensions;

/// <summary>
/// Prints a Bicep syntax tree to a TextWriter.  The output uses
/// ASCII art to visualise the hierarchy of syntax nodes and
/// tokens.  This class is copied from the Bicep access syntax
/// tree sample (https://github.com/anthony-c-martin/samples/) and
/// lightly adapted for this prototype.
/// </summary>
public static class SyntaxWriter
{
    public static void WriteSyntax(SyntaxBase syntax, TextWriter writer)
    {
        var syntaxList = SyntaxCollectorVisitor.Build(syntax);
        var syntaxByParent = syntaxList.ToLookup(x => x.Parent);

        foreach (var element in syntaxList)
        {
            writer.WriteLine(GetSyntaxLoggingString(syntaxByParent, element));
        }
    }

    public static void WriteSyntax(SyntaxBase syntax, PsBidirectionalGraph g)
    {
        var syntaxList = SyntaxCollectorVisitor.Build(syntax);

        // Локальная функция для получения «метки» узла:
        // если это Token, возвращает его текст, иначе — имя класса узла.
        static string GetLabel(SyntaxBase node) =>
            node switch
            {
                Token t => t.Text,
                _ => node.GetType().Name
            };

        foreach (var element in syntaxList)
        {
            int ti = Array.IndexOf(syntaxList, element);
            var t = ti.ToString() + ": " + GetLabel(element.Syntax);

            foreach (var ancestor in element.GetAncestors().Reverse())
            {
                int si = Array.IndexOf(syntaxList, ancestor);
                // Получаем метки для текущего узла и его родителя.
                var s = si.ToString() + ": " + GetLabel(ancestor.Syntax);

                // Добавляем вершины и ребро в граф
                var sNode = new PSVertex(s);
                var tNode = new PSVertex(t);
                var edge = new PSEdge(sNode, tNode, new PSEdgeTag("none"));
                g.AddVerticesAndEdge(edge);
            }
        }
    }

    public static void WriteSyntax(Dictionary<DeclaredSymbol, HashSet<DeclaredSymbol>> dependencyMap, PsBidirectionalGraph g)
    {
        foreach (var kvp in dependencyMap)
        {
            string s, t;
            var declaringSymbol = kvp.Key;
            var dependencies = kvp.Value;

            s = $"{declaringSymbol.Name} ({declaringSymbol.Kind})";
            var sNode = new PSVertex(s);
            g.AddVertex(sNode);

            foreach (var child in dependencies)
            {
                t = $"{child.Name} ({child.Kind})";
                var tNode = new PSVertex(t);
                g.AddVertex(tNode);
                g.AddEdge(new PSEdge(sNode, tNode, new PSEdgeTag("none")));
            }
        }

    }

    private static string GetSyntaxLoggingString(
        ILookup<SyntaxCollectorVisitor.SyntaxItem?, SyntaxCollectorVisitor.SyntaxItem> syntaxByParent,
        SyntaxCollectorVisitor.SyntaxItem syntax)
    {
        // Build a visual graph with lines to help understand the syntax hierarchy
        var graphPrefix = new StringBuilder();

        foreach (var ancestor in syntax.GetAncestors().Reverse().Skip(1))
        {
            var isLast = (ancestor.Depth > 0 && ancestor == syntaxByParent[ancestor.Parent].Last());
            graphPrefix.Append(isLast switch
            {
                true => "  ",
                _ => "| ",
            });
        }

        if (syntax.Depth > 0)
        {
            var isLast = syntax == syntaxByParent[syntax.Parent].Last();
            graphPrefix.Append(isLast switch
            {
                true => "└─",
                _ => "├─",
            });
        }

        return syntax.Syntax switch
        {
            Token token => $"{graphPrefix}Token({token.Type}) |{EscapeWhitespace(token.Text)}|",
            _ => $"{graphPrefix}{syntax.Syntax.GetType().Name}",
        };
    }

    private static string EscapeWhitespace(string input)
        => input
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
}
