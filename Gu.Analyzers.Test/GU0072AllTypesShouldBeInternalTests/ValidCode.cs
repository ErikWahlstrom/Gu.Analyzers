namespace Gu.Analyzers.Test.GU0072AllTypesShouldBeInternalTests
{
    using Gu.Roslyn.Asserts;
    using NUnit.Framework;

    internal class ValidCode
    {
        private static readonly GU0072AllTypesShouldBeInternal Analyzer = new GU0072AllTypesShouldBeInternal();

        [TestCase("internal class Foo")]
        [TestCase("internal struct Foo")]
        public void SimpleType(string signature)
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.Collections.Generic;

    internal class Foo
    {
    }
}".AssertReplace("internal class Foo", signature);

            AnalyzerAssert.Valid(Analyzer, testCode);
        }

        [TestCase("internal class Bar")]
        [TestCase("protected class Bar")]
        [TestCase("private class Bar")]
        [TestCase("internal struct Bar")]
        [TestCase("protected struct Bar")]
        [TestCase("private struct Bar")]
        public void NestedType(string signature)
        {
            var testCode = @"
namespace RoslynSandbox
{
    using System.Collections.Generic;

    internal class Foo
    {
        private class Bar
        {
        }
    }
}".AssertReplace("private class Bar", signature);

            AnalyzerAssert.Valid(Analyzer, testCode);
        }
    }
}
