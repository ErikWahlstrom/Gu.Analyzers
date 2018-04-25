namespace Gu.Analyzers
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using Gu.Roslyn.AnalyzerExtensions;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal sealed class AssignedValueWalker : PooledWalker<AssignedValueWalker>, IReadOnlyList<ExpressionSyntax>
    {
        private readonly List<ExpressionSyntax> values = new List<ExpressionSyntax>();
        private readonly List<ExpressionSyntax> outValues = new List<ExpressionSyntax>();
        private readonly MemberWalkers memberWalkers = new MemberWalkers();
        private readonly HashSet<IParameterSymbol> refParameters = new HashSet<IParameterSymbol>(SymbolComparer.Default);
        private readonly HashSet<IParameterSymbol> outParameters = new HashSet<IParameterSymbol>(SymbolComparer.Default);
        private readonly PublicMemberWalker publicMemberWalker;

        private SyntaxNode context;
        private SemanticModel semanticModel;
        private CancellationToken cancellationToken;

        private AssignedValueWalker()
        {
            this.publicMemberWalker = new PublicMemberWalker(this);
        }

        public int Count => this.values.Count;

        internal ISymbol CurrentSymbol { get; private set; }

        public ExpressionSyntax this[int index] => this.values[index];

        public IEnumerator<ExpressionSyntax> GetEnumerator() => this.values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public override void Visit(SyntaxNode node)
        {
            if (this.ShouldVisit(node) == false)
            {
                return;
            }

            base.Visit(node);
        }

        public override void VisitVariableDeclarator(VariableDeclaratorSyntax node)
        {
            this.HandleAssignedValue(node, node.Initializer?.Value);
            base.VisitVariableDeclarator(node);
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (node.Initializer != null)
            {
                var ctor = this.semanticModel.GetSymbolSafe(node.Initializer, this.cancellationToken);
                this.HandleInvoke(ctor, node.Initializer.ArgumentList);
            }
            else
            {
                var ctor = this.semanticModel.GetDeclaredSymbolSafe(node, this.cancellationToken);
                if (Constructor.TryGetDefault(ctor?.ContainingType?.BaseType, out var baseCtor))
                {
                    this.HandleInvoke(baseCtor, null);
                }
            }

            var contextCtor = this.context?.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
            if (contextCtor != null)
            {
                if (contextCtor == node ||
                    node.IsRunBefore(contextCtor, this.semanticModel, this.cancellationToken))
                {
                    base.VisitConstructorDeclaration(node);
                }
            }
            else
            {
                base.VisitConstructorDeclaration(node);
            }
        }

        public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
        {
            this.HandleAssignedValue(node.Left, node.Right);
            base.VisitAssignmentExpression(node);
        }

        public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
        {
            switch (node.Kind())
            {
                case SyntaxKind.LogicalNotExpression:
                    break;
                default:
                    this.HandleAssignedValue(node.Operand, node);
                    break;
            }

            base.VisitPrefixUnaryExpression(node);
        }

        public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
        {
            this.HandleAssignedValue(node.Operand, node);
            base.VisitPostfixUnaryExpression(node);
        }

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (this.semanticModel.GetSymbolSafe(node, this.cancellationToken) is IMethodSymbol method)
            {
                base.VisitInvocationExpression(node);
                if (this.context is ElementAccessExpressionSyntax &&
                    node.Expression is MemberAccessExpressionSyntax memberAccess &&
                    method.Name == "Add" &&
                    SymbolComparer.Equals(this.CurrentSymbol, this.semanticModel.GetSymbolSafe(memberAccess.Expression, this.cancellationToken)))
                {
                    if (method.ContainingType.Is(KnownSymbol.IDictionary) &&
                        node.ArgumentList?.Arguments.Count == 2)
                    {
                        this.values.Add(node.ArgumentList.Arguments[1].Expression);
                    }
                    else if (node.ArgumentList?.Arguments.Count == 1)
                    {
                        this.values.Add(node.ArgumentList.Arguments[0].Expression);
                    }
                }

                this.HandleInvoke(method, node.ArgumentList);
            }
        }

        public override void VisitElementAccessExpression(ElementAccessExpressionSyntax node)
        {
            if (node.Parent is AssignmentExpressionSyntax assignment &&
                this.context is ElementAccessExpressionSyntax &&
                SymbolComparer.Equals(this.CurrentSymbol, this.semanticModel.GetSymbolSafe(node.Expression, this.cancellationToken)))
            {
                this.values.Add(assignment.Right);
                base.VisitElementAccessExpression(node);
            }
        }

        public override void VisitIsPatternExpression(IsPatternExpressionSyntax node)
        {
            this.HandleAssignedValue(node.Pattern, node.Expression);
            base.VisitIsPatternExpression(node);
        }

        public override void VisitCasePatternSwitchLabel(CasePatternSwitchLabelSyntax node)
        {
            if (node.Parent is SwitchSectionSyntax switchSection &&
                switchSection.Parent is SwitchStatementSyntax switchStatement)
            {
                this.HandleAssignedValue(node.Pattern, switchStatement.Expression);
            }

            base.VisitCasePatternSwitchLabel(node);
        }

        internal static AssignedValueWalker Borrow(IPropertySymbol property, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            return Borrow(property, null, semanticModel, cancellationToken);
        }

        internal static AssignedValueWalker Borrow(IFieldSymbol field, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            return Borrow(field, null, semanticModel, cancellationToken);
        }

        internal static AssignedValueWalker Borrow(ExpressionSyntax value, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (value is ElementAccessExpressionSyntax elementAccess)
            {
                return Borrow(semanticModel.GetSymbolSafe(elementAccess.Expression, cancellationToken), elementAccess, semanticModel, cancellationToken);
            }

            var symbol = semanticModel.GetSymbolSafe(value, cancellationToken);
            if (symbol is IFieldSymbol ||
                symbol is IPropertySymbol ||
                symbol is ILocalSymbol ||
                symbol is IParameterSymbol)
            {
                return Borrow(symbol, value, semanticModel, cancellationToken);
            }

            return Borrow(() => new AssignedValueWalker());
        }

        internal static AssignedValueWalker Borrow(ISymbol symbol, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (symbol is IFieldSymbol ||
                symbol is IPropertySymbol ||
                symbol is ILocalSymbol ||
                symbol is IParameterSymbol)
            {
                return Borrow(symbol, null, semanticModel, cancellationToken);
            }

            return Borrow(() => new AssignedValueWalker());
        }

        internal static AssignedValueWalker Borrow(ISymbol symbol, SyntaxNode context, SemanticModel semanticModel, CancellationToken cancellationToken)
        {
            if (symbol == null)
            {
                return Borrow(() => new AssignedValueWalker());
            }

            var pooled = Borrow(() => new AssignedValueWalker());
            pooled.CurrentSymbol = symbol;
            pooled.context = context;
            pooled.semanticModel = semanticModel;
            pooled.cancellationToken = cancellationToken;
            if (context != null)
            {
                pooled.Run();
            }
            else
            {
                foreach (var reference in symbol.DeclaringSyntaxReferences)
                {
                    pooled.context = symbol is IFieldSymbol || symbol is IPropertySymbol
                                              ? reference.GetSyntax(cancellationToken)
                                                         .FirstAncestor<TypeDeclarationSyntax>()
                                              : reference.GetSyntax(cancellationToken)
                                                         .FirstAncestor<MemberDeclarationSyntax>();
                    pooled.Run();
                }
            }

            pooled.values.PurgeDuplicates();
            return pooled;
        }

        internal void HandleInvoke(IMethodSymbol method, ArgumentListSyntax argumentList)
        {
            if (method != null &&
                (method.Parameters.TryFirst(x => x.RefKind != RefKind.None, out _) ||
                 this.CurrentSymbol.ContainingType.Is(method.ContainingType)))
            {
                if (TryGetWalker(out var walker))
                {
                    foreach (var value in walker.values)
                    {
                        this.values.Add(GetArgumentValue(value));
                    }

                    foreach (var outValue in walker.outValues)
                    {
                        this.values.Add(GetArgumentValue(outValue));
                    }
                }
            }

            bool TryGetWalker(out AssignedValueWalker result)
            {
                result = null;
                if (TryGetKey(out var key))
                {
                    if (this.memberWalkers.TryGetValue(key, out result))
                    {
                        return false;
                    }

                    if (method.TrySingleDeclaration(this.cancellationToken, out var declaration))
                    {
                        result = Borrow(() => new AssignedValueWalker());
                        this.memberWalkers.Add(key, result);
                        result.CurrentSymbol = this.CurrentSymbol;
                        result.semanticModel = this.semanticModel;
                        result.cancellationToken = this.cancellationToken;
                        result.context = this.context.FirstAncestorOrSelf<ConstructorDeclarationSyntax>() != null ? this.context : declaration;
                        result.memberWalkers.Parent = this.memberWalkers;
                        if (argumentList != null)
                        {
                            foreach (var argument in argumentList.Arguments)
                            {
                                if (argument.RefOrOutKeyword.IsKind(SyntaxKind.RefKeyword) &&
                                    TryGetMatchingParameter(argument, out var parameter))
                                {
                                    result.refParameters.Add(parameter);
                                }
                                else if (argument.RefOrOutKeyword.IsKind(SyntaxKind.OutKeyword) &&
                                         TryGetMatchingParameter(argument, out parameter))
                                {
                                    result.outParameters.Add(parameter);
                                }
                            }
                        }

                        result.Visit(declaration);
                    }
                }

                return result != null;

                bool TryGetMatchingParameter(ArgumentSyntax argument, out IParameterSymbol parameter)
                {
                    parameter = null;
                    if (this.semanticModel.GetSymbolSafe(argument.Expression, this.cancellationToken) is ISymbol symbol)
                    {
                        if (SymbolComparer.Equals(this.CurrentSymbol, symbol) ||
                            this.refParameters.Contains(symbol as IParameterSymbol) ||
                            this.outParameters.Contains(symbol as IParameterSymbol))
                        {
                            return method.TryGetMatchingParameter(argument, out parameter);
                        }
                    }

                    return false;
                }

                bool TryGetKey(out SyntaxNode node)
                {
                    if (argumentList != null)
                    {
                        node = argumentList;
                        return true;
                    }

                    return method.TrySingleDeclaration(this.cancellationToken, out node);
                }
            }

            ExpressionSyntax GetArgumentValue(ExpressionSyntax value)
            {
                if (value is IdentifierNameSyntax identifierName &&
                    method.Parameters.TryFirst(x => x.Name == identifierName.Identifier.ValueText, out var parameter))
                {
                    if (argumentList.TryGetMatchingArgument(parameter, out var argument))
                    {
                        return argument.Expression;
                    }

                    if (parameter.HasExplicitDefaultValue &&
                        parameter.TrySingleDeclaration(this.cancellationToken, out var parameterDeclaration))
                    {
                        return parameterDeclaration.Default?.Value;
                    }
                }

                return value;
            }
        }

        protected override void Clear()
        {
            this.values.Clear();
            this.outValues.Clear();
            this.refParameters.Clear();
            this.outParameters.Clear();
            this.CurrentSymbol = null;
            this.context = null;
            this.semanticModel = null;
            this.cancellationToken = CancellationToken.None;
            this.memberWalkers.Clear();
        }

        private void Run()
        {
            if (this.CurrentSymbol == null)
            {
                return;
            }

            if (this.CurrentSymbol is ILocalSymbol local &&
                local.TrySingleDeclaration(this.cancellationToken, out var declaration))
            {
                var scope = (SyntaxNode)declaration.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() ??
                                        declaration.FirstAncestorOrSelf<MemberDeclarationSyntax>();
                if (scope != null)
                {
                    this.Visit(scope);
                }

                return;
            }

            if (this.CurrentSymbol is IParameterSymbol)
            {
                var scope = (SyntaxNode)this.context?.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() ??
                                        this.context?.FirstAncestorOrSelf<MemberDeclarationSyntax>();
                if (scope != null)
                {
                    this.Visit(scope);
                }

                return;
            }

            if (this.CurrentSymbol is IFieldSymbol ||
                this.CurrentSymbol is IPropertySymbol)
            {
                var type = (INamedTypeSymbol)this.semanticModel.GetDeclaredSymbolSafe(this.context?.FirstAncestorOrSelf<TypeDeclarationSyntax>(), this.cancellationToken);
                if (type == null)
                {
                    return;
                }

                if (this.CurrentSymbol is IFieldSymbol &&
                    this.CurrentSymbol.TrySingleDeclaration(this.cancellationToken, out FieldDeclarationSyntax fieldDeclarationSyntax))
                {
                    this.Visit(fieldDeclarationSyntax);
                }
                else if (this.CurrentSymbol is IPropertySymbol &&
                    this.CurrentSymbol.TrySingleDeclaration(this.cancellationToken, out PropertyDeclarationSyntax propertyDeclaration) &&
                    propertyDeclaration.Initializer != null)
                {
                    this.values.Add(propertyDeclaration.Initializer.Value);
                }

                var contextCtor = this.context?.FirstAncestorOrSelf<ConstructorDeclarationSyntax>();
                foreach (var reference in type.DeclaringSyntaxReferences)
                {
                    using (var ctorWalker = ConstructorsWalker.Borrow((TypeDeclarationSyntax)reference.GetSyntax(this.cancellationToken), this.semanticModel, this.cancellationToken))
                    {
                        foreach (var creation in ctorWalker.ObjectCreations)
                        {
                            if (contextCtor == null ||
                                creation.Creates(contextCtor, Search.Recursive, this.semanticModel, this.cancellationToken))
                            {
                                this.VisitObjectCreationExpression(creation);
                                var method = this.semanticModel.GetSymbolSafe(creation, this.cancellationToken);
                                this.HandleInvoke(method, creation.ArgumentList);
                            }
                        }

                        if (contextCtor != null)
                        {
                            foreach (var initializer in ctorWalker.Initializers)
                            {
                                var other = (ConstructorDeclarationSyntax)initializer.Parent;
                                if (contextCtor.IsRunBefore(other, this.semanticModel, this.cancellationToken))
                                {
                                    this.Visit(other);
                                }
                            }

                            if (!contextCtor.Modifiers.Any(SyntaxKind.PrivateKeyword))
                            {
                                this.Visit(contextCtor);
                            }

                            return;
                        }

                        if (ctorWalker.Default != null)
                        {
                            this.Visit(ctorWalker.Default);
                        }

                        foreach (var ctor in ctorWalker.NonPrivateCtors)
                        {
                            this.Visit(ctor);
                        }
                    }
                }

                if ((this.CurrentSymbol is IFieldSymbol field &&
                     !field.IsReadOnly) ||
                    (this.CurrentSymbol is IPropertySymbol property &&
                     !property.IsReadOnly))
                {
                    var scope = (SyntaxNode)this.context?.FirstAncestorOrSelf<AnonymousFunctionExpressionSyntax>() ??
                                this.context?.FirstAncestorOrSelf<MemberDeclarationSyntax>();
                    if (scope != null &&
                        !(scope is ConstructorDeclarationSyntax))
                    {
                        while (type.Is(this.CurrentSymbol.ContainingType))
                        {
                            foreach (var reference in type.DeclaringSyntaxReferences)
                            {
                                var typeDeclaration = (TypeDeclarationSyntax)reference.GetSyntax(this.cancellationToken);
                                this.publicMemberWalker.Visit(typeDeclaration);
                            }

                            type = type.BaseType;
                        }
                    }
                }
            }
        }

        private void HandleAssignedValue(SyntaxNode assigned, ExpressionSyntax value)
        {
            if (value == null)
            {
                return;
            }

            if (assigned is VariableDeclaratorSyntax declarator &&
                declarator.Identifier.ValueText != this.CurrentSymbol.Name)
            {
                return;
            }

            if (this.CurrentSymbol.IsEither<ILocalSymbol, IParameterSymbol>() &&
                assigned is DeclarationPatternSyntax declarationPattern &&
                declarationPattern.Designation is SingleVariableDesignationSyntax singleVariableDesignation &&
                singleVariableDesignation.Identifier.ValueText == this.CurrentSymbol.Name)
            {
                this.values.Add(value);
                return;
            }

            if (this.CurrentSymbol.IsEither<ILocalSymbol, IParameterSymbol>() &&
                assigned is MemberAccessExpressionSyntax)
            {
                return;
            }

            if (this.context is ElementAccessExpressionSyntax)
            {
                switch (value)
                {
                    case ArrayCreationExpressionSyntax arrayCreation:
                        {
                            if (arrayCreation.Initializer == null)
                            {
                                return;
                            }

                            foreach (var item in arrayCreation.Initializer.Expressions)
                            {
                                this.values.Add(item);
                            }
                        }

                        break;

                    case ObjectCreationExpressionSyntax objectCreation:
                        {
                            if (objectCreation.Initializer == null)
                            {
                                return;
                            }

                            foreach (var item in objectCreation.Initializer.Expressions)
                            {
                                if (item is InitializerExpressionSyntax kvp)
                                {
                                    if (kvp.Expressions.Count == 2)
                                    {
                                        this.values.Add(kvp.Expressions[1]);
                                    }
                                }
                                else if (item is AssignmentExpressionSyntax assignment)
                                {
                                    this.values.Add(assignment.Right);
                                }
                                else
                                {
                                    this.values.Add(item);
                                }
                            }
                        }

                        break;

                    case InitializerExpressionSyntax initializer:
                        {
                            foreach (var item in initializer.Expressions)
                            {
                                this.values.Add(item);
                            }
                        }

                        break;
                    default:
                        return;
                }

                return;
            }

            if (TryGetSetterWalker(out var setterWalker))
            {
                foreach (var nested in setterWalker.values)
                {
                    if (nested is IdentifierNameSyntax identifierName &&
                        identifierName.Identifier.ValueText == "value")
                    {
                        this.values.Add(value);
                    }
                    else
                    {
                        this.values.Add(nested);
                    }
                }

                return;
            }

            var assignedSymbol = this.semanticModel.GetSymbolSafe(assigned, this.cancellationToken) ??
                                 this.semanticModel.GetDeclaredSymbolSafe(assigned, this.cancellationToken);
            if (assignedSymbol == null)
            {
                return;
            }

            if (SymbolComparer.Equals(this.CurrentSymbol, assignedSymbol) ||
                this.refParameters.Contains(assignedSymbol as IParameterSymbol))
            {
                this.values.Add(value);
            }

            if (this.outParameters.Contains(assignedSymbol as IParameterSymbol))
            {
                this.outValues.Add(value);
            }

            bool TryGetSetterWalker(out AssignedValueWalker walker)
            {
                walker = null;
                if (!this.CurrentSymbol.IsEither<IFieldSymbol, IPropertySymbol>())
                {
                    return false;
                }

                if (TryGetProperty(out var property) &&
                    !SymbolComparer.Equals(this.CurrentSymbol, property) &&
                    property.ContainingType.Is(this.CurrentSymbol.ContainingType))
                {
                    if (this.memberWalkers.TryGetValue(value, out walker))
                    {
                        return false;
                    }

                    if (property.TrySingleDeclaration(this.cancellationToken, out var declaration) &&
                        declaration.TryGetSetter(out var setter))
                    {
                        walker = Borrow(() => new AssignedValueWalker());
                        this.memberWalkers.Add(value, walker);
                        walker.CurrentSymbol = this.CurrentSymbol;
                        walker.semanticModel = this.semanticModel;
                        walker.cancellationToken = this.cancellationToken;
                        walker.context = setter;
                        walker.memberWalkers.Parent = this.memberWalkers;
                        walker.Visit(setter);
                    }
                }

                return walker != null;

                bool TryGetProperty(out IPropertySymbol result)
                {
                    if (assigned is IdentifierNameSyntax identifierName &&
                        this.CurrentSymbol.ContainingType.TryFindPropertyRecursive(identifierName.Identifier.ValueText, out result))
                    {
                        return true;
                    }

                    if (assigned is MemberAccessExpressionSyntax memberAccess &&
                        memberAccess.Expression is InstanceExpressionSyntax &&
                        this.CurrentSymbol.ContainingType.TryFindPropertyRecursive(memberAccess.Name.Identifier.ValueText, out result))
                    {
                        return true;
                    }

                    result = null;
                    return false;
                }
            }
        }

        private bool? ShouldVisit(SyntaxNode node)
        {
            if (this.CurrentSymbol.IsEither<IFieldSymbol, IPropertySymbol>())
            {
                if (node is ExpressionStatementSyntax &&
                    this.context.SharesAncestor<ConstructorDeclarationSyntax>(node) &&
                    node.FirstAncestor<AnonymousFunctionExpressionSyntax>() == null)
                {
                    return node.IsExecutedBefore(this.context);
                }

                return true;
            }

            if (this.CurrentSymbol.IsEither<ILocalSymbol, IParameterSymbol>())
            {
                if (node.FirstAncestor<AnonymousFunctionExpressionSyntax>() is AnonymousFunctionExpressionSyntax lambda)
                {
                    if (this.CurrentSymbol is ILocalSymbol local &&
                        local.TrySingleDeclaration(this.cancellationToken, out var declaration) &&
                        lambda.Contains(declaration))
                    {
                        return node.IsExecutedBefore(this.context);
                    }

                    return true;
                }

                return node.IsExecutedBefore(this.context);
            }

            if (this.context is InvocationExpressionSyntax &&
                ReferenceEquals(node, this.context))
            {
                return false;
            }

            return true;
        }

        private class PublicMemberWalker : CSharpSyntaxWalker
        {
            private readonly AssignedValueWalker inner;

            public PublicMemberWalker(AssignedValueWalker inner)
            {
                this.inner = inner;
            }

            public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                // Don't walk ctor.
            }

            public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
            {
                if (!node.Modifiers.Any(SyntaxKind.PrivateKeyword))
                {
                    base.VisitPropertyDeclaration(node);
                }
            }

            public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
            {
                if (!node.Modifiers.Any(SyntaxKind.PrivateKeyword))
                {
                    this.inner.VisitMethodDeclaration(node);
                }
            }

            public override void VisitAccessorDeclaration(AccessorDeclarationSyntax node)
            {
                if (!node.Modifiers.Any(SyntaxKind.PrivateKeyword))
                {
                    this.inner.VisitAccessorDeclaration(node);
                }
            }

            public override void VisitArrowExpressionClause(ArrowExpressionClauseSyntax node)
            {
                this.inner.VisitArrowExpressionClause(node);
            }
        }

        private class MemberWalkers
        {
            private readonly Dictionary<SyntaxNode, AssignedValueWalker> map = new Dictionary<SyntaxNode, AssignedValueWalker>();

            public MemberWalkers Parent { get; set; }

            private Dictionary<SyntaxNode, AssignedValueWalker> Current => this.Parent?.Current ??
                                                                        this.map;

            public void Add(SyntaxNode location, AssignedValueWalker walker)
            {
                this.Current.Add(location, walker);
            }

            public bool TryGetValue(SyntaxNode location, out AssignedValueWalker walker)
            {
                return this.Current.TryGetValue(location, out walker);
            }

            public void Clear()
            {
                if (this.map != null)
                {
                    foreach (var walker in this.map.Values)
                    {
                        ((IDisposable)walker)?.Dispose();
                    }

                    this.map.Clear();
                }

                this.Parent = null;
            }
        }
    }
}
