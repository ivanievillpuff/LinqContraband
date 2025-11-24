using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Microsoft.CodeAnalysis.Formatting;

namespace LinqContraband.Analyzers.LC008_SyncBlocker;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SyncBlockerFixer))]
[Shared]
public class SyncBlockerFixer : CodeFixProvider
{
    private static readonly Dictionary<string, string> SyncToAsyncMap = new()
    {
        { "ToList", "ToListAsync" },
        { "ToArray", "ToArrayAsync" },
        { "ToDictionary", "ToDictionaryAsync" },
        { "ToHashSet", "ToHashSetAsync" },
        { "First", "FirstAsync" },
        { "FirstOrDefault", "FirstOrDefaultAsync" },
        { "Single", "SingleAsync" },
        { "SingleOrDefault", "SingleOrDefaultAsync" },
        { "Last", "LastAsync" },
        { "LastOrDefault", "LastOrDefaultAsync" },
        { "Count", "CountAsync" },
        { "LongCount", "LongCountAsync" },
        { "Any", "AnyAsync" },
        { "All", "AllAsync" },
        { "Min", "MinAsync" },
        { "Max", "MaxAsync" },
        { "Sum", "SumAsync" },
        { "Average", "AverageAsync" },
        { "SaveChanges", "SaveChangesAsync" },
        { "Find", "FindAsync" }
    };

    public sealed override ImmutableArray<string> FixableDiagnosticIds =>
        ImmutableArray.Create(SyncBlockerAnalyzer.DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var invocation = root?.FindNode(diagnosticSpan) as InvocationExpressionSyntax;
        if (invocation == null) return;

        // We need the method name to find the replacement
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            var methodName = memberAccess.Name.Identifier.Text;
            if (SyncToAsyncMap.TryGetValue(methodName, out var asyncMethodName))
                context.RegisterCodeFix(
                    CodeAction.Create(
                        $"Use {asyncMethodName} and await",
                        c => ApplyFixAsync(context.Document, invocation, asyncMethodName, c),
                        "UseAsyncMethod"),
                    diagnostic);
        }
    }

    private async Task<Document> ApplyFixAsync(Document document, InvocationExpressionSyntax invocation,
        string asyncMethodName, CancellationToken cancellationToken)
    {
        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess) return document;

        // 1. Replace method name (e.g. ToList -> ToListAsync)
        var newMemberAccess = memberAccess.WithName(
            SyntaxFactory.IdentifierName(asyncMethodName));

        var newInvocation = invocation.WithExpression(newMemberAccess);

        // 2. Add 'await' expression
        var awaitExpression = SyntaxFactory.AwaitExpression(newInvocation)
            .WithLeadingTrivia(invocation.GetLeadingTrivia())
            .WithTrailingTrivia(invocation.GetTrailingTrivia());

        // We need to clear trivia from the inner invocation to avoid duplication/messy formatting
        newInvocation = newInvocation.WithoutLeadingTrivia().WithoutTrailingTrivia();

        // Re-attach to await
        awaitExpression = awaitExpression.WithExpression(newInvocation);

        // 3. Add Formatting annotation
        var formattedAwait = awaitExpression.WithAdditionalAnnotations(Formatter.Annotation);

        editor.ReplaceNode(invocation, formattedAwait);

        return editor.GetChangedDocument();
    }
}
