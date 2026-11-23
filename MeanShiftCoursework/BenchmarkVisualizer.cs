using System.Globalization;

namespace MeanShiftCoursework;

public static class BenchmarkVisualizer
{
    private const int ImageWidth = 1600;
    private const int ImageHeight = 1000;

    public static void GeneratePlots()
    {
        var resultsPath = FindResultsPath();
        var outputDirectory = Path.GetDirectoryName(resultsPath)
            ?? Path.Combine(Environment.CurrentDirectory, "Results");

        var rows = ReadUniqueRows(resultsPath);
        if (rows.Count == 0)
        {
            throw new InvalidDataException($"CSV-файл не містить даних: {resultsPath}");
        }

        var datasetSizes = rows
            .Select(row => row.DatasetSize)
            .Distinct()
            .OrderBy(size => size)
            .ToArray();

        var colors = new[]
        {
            ScottPlot.Colors.Blue,
            ScottPlot.Colors.Orange,
            ScottPlot.Colors.Green
        };

        SaveExecutionTimePlot(rows, datasetSizes, colors, outputDirectory);
        SaveSpeedupPlot(rows, datasetSizes, colors, outputDirectory);
        SaveEfficiencyPlot(rows, datasetSizes, colors, outputDirectory);

        Console.WriteLine($"Графіки згенеровано з: {resultsPath}");
        Console.WriteLine($"Каталог результатів: {outputDirectory}");
    }

