using System.Diagnostics;
using System.Globalization;

namespace MeanShiftCoursework;

public static class Benchmark
{
    private static readonly int[] DatasetSizes = [1_000, 5_000, 10_000, 20_000];
    private const int Repetitions = 3;
    private const int RandomSeed = 42;
    private const double Bandwidth = 2.0;
    private const double Tolerance = 1e-3;
    private const int MaxIterations = 300;

    public static void Run()
    {
        var resultsPath = GetResultsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(resultsPath)!);

        using var writer = new StreamWriter(resultsPath, append: false);
        writer.WriteLine(
            "DatasetSize,Algorithm,ThreadCount,Repeat,ExecutionTimeMs,AverageTimeMs,Speedup,Efficiency,ClusterCount");

        foreach (var datasetSize in DatasetSizes)
        {
            // Generate once so the same points are used by every timed run.
            var points = DataGenerator.GeneratePoints(datasetSize, RandomSeed);
            Console.WriteLine($"\nDataset size: {datasetSize:N0}");

            // Warm up both implementations; these results are intentionally not measured.
            _ = MeanShift.Cluster(points, Bandwidth, Tolerance, MaxIterations);
            _ = ParallelMeanShift.Cluster(
                points, Bandwidth, Tolerance, MaxIterations, maxDegreeOfParallelism: 20);

            var sequentialTimes = new double[Repetitions];
            MeanShiftResult? baselineResult = null;

            // Measure the sequential baseline before any parallel configurations.
            for (var repeat = 0; repeat < Repetitions; repeat++)
            {
                var (result, elapsedMilliseconds) = Measure(
                    () => MeanShift.Cluster(points, Bandwidth, Tolerance, MaxIterations));

                sequentialTimes[repeat] = elapsedMilliseconds;
                if (baselineResult is null)
                {
                    baselineResult = result;
                }
                else
                {
                    EnsureEquivalent(baselineResult, result, datasetSize, "Sequential", 1, repeat + 1);
                }
            }

            var sequentialAverage = sequentialTimes.Average();
            var baseline = baselineResult!;

            for (var repeat = 0; repeat < Repetitions; repeat++)
            {
                WriteResult(
                    writer,
                    datasetSize,
                    "Sequential",
                    1,
                    repeat + 1,
                    sequentialTimes[repeat],
                    sequentialAverage,
                    speedup: 1.0,
                    efficiency: 1.0,
                    baseline.ClusterCount);
            }

            PrintSummary("Sequential", 1, sequentialAverage, baseline.ClusterCount);

            // Run each parallel configuration on the same points and with the same parameters.
            for (var threadCount = 1; threadCount <= 20; threadCount++)
            {
                var parallelTimes = new double[Repetitions];

                for (var repeat = 0; repeat < Repetitions; repeat++)
                {
                    var (result, elapsedMilliseconds) = Measure(
                        () => ParallelMeanShift.Cluster(
                            points,
                            Bandwidth,
                            Tolerance,
                            MaxIterations,
                            maxDegreeOfParallelism: threadCount));

                    EnsureEquivalent(
                        baseline, result, datasetSize, "Parallel", threadCount, repeat + 1);
                    parallelTimes[repeat] = elapsedMilliseconds;
                }

                var parallelAverage = parallelTimes.Average();
                var speedup = sequentialAverage / parallelAverage;
                var efficiency = speedup / threadCount;

                for (var repeat = 0; repeat < Repetitions; repeat++)
                {
                    WriteResult(
                        writer,
                        datasetSize,
                        "Parallel",
                        threadCount,
                        repeat + 1,
                        parallelTimes[repeat],
                        parallelAverage,
                        speedup,
                        efficiency,
                        baseline.ClusterCount);
                }

                PrintSummary("Parallel", threadCount, parallelAverage, baseline.ClusterCount);
            }

            writer.Flush();
        }

        Console.WriteLine($"\nCSV results written to: {resultsPath}");
    }

    private static (MeanShiftResult Result, double ElapsedMilliseconds) Measure(
        Func<MeanShiftResult> operation)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = operation();
        stopwatch.Stop();

        return (result, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static void EnsureEquivalent(
        MeanShiftResult expected,
        MeanShiftResult actual,
        int datasetSize,
        string algorithm,
        int threadCount,
        int repeat)
    {
        var centersMatch = expected.Centers.Length == actual.Centers.Length &&
            expected.Centers
                .Zip(actual.Centers)
                .All(pair => pair.First.SequenceEqual(pair.Second));

        if (!centersMatch || !expected.Labels.SequenceEqual(actual.Labels))
        {
            throw new InvalidOperationException(
                $"{algorithm} result differs from the sequential baseline " +
                $"(size={datasetSize}, threads={threadCount}, repeat={repeat}).");
        }
    }

    private static void WriteResult(
        StreamWriter writer,
        int datasetSize,
        string algorithm,
        int threadCount,
        int repeat,
        double executionTime,
        double averageTime,
        double speedup,
        double efficiency,
        int clusterCount)
    {
        var culture = CultureInfo.InvariantCulture;
        writer.WriteLine(string.Join(",",
        [
            datasetSize.ToString(culture),
            algorithm,
            threadCount.ToString(culture),
            repeat.ToString(culture),
            executionTime.ToString("F3", culture),
            averageTime.ToString("F3", culture),
            speedup.ToString("F4", culture),
            efficiency.ToString("F4", culture),
            clusterCount.ToString(culture)
        ]));
    }

    private static void PrintSummary(
        string algorithm,
        int threadCount,
        double averageTime,
        int clusterCount)
    {
        Console.WriteLine(
            $"  {algorithm,-10} threads={threadCount,2}: " +
            $"average={averageTime:F3} ms, clusters={clusterCount}");
    }

    private static string GetResultsPath()
    {
        var projectFileName = $"{typeof(Benchmark).Assembly.GetName().Name}.csproj";

        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, projectFileName)))
            {
                return Path.Combine(directory.FullName, "Results", "results.csv");
            }
        }

        return Path.Combine(Environment.CurrentDirectory, "Results", "results.csv");
    }
}