// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global
namespace Gu.Analyzers
{
    using Gu.Roslyn.AnalyzerExtensions;

    internal static class KnownSymbol
    {
        internal static readonly QualifiedType Void = Create("System.Void");
        internal static readonly QualifiedType Object = Create("System.Object", "object");
        internal static readonly QualifiedType Boolean = Create("System.Boolean", "bool");
        internal static readonly QualifiedType Int32 = Create("System.Int32", "int");
        internal static readonly QualifiedType Int64 = Create("System.Int64", "long");
        internal static readonly StringType String = new StringType();
        internal static readonly QualifiedType NullableOfT = new QualifiedType("System.Nullable`1");
        internal static readonly QualifiedType Array = Create("System.Array");
        internal static readonly QualifiedType Tuple = Create("System.Tuple");
        internal static readonly QualifiedType Func = Create("System.Func");
        internal static readonly QualifiedType Type = Create("System.Type");
        internal static readonly QualifiedType Guid = Create("System.Guid");
        internal static readonly QualifiedType Exception = Create("System.Exception");
        internal static readonly QualifiedType DateTime = Create("System.DateTime");
        internal static readonly QualifiedType SerializableAttribute = Create("System.SerializableAttribute");
        internal static readonly QualifiedType NonSerializedAttribute = Create("System.NonSerializedAttribute");
        internal static readonly DisposableType IDisposable = new DisposableType();
        internal static readonly QualifiedType ArgumentException = Create("System.ArgumentException");
        internal static readonly QualifiedType ArgumentNullException = Create("System.ArgumentNullException");
        internal static readonly QualifiedType ArgumentOutOfRangeException = Create("System.ArgumentOutOfRangeException");
        internal static readonly QualifiedType EventHandler = Create("System.EventHandler");

        internal static readonly QualifiedType IDictionary = Create("System.Collections.IDictionary");

        internal static readonly QualifiedType ListOfT = Create("System.Collections.Generic.List`1");
        internal static readonly QualifiedType StackOfT = Create("System.Collections.Generic.Stack`1");
        internal static readonly QualifiedType QueueOfT = Create("System.Collections.Generic.Queue`1");
        internal static readonly QualifiedType LinkedListOfT = Create("System.Collections.Generic.LinkedList`1");
        internal static readonly QualifiedType SortedSetOfT = Create("System.Collections.Generic.SortedSet`1");

        internal static readonly QualifiedType DictionaryOfTKeyTValue = Create("System.Collections.Generic.Dictionary`2");
        internal static readonly QualifiedType SortedListOfTKeyTValue = Create("System.Collections.Generic.SortedList`2");
        internal static readonly QualifiedType SortedDictionaryOfTKeyTValue = Create("System.Collections.Generic.SortedDictionary`2");

        internal static readonly QualifiedType ImmutableHashSetOfT = Create("System.Collections.Immutable.ImmutableHashSet`1");
        internal static readonly QualifiedType ImmutableListOfT = Create("System.Collections.Immutable.ImmutableList`1");
        internal static readonly QualifiedType ImmutableQueueOfT = Create("System.Collections.Immutable.ImmutableQueue`1");
        internal static readonly QualifiedType ImmutableSortedSetOfT = Create("System.Collections.Immutable.ImmutableSortedSet`1");
        internal static readonly QualifiedType ImmutableStackOfT = Create("System.Collections.Immutable.ImmutableStack`1");

        internal static readonly QualifiedType ImmutableDictionaryOfTKeyTValue = Create("System.Collections.Immutable.ImmutableDictionary`2");
        internal static readonly QualifiedType ImmutableSortedDictionaryOfTKeyTValue = Create("System.Collections.Immutable.ImmutableSortedDictionary`2");

        internal static readonly QualifiedType FlagsAttribute = Create("System.FlagsAttribute");

        internal static readonly StringBuilderType StringBuilder = new StringBuilderType();

        internal static readonly TextReaderType TextReader = new TextReaderType();
        internal static readonly QualifiedType StreamReader = new QualifiedType("System.IO.StreamReader");
        internal static readonly FileType File = new FileType();
        internal static readonly IEnumerableType IEnumerable = new IEnumerableType();
        internal static readonly IEnumerableOfTType IEnumerableOfT = new IEnumerableOfTType();
        internal static readonly QualifiedType IEnumeratorOfT = new QualifiedType("System.Collections.Generic.IEnumerator`1");
        internal static readonly QualifiedType IEnumerator = new QualifiedType("System.Collections.IEnumerator");
        internal static readonly IListType IList = new IListType();
        internal static readonly EnumerableType Enumerable = new EnumerableType();
        internal static readonly QualifiedType Expression = Create("System.Linq.Expressions.Expression");
        internal static readonly QualifiedType ConditionalWeakTable = Create("System.Runtime.CompilerServices.ConditionalWeakTable`2");
        internal static readonly TaskType Task = new TaskType();
        internal static readonly QualifiedType TaskOfT = new QualifiedType("System.Threading.Tasks.Task`1");
        internal static readonly XmlSerializerType XmlSerializer = new XmlSerializerType();

        internal static readonly SerialDisposableType SerialDisposable = new SerialDisposableType();
        internal static readonly RxDisposableType RxDisposable = new RxDisposableType();
        internal static readonly SingleAssignmentDisposableType SingleAssignmentDisposable = new SingleAssignmentDisposableType();
        internal static readonly QualifiedType CompositeDisposable = new QualifiedType("System.Reactive.Disposables.CompositeDisposable");

        internal static readonly DependencyPropertyType DependencyProperty = new DependencyPropertyType();
        internal static readonly PasswordBoxType PasswordBox = new PasswordBoxType();
        internal static readonly DependencyObjectType DependencyObject = new DependencyObjectType();
        internal static readonly QualifiedType DependencyPropertyChangedEventArgs = Create("System.Windows.DependencyPropertyChangedEventArgs");

        internal static readonly NUnitAssertType NUnitAssert = new NUnitAssertType();
        internal static readonly QualifiedType NUnitTestAttribute = new QualifiedType("NUnit.Framework.TestAttribute");
        internal static readonly QualifiedType NUnitTestCaseAttribute = new QualifiedType("NUnit.Framework.TestCaseAttribute");
        internal static readonly QualifiedType NUnitTestCaseSourceAttribute = new QualifiedType("NUnit.Framework.TestCaseSourceAttribute");
        internal static readonly QualifiedType NUnitSetUpAttribute = new QualifiedType("NUnit.Framework.SetUpAttribute");
        internal static readonly QualifiedType NUnitTearDownAttribute = new QualifiedType("NUnit.Framework.TearDownAttribute");
        internal static readonly QualifiedType NUnitOneTimeSetUpAttribute = new QualifiedType("NUnit.Framework.OneTimeSetUpAttribute");
        internal static readonly QualifiedType NUnitOneTimeTearDownAttribute = new QualifiedType("NUnit.Framework.OneTimeTearDownAttribute");
        internal static readonly XunitAssertType XunitAssert = new XunitAssertType();
        internal static readonly QualifiedType MoqIReturnsResult = new QualifiedType("Moq.Language.Flow.IReturnsResult`1");
        internal static readonly QualifiedType MoqMockOfT = new QualifiedType("Moq.Mock`1");
        internal static readonly QualifiedType MoqIFluentInterface = new QualifiedType("Moq.IFluentInterface");
        internal static readonly QualifiedType NinjectIFluentSyntax = new QualifiedType("Ninject.Syntax.IFluentSyntax");

        private static QualifiedType Create(string qualifiedName, string alias = null)
        {
            return new QualifiedType(qualifiedName, alias);
        }
    }
}
