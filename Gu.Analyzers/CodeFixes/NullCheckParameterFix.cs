namespace Gu.Analyzers
{
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Threading.Tasks;
    using Gu.Roslyn.AnalyzerExtensions;
    using Gu.Roslyn.CodeFixExtensions;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CodeFixes;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.CodeAnalysis.Formatting;

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NullCheckParameterFix))]
    [Shared]
    internal class NullCheckParameterFix : DocumentEditorCodeFixProvider
    {
        /// <inheritdoc/>
        public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(
            GU0012NullCheckParameter.DiagnosticId);

        /// <inheritdoc/>
        protected override async Task RegisterCodeFixesAsync(DocumentEditorCodeFixContext context)
        {
            var syntaxRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken)
                                          .ConfigureAwait(false);
            foreach (var diagnostic in context.Diagnostics)
            {
                var node = syntaxRoot.FindNode(diagnostic.Location.SourceSpan);
                if (node is IdentifierNameSyntax identifierName)
                {
                    context.RegisterCodeFix(
                        "Throw if null.",
                        (editor, _) => editor.ReplaceNode(
                            node,
                            SyntaxFactory.ParseExpression($"{identifierName.Identifier.ValueText} ?? throw new System.ArgumentNullException(nameof({identifierName.Identifier.ValueText}))").WithSimplifiedNames()),
                        this.GetType(),
                        diagnostic);
                }
                else if (node is ParameterSyntax parameter)
                {
                    var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken)
                                                     .ConfigureAwait(false);
                    if (parameter.Parent is ParameterListSyntax parameterList &&
                        parameterList.Parent is BaseMethodDeclarationSyntax methodDeclaration)
                    {
                        using (var walker = AssignmentExecutionWalker.Borrow(methodDeclaration, Scope.Member, semanticModel, context.CancellationToken))
                        {
                            if (TryFirstAssignedWith(parameter, walker.Assignments, out var assignedValue))
                            {
                                context.RegisterCodeFix(
                                    "Throw if null on first assignment.",
                                    (editor, _) => editor.ReplaceNode(
                                        assignedValue,
                                        SyntaxFactory.ParseExpression($"{assignedValue.Identifier.ValueText} ?? throw new System.ArgumentNullException(nameof({assignedValue.Identifier.ValueText}))").WithSimplifiedNames()),
                                    this.GetType(),
                                    diagnostic);
                            }
                            else if (methodDeclaration.Body is BlockSyntax block)
                            {
                                context.RegisterCodeFix(
                                    "Add null check.",
                                    (editor, _) => editor.ReplaceNode(
                                        block,
                                        x => WithNullCheck(x, parameter)),
                                    this.GetType(),
                                    diagnostic);
                            }
                        }
                    }
                }
            }
        }

        private static bool TryFirstAssignedWith(ParameterSyntax parameter, IReadOnlyList<AssignmentExpressionSyntax> assignments, out IdentifierNameSyntax assignedValue)
        {
            foreach (var assignment in assignments)
            {
                if (assignment.Right is IdentifierNameSyntax candidate &&
                    candidate.Identifier.ValueText == parameter.Identifier.ValueText)
                {
                    assignedValue = candidate;
                    return true;
                }
            }

            assignedValue = null;
            return false;
        }

        private static BlockSyntax WithNullCheck(BlockSyntax body, ParameterSyntax parameter)
        {
            var code = StringBuilderPool.Borrow()
                                        .AppendLine($"if ({parameter.Identifier.ValueText} == null)")
                                        .AppendLine("{")
                                        .AppendLine($"    throw new System.ArgumentNullException(nameof({parameter.Identifier.ValueText}));")
                                        .AppendLine("}")
                                        .Return();
            var nullCheck = SyntaxFactory.ParseStatement(code)
                                         .WithSimplifiedNames()
                                         .WithAdditionalAnnotations(Formatter.Annotation)
                                         .WithLeadingElasticLineFeed()
                                         .WithTrailingElasticLineFeed();

            return body.WithStatements(body.Statements.Insert(FindPosition(), nullCheck));

            int FindPosition()
            {
                if (parameter.Parent is ParameterListSyntax parameterList)
                {
                    int ordinal = parameterList.Parameters.IndexOf(parameter);
                    if (ordinal <= 0)
                    {
                        return 0;
                    }

                    var position = 0;
                    for (var i = 0; i < body.Statements.Count; i++)
                    {
                        var statement = body.Statements[i];
                        if (statement is IfStatementSyntax ifStatement &&
                            IsThrow(ifStatement.Statement) &&
                            ifStatement.Condition is BinaryExpressionSyntax condition &&
                            condition.OperatorToken.IsKind(SyntaxKind.EqualsEqualsToken) &&
                            condition.Right is LiteralExpressionSyntax literal &&
                            literal.IsKind(SyntaxKind.NullLiteralExpression) &&
                            condition.Left is IdentifierNameSyntax identifierName &&
                            body.Parent is BaseMethodDeclarationSyntax methodDeclaration &&
                            methodDeclaration.TryFindParameter(identifierName.Identifier.ValueText, out var other) &&
                            parameterList.Parameters.IndexOf(other) < ordinal)
                        {
                            position++;
                        }
                        else
                        {
                            return position;
                        }
                    }

                    return position;
                }

                return 0;
            }
        }

        private static bool IsThrow(StatementSyntax statement)
        {
            if (statement is ThrowStatementSyntax)
            {
                return true;
            }

            return statement is BlockSyntax block &&
                   block.Statements.TrySingle(out var single) &&
                   single is ThrowStatementSyntax;
        }
    }
}
