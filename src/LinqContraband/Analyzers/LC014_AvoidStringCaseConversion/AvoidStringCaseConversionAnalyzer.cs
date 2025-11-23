using System.Collections.Generic;
using System.Collections.Immutable;
using LinqContraband.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace LinqContraband.Analyzers.LC014_AvoidStringCaseConversion;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class AvoidStringCaseConversionAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "LC014";
    private const string Category = "Performance";
    private static readonly LocalizableString Title = "Avoid String.ToLower() or ToUpper() in LINQ queries";

    private static readonly LocalizableString MessageFormat =
        "Using '{0}' in a LINQ query prevents index usage. Use 'string.Equals' with 'StringComparison' or 'EF.Functions.Collate' instead.";

    private static readonly LocalizableString Description =
        "Using ToLower() or ToUpper() in a LINQ query predicate forces a full table scan (non-sargable) because it transforms the column value before comparison. Indexes cannot be used.";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Warning,
        true,
        Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    private static readonly HashSet<string> CaseConversionMethods = new()
    {
        "ToLower",
        "ToLowerInvariant",
        "ToUpper",
        "ToUpperInvariant"
    };

    private static readonly HashSet<string> TargetLinqMethods = new()
    {
        "Where",
        "OrderBy", "OrderByDescending",
        "ThenBy", "ThenByDescending",
        "Count", "LongCount",
        "Any", "All",
        "First", "FirstOrDefault",
        "Single", "SingleOrDefault",
        "Last", "LastOrDefault",
        "Join", "GroupJoin"
    };

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        // 1. Check if it is ToLower/ToUpper
        if (!CaseConversionMethods.Contains(method.Name)) return;
        
        // Check if it belongs to System.String
        if (method.ContainingType.SpecialType != SpecialType.System_String) return;

        // 2. Check if we are inside an IQueryable LINQ method
        if (!IsInsideQueryableLambda(invocation)) return;

        // 3. Check if the receiver depends on a parameter (DB column)
        if (!ReceiverDependsOnParameter(invocation.Instance)) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), method.Name));
    }

    private bool IsInsideQueryableLambda(IOperation operation)
    {
        var current = operation.Parent;
        while (current != null)
        {
            if (current is IArgumentOperation argument)
            {
                // We found an argument. Check the invocation that owns it.
                if (argument.Parent is IInvocationOperation linqInvocation)
                {
                    var method = linqInvocation.TargetMethod;
                    // Must be one of the target LINQ methods (Where, etc.)
                    if (TargetLinqMethods.Contains(method.Name))
                    {
                         // Must be on IQueryable
                         if (method.ContainingType.Name == "Queryable" &&
                             method.ContainingNamespace?.ToString() == "System.Linq")
                         {
                             return true;
                         }
                    }
                    // If not a target LINQ method, we continue walking up!
                    // We don't return false immediately, because this argument might be passed 
                    // into a helper method inside the LINQ lambda.
                }
            }
            current = current.Parent;
        }
        return false;
    }

    private bool ReceiverDependsOnParameter(IOperation? operation)
    {
        if (operation == null) return false;

        // Unwrap conversions
        operation = operation.UnwrapConversions();

        // If it's a parameter reference, it's the entity itself (e.g. x => x.ToLower() - unlikely for string but possible)
        if (operation is IParameterReferenceOperation) return true;

        // If it's a property reference, check the instance of the property
        if (operation is IPropertyReferenceOperation propRef)
        {
            return ReceiverDependsOnParameter(propRef.Instance);
        }

        // If it's a method call (chained), check the instance
        // e.g. x.Address.City.Trim().ToLower()
        if (operation is IInvocationOperation invocation)
        {
             // Check instance
             if (ReceiverDependsOnParameter(invocation.Instance)) return true;
        }
        
        // If it's an array/indexer access?
        // e.g. x.Tags[0].ToLower()
        if (operation is IPropertyReferenceOperation indexer && indexer.Arguments.Length > 0)
        {
             return ReceiverDependsOnParameter(indexer.Instance);
        }

        // Binary Operator (e.g. (x.Name + "suffix").ToLower())
        if (operation is IBinaryOperation binaryOp)
        {
            return ReceiverDependsOnParameter(binaryOp.LeftOperand) || ReceiverDependsOnParameter(binaryOp.RightOperand);
        }
        
        // Coalesce Operator (e.g. (x.Name ?? "").ToLower())
        if (operation is ICoalesceOperation coalesce)
        {
             return ReceiverDependsOnParameter(coalesce.Value) || ReceiverDependsOnParameter(coalesce.WhenNull);
        }
        
        if (operation.Kind == OperationKind.ConditionalAccess) // IConditionalAccessOperation
        {
            var conditional = (IConditionalAccessOperation)operation;
            return ReceiverDependsOnParameter(conditional.Operation);
        }

        // Also handle if we are looking at the 'WhenTrue' part of a conditional access?
        // When we call ToLower() on 'x?.Name?.ToLower()', the instance of ToLower is actually the Result of the previous chain?
        // No, if we have `x?.ToLower()`, ToLower is the Operation of the ConditionalAccess? No.
        // `x?.ToLower()` parses as ConditionalAccess.
        // Operation: x
        // WhenTrue: Invocation(ToLower) on Instance: ConditionalAccessInstance
        
        // BUT, my visitor visits IInvocationOperation. 
        // If I write `x?.ToLower()`, the IInvocationOperation is inside the ConditionalAccess.
        // Its Instance is IConditionalAccessInstanceOperation.
        
        if (operation.Kind == OperationKind.ConditionalAccessInstance)
        {
            // We need to find the PARENT ConditionalAccessOperation to see what it is operating on.
            // BUT, operation.Parent might not be the ConditionalAccessOperation directly?
            // The IConditionalAccessInstanceOperation is a leaf that refers to the value being checked.
            // To find the source, we must walk up the parents of the *current* invocation until we find the ConditionalAccessOperation?
            // No, the IConditionalAccessInstanceOperation itself doesn't link back easily without walking parents of the usage?
            
            // Actually, if I am verifying the Instance of ToLower, and it is IConditionalAccessInstance,
            // it means ToLower is being called conditionally.
            // So I should look at the Parent of the ToLower Invocation.
            // The Parent of ToLower Invocation should be the IConditionalAccessOperation (or a conversion to it).
            
            // Wait, I don't have the 'parent' easily here inside ReceiverDependsOnParameter recursion unless I pass it or look at operation.Parent?
            // But 'operation' here IS the Instance. Its parent is the Invocation (ToLower).
            // The Invocation's Parent is likely the ConditionalAccessOperation.
            
            // Let's try:
            var parent = operation.Parent; // This is the Invocation (ToLower)
            var grandParent = parent?.Parent; // This should be ConditionalAccessOperation
            
            if (grandParent is IConditionalAccessOperation caOp)
            {
                return ReceiverDependsOnParameter(caOp.Operation);
            }
        }

        return false;
    }
}