    private static List<BenchmarkRow> ReadUniqueRows(string resultsPath)
    {
        if (!File.Exists(resultsPath))
        {
            throw new FileNotFoundException("Файл Results/results.csv не знайдено.", resultsPath);
        }

        var lines = File.ReadAllLines(resultsPath);
        if (lines.Length < 2)
        {
            throw new InvalidDataException($"CSV-файл порожній: {resultsPath}");
        }

        var headers = ParseCsvLine(lines[0])
            .Select(header => header.Trim())
            .ToArray();

        var requiredHeaders = new[]
        {
            "DatasetSize",
            "Algorithm",
            "ThreadCount",
            "AverageTimeMs"
        };

        var indexes = requiredHeaders.ToDictionary(
            header => header,
            header =>
            {
                var index = Array.FindIndex(headers,
                    candidate => candidate.Equals(header, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    throw new InvalidDataException(
                        $"У CSV відсутній обов'язковий стовпець '{header}'.");
                }

                return index;
            });

        var uniqueRows = new Dictionary<(int DatasetSize, string Algorithm, int ThreadCount), BenchmarkRow>();

        for (var lineIndex = 1; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (fields.Length != headers.Length ||
                !TryParseInt(fields[indexes["DatasetSize"]], out var datasetSize) ||
                !TryParseInt(fields[indexes["ThreadCount"]], out var threadCount) ||
                !TryParseDouble(fields[indexes["AverageTimeMs"]], out var averageTime))
            {
                throw new InvalidDataException(
                    $"Некоректні числові дані у рядку {lineIndex + 1} CSV-файлу.");
            }

            var algorithm = fields[indexes["Algorithm"]].Trim();
            if (datasetSize <= 0 || threadCount <= 0 || !double.IsFinite(averageTime) || averageTime <= 0 ||
                (algorithm != "Sequential" && algorithm != "Parallel"))
            {
                throw new InvalidDataException(
                    $"Некоректні значення у рядку {lineIndex + 1} CSV-файлу.");
            }

            var key = (datasetSize, algorithm, threadCount);
            var row = new BenchmarkRow(datasetSize, algorithm, threadCount, averageTime);

            if (!uniqueRows.TryAdd(key, row) &&
                uniqueRows[key].AverageTimeMs != averageTime)
            {
                throw new InvalidDataException(
                    $"Повторювані рядки для size={datasetSize}, algorithm={algorithm}, " +
                    $"threads={threadCount} мають різні AverageTimeMs.");
            }
        }

        return uniqueRows.Values.ToList();
    }

    private static void SaveExecutionTimePlot(
        IReadOnlyCollection<BenchmarkRow> rows,
        int[] datasetSizes,
        ScottPlot.Color[] colors,
        string outputDirectory)
    {
        for (var sizeIndex = 0; sizeIndex < datasetSizes.Length; sizeIndex++)
        {
            var datasetSize = datasetSizes[sizeIndex];
            SaveExecutionTimePlot(
                rows.Where(row => row.DatasetSize == datasetSize).ToArray(),
                [datasetSize],
                [colors[sizeIndex % colors.Length]],
                outputDirectory,
                $"execution_time_{datasetSize}.png",
                includeDatasetLabel: false);
        }

        SaveExecutionTimePlot(
            rows,
            datasetSizes,
            colors,
            outputDirectory,
            "execution_time.png",
            includeDatasetLabel: true);
    }

    private static void SaveExecutionTimePlot(
        IReadOnlyCollection<BenchmarkRow> rows,
        int[] datasetSizes,
        ScottPlot.Color[] colors,
        string outputDirectory,
        string fileName,
        bool includeDatasetLabel)
    {
        var plot = new ScottPlot.Plot();
        for (var sizeIndex = 0; sizeIndex < datasetSizes.Length; sizeIndex++)
        {
            var size = datasetSizes[sizeIndex];
            var seriesColor = colors[sizeIndex % colors.Length];

            var parallelRows = rows
                .Where(row => row.DatasetSize == size && row.Algorithm == "Parallel")
                .OrderBy(row => row.ThreadCount)
                .ToArray();

            if (parallelRows.Length > 0)
            {
                var scatter = plot.Add.Scatter(
                    parallelRows.Select(row => (double)row.ThreadCount).ToArray(),
                    parallelRows.Select(row => row.AverageTimeMs).ToArray());
                scatter.Color = seriesColor;
                scatter.LineWidth = 2.5f;
                scatter.MarkerSize = 7;
                scatter.LegendText = includeDatasetLabel
                    ? $"{size:N0} точок — паралельний"
                    : "Паралельний";
            }

            var sequentialRow = rows.FirstOrDefault(row =>
                row.DatasetSize == size && row.Algorithm == "Sequential");
            if (sequentialRow is not null)
            {
                var sequentialLine = plot.Add.HorizontalLine(sequentialRow.AverageTimeMs);
                sequentialLine.Color = seriesColor;
                sequentialLine.LinePattern = ScottPlot.LinePattern.Dashed;
                sequentialLine.LineWidth = 2.5f;
                sequentialLine.LegendText = includeDatasetLabel
                    ? $"{size:N0} точок — послідовний"
                    : "Послідовний";
            }
        }

        var maximumTime = rows.Max(row => row.AverageTimeMs);
        plot.Axes.SetLimitsY(0, maximumTime * 2);
        ConfigurePlot(
            plot,
            "Середній час виконання Mean Shift",
            "Кількість потоків",
            "Час виконання, мс");
        plot.SavePng(Path.Combine(outputDirectory, fileName), ImageWidth, ImageHeight);
    }

    private static void SaveSpeedupPlot(
        IReadOnlyCollection<BenchmarkRow> rows,
        int[] datasetSizes,
        ScottPlot.Color[] colors,
        string outputDirectory)
    {
        var plot = new ScottPlot.Plot();

        for (var sizeIndex = 0; sizeIndex < datasetSizes.Length; sizeIndex++)
        {
            var datasetSize = datasetSizes[sizeIndex];
            var sequentialRow = rows.FirstOrDefault(row =>
                row.DatasetSize == datasetSize && row.Algorithm == "Sequential");
            var parallelRows = rows
                .Where(row => row.DatasetSize == datasetSize && row.Algorithm == "Parallel")
                .OrderBy(row => row.ThreadCount)
                .ToArray();

            if (sequentialRow is null || parallelRows.Length == 0)
            {
                continue;
            }

            var scatter = plot.Add.Scatter(
                parallelRows.Select(row => (double)row.ThreadCount).ToArray(),
                parallelRows
                    .Select(row => sequentialRow.AverageTimeMs / row.AverageTimeMs)
                    .ToArray());
            scatter.Color = colors[sizeIndex % colors.Length];
            scatter.LineWidth = 2.5f;
            scatter.MarkerSize = 7;
            scatter.LegendText = $"{datasetSize:N0} точок";
        }

        var threadCounts = rows
            .Where(row => row.Algorithm == "Parallel")
            .Select(row => (double)row.ThreadCount)
            .Distinct()
            .Order()
            .ToArray();
        if (threadCounts.Length > 0)
        {
            var idealSpeedup = plot.Add.Scatter(threadCounts, threadCounts);
            idealSpeedup.Color = ScottPlot.Colors.Gray;
            idealSpeedup.LinePattern = ScottPlot.LinePattern.Dashed;
            idealSpeedup.LineWidth = 2.5f;
            idealSpeedup.MarkerSize = 0;
            idealSpeedup.LegendText = "Ідеальне прискорення S(p) = p";

            var maximumSpeedup = rows
                .Where(row => row.Algorithm == "Parallel")
                .Select(row => rows.First(sequential =>
                    sequential.DatasetSize == row.DatasetSize &&
                    sequential.Algorithm == "Sequential").AverageTimeMs / row.AverageTimeMs)
                .Append(threadCounts[^1])
                .Max();
            plot.Axes.SetLimitsY(0, maximumSpeedup * 2);
        }

        ConfigurePlot(
            plot,
            "Прискорення паралельного Mean Shift",
            "Кількість потоків",
            "Прискорення, разів");
        plot.SavePng(
            Path.Combine(outputDirectory, "speedup.png"),
            ImageWidth,
            ImageHeight);
    }

    private static void SaveEfficiencyPlot(
        IReadOnlyCollection<BenchmarkRow> rows,
        int[] datasetSizes,
        ScottPlot.Color[] colors,
        string outputDirectory)
    {
        var plot = new ScottPlot.Plot();

        for (var sizeIndex = 0; sizeIndex < datasetSizes.Length; sizeIndex++)
        {
            var datasetSize = datasetSizes[sizeIndex];
            var sequentialRow = rows.FirstOrDefault(row =>
                row.DatasetSize == datasetSize && row.Algorithm == "Sequential");
            var parallelRows = rows
                .Where(row => row.DatasetSize == datasetSize && row.Algorithm == "Parallel")
                .OrderBy(row => row.ThreadCount)
                .ToArray();

            if (sequentialRow is null || parallelRows.Length == 0)
            {
                continue;
            }

            var scatter = plot.Add.Scatter(
                parallelRows.Select(row => (double)row.ThreadCount).ToArray(),
                parallelRows
                    .Select(row =>
                        (sequentialRow.AverageTimeMs / row.AverageTimeMs) /
                        row.ThreadCount * 100.0)
                    .ToArray());
            scatter.Color = colors[sizeIndex % colors.Length];
            scatter.LineWidth = 2.5f;
            scatter.MarkerSize = 7;
            scatter.LegendText = $"{datasetSize:N0} точок";
        }

        var maximumEfficiency = rows
            .Where(row => row.Algorithm == "Parallel")
            .Select(row =>
            {
                var sequentialTime = rows.First(sequential =>
                    sequential.DatasetSize == row.DatasetSize &&
                    sequential.Algorithm == "Sequential").AverageTimeMs;
                return sequentialTime / row.AverageTimeMs / row.ThreadCount * 100.0;
            })
            .DefaultIfEmpty(100.0)
            .Max();
        plot.Axes.SetLimitsY(0, maximumEfficiency <= 100.0 ? 100.0 : maximumEfficiency * 1.1);
        var referenceLine = plot.Add.HorizontalLine(100);
        referenceLine.Color = ScottPlot.Colors.Gray;
        referenceLine.LinePattern = ScottPlot.LinePattern.Dashed;
        referenceLine.LineWidth = 2.5f;
        referenceLine.LegendText = "Орієнтир — 100%";

        ConfigurePlot(
            plot,
            "Ефективність паралельного Mean Shift",
            "Кількість потоків",
            "Ефективність, %");
        plot.SavePng(
            Path.Combine(outputDirectory, "efficiency.png"),
            ImageWidth,
            ImageHeight);
    }

    private static void ConfigurePlot(
        ScottPlot.Plot plot,
        string title,
        string xLabel,
        string yLabel)
    {
        plot.FigureBackground.Color = ScottPlot.Colors.White;
        plot.DataBackground.Color = ScottPlot.Colors.White;
        plot.Title(title, 28);
        plot.XLabel(xLabel, 22);
        plot.YLabel(yLabel, 22);
        plot.Axes.Bottom.TickLabelStyle.FontSize = 17;
        plot.Axes.Left.TickLabelStyle.FontSize = 17;
        plot.Grid.MajorLineColor = new ScottPlot.Color(225, 228, 232);
        plot.Legend.IsVisible = true;
        plot.Legend.Alignment = ScottPlot.Alignment.UpperRight;
        plot.Legend.FontSize = 16;
    }

    private static string FindResultsPath()
    {
        const string projectFileName = "MeanShiftCoursework.csproj";

        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, projectFileName)))
            {
                return Path.Combine(directory.FullName, "Results", "results.csv");
            }
        }

        return Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "Results",
            "results.csv"));
    }

    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var start = 0;
        for (var index = 0; index <= line.Length; index++)
        {
            if (index == line.Length || line[index] == ',')
            {
                fields.Add(line[start..index].Trim().Trim('"'));
                start = index + 1;
            }
        }

        return fields.ToArray();
    }

    private static bool TryParseInt(string text, out int value)
    {
        return int.TryParse(
            text.Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static bool TryParseDouble(string text, out double value)
    {
        return double.TryParse(
            text.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private sealed record BenchmarkRow(
        int DatasetSize,
        string Algorithm,
        int ThreadCount,
        double AverageTimeMs);
}
