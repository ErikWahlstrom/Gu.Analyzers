// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedParameter.Local
// ReSharper disable HeuristicUnreachableCode
// ReSharper disable RedundantNameQualifier

#pragma warning disable CS0162 // Unreachable code detected
#pragma warning disable GU0011 // Don't ignore the return value.
namespace Gu.Analyzers.Benchmarks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using BenchmarkDotNet.Reports;
    using BenchmarkDotNet.Running;
    using Gu.Analyzers.Benchmarks.Benchmarks;

    internal class Program
    {
        public static void Main()
        {
            if (false)
            {
                var benchmark = Gu.Roslyn.Asserts.Benchmark.Create(
                    Code.AnalyzersProject,
                    new ArgumentListAnalyzer());

                // Warmup
                benchmark.Run();
                Console.WriteLine("Attach profiler and press any key to continue...");
                Console.ReadKey();
                benchmark.Run();
            }
            else
            {
                foreach (var summary in RunSingle<AllBenchmarks>())
                {
                    CopyResult(summary);
                }
            }
        }

        private static IEnumerable<Summary> RunAll()
        {
            var switcher = new BenchmarkSwitcher(typeof(Program).Assembly);
            var summaries = switcher.Run(new[] { "*" });
            return summaries;
        }

        private static IEnumerable<Summary> RunSingle<T>()
        {
            yield return BenchmarkRunner.Run<T>();
        }

        private static void CopyResult(Summary summary)
        {
            var sourceFileName = Directory.EnumerateFiles(summary.ResultsDirectoryPath, $"*{summary.Title}-report-github.md")
                                          .Single();
            var destinationFileName = Path.ChangeExtension(FindCsFile(), ".md");
            Console.WriteLine($"Copy: {sourceFileName}");
            Console.WriteLine($"   -> {destinationFileName}");
            File.Copy(sourceFileName, destinationFileName, overwrite: true);

            string FindCsFile()
            {
                return Directory.EnumerateFiles(
                                    AppDomain.CurrentDomain.BaseDirectory.Split(new[] { "\\bin\\" }, StringSplitOptions.RemoveEmptyEntries).First(),
                                    $"{summary.Title.Split('.').Last()}.cs",
                                    SearchOption.AllDirectories)
                                .Single();
            }
        }
    }
}
