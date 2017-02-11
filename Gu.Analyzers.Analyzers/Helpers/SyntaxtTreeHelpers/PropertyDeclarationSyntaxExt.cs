namespace Gu.Analyzers
{
    using Microsoft.CodeAnalysis.CSharp.Syntax;

    internal static class PropertyDeclarationSyntaxExt
    {
        internal static bool IsAutoProperty(this PropertyDeclarationSyntax property)
        {
            AccessorDeclarationSyntax getter;
            AccessorDeclarationSyntax setter = null;
            if (property.TryGetGetAccessorDeclaration(out getter) ||
                property.TryGetSetAccessorDeclaration(out setter))
            {
                return getter?.Body == null && setter?.Body == null;
            }

            return false;
        }
    }
}