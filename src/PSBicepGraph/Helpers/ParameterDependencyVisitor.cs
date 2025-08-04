namespace PSBicepGraph;

using Bicep.Core.Syntax;
using Bicep.Core.Visitors;
using Bicep.Core.Emit;
internal class ParameterDependencyVisitor : AstVisitor
{
    private string? currentDeclarationName;
    private readonly Dictionary<string, HashSet<string>> dependencies = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, HashSet<string>> Dependencies => dependencies;

    public override void VisitVariableDeclarationSyntax(VariableDeclarationSyntax syntax)
    {
        var previous = currentDeclarationName;

        // Запоминаем имя текущей декларации и создаём для него набор зависимостей
        currentDeclarationName = syntax.Name.IdentifierName;
        dependencies[currentDeclarationName] = new HashSet<string>();

        // Обходим выражение, определяющее значение переменной
        this.Visit(syntax.Value);

        // Восстанавливаем предыдущее значение
        currentDeclarationName = previous;
    }

    public override void VisitParameterDeclarationSyntax(ParameterDeclarationSyntax syntax)
    {
        var previous = currentDeclarationName;
        currentDeclarationName = syntax.Name.IdentifierName;
        dependencies[currentDeclarationName] = new HashSet<string>();

        // Если есть модификатор по умолчанию, обходим только его значение
        if (syntax.Modifier is ParameterDefaultValueSyntax def)
        {
            this.Visit(def.DefaultValue);
        }

        currentDeclarationName = previous;
    }

    public override void VisitVariableAccessSyntax(VariableAccessSyntax syntax)
    {
        // Теперь currentDeclarationName будет заполнено, если мы находимся внутри объявления
        if (this.currentDeclarationName is { } current)
        {
            dependencies[current].Add(syntax.Name.IdentifierName);
        }

        base.VisitVariableAccessSyntax(syntax);
    }
}
