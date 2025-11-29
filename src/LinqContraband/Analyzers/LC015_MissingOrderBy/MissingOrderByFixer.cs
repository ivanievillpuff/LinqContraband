using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LinqContraband.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace LinqContraband.Analyzers.LC015_MissingOrderBy;

/// <summary>
/// Provides code fixes for LC015. Injects OrderBy(x => x.Id) before unordered Skip/Last calls.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MissingOrderByFixer))]
[Shared]
public class MissingOrderByFixer : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(MissingOrderByAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var invocation = root?.FindNode(diagnosticSpan).FirstAncestorOrSelf<InvocationExpressionSyntax>();
        if (invocation == null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                "Add OrderBy",
                c => AddOrderByAsync(context.Document, invocation, c),
                nameof(MissingOrderByFixer)),
            diagnostic);
    }

    private async Task<Document> AddOrderByAsync(Document document, InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return document;

        var sourceExpression = memberAccess.Expression;
        var sourceType = semanticModel?.GetTypeInfo(sourceExpression).Type;

        // Determine Entity Type T from IQueryable<T>
        ITypeSymbol? entityType = null;
        if (sourceType is INamedTypeSymbol namedType)
        {
            if (namedType.TypeArguments.Length > 0)
                entityType = namedType.TypeArguments[0];
        }

        // Find Primary Key or default to "Id"
        var keyName = "Id";
        if (entityType != null)
        {
            var foundKey = entityType.TryFindPrimaryKey();
            if (foundKey != null) keyName = foundKey;
        }

        // Generate: .OrderBy(x => x.Id)
        var generator = editor.Generator;
        
        // Lambda: x => x.Id
        var lambdaParamName = "x";
        var lambda = generator.ValueReturningLambdaExpression(
            lambdaParamName,
            generator.MemberAccessExpression(generator.IdentifierName(lambdaParamName), keyName)
        );

        // Expression: source.OrderBy(...)
        var orderByInvocation = generator.InvocationExpression(
            generator.MemberAccessExpression(sourceExpression, "OrderBy"),
            lambda
        );

        // Replace the original source expression (e.g. 'db.Users') with 'db.Users.OrderBy(x => x.Id)'
        // But wait, 'sourceExpression' is inside 'invocation'.
        // Example: db.Users.Skip(10)
        // sourceExpression = db.Users
        // invocation = db.Users.Skip(10)
        // We want: db.Users.OrderBy(x => x.Id).Skip(10)
        
        // If we replace sourceExpression, we are modifying the tree correctly.
        editor.ReplaceNode(sourceExpression, orderByInvocation);

        EnsureUsing(editor, "System.Linq");

        return editor.GetChangedDocument();
    }

    private static void EnsureUsing(DocumentEditor editor, string namespaceName)
    {
        var root = editor.OriginalRoot as CompilationUnitSyntax;
        if (root == null) return;
        if (root.Usings.Any(u =>
                u.Name?.ToString() == namespaceName ||
                (u.Alias != null && u.Name?.ToString() == namespaceName)))
            return;

        var newRoot = root.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(namespaceName)));
        editor.ReplaceNode(root, newRoot);
    }
}
