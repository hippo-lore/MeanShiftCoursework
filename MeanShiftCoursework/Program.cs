using System.Diagnostics;
using System.Globalization;
using System.Text;
using MeanShiftCoursework;

Console.OutputEncoding = Encoding.UTF8;

if (args.Any(argument =>
        argument.Equals("--plots", StringComparison.OrdinalIgnoreCase) ||
        argument.Equals("--generate-plots", StringComparison.OrdinalIgnoreCase)))
{
    try
    {
        BenchmarkVisualizer.GeneratePlots();
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Помилка генерації графіків: {exception.Message}");
        Environment.ExitCode = 1;
        return;
    }

    return;
}

while (true)
{
    Console.WriteLine();
    Console.WriteLine("=== Кластеризація Mean Shift ===");
    Console.WriteLine("1. Запуск кластеризації Mean Shift");
    Console.WriteLine("2. Порівняння послідовного та паралельного алгоритмів");
    Console.WriteLine("3. Згенерувати графіки з готового Results/results.csv");
    Console.WriteLine("4. Вихід із програми");
    Console.Write("Оберіть пункт меню: ");

    var choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
        {
            if (!TryReadPositiveInt("Кількість точок: ", out var pointCount) ||
                !TryReadPositiveDouble("Bandwidth (> 0): ", out var bandwidth))
            {
                return;
            }

            var points = DataGenerator.GeneratePoints(pointCount);
            Console.WriteLine("Виконується послідовна кластеризація...");
            var stopwatch = Stopwatch.StartNew();
            var result = MeanShift.Cluster(points, bandwidth);
            stopwatch.Stop();

            Console.WriteLine($"Згенеровано точок: {points.Length}");
            Console.WriteLine($"Знайдено кластерів: {result.ClusterCount}");
            Console.WriteLine(
                $"Час виконання: {stopwatch.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.CurrentCulture)} мс");

            var centersCsvPath = SaveClusterCentersCsv(result);
            Console.WriteLine($"Центри збережено: {centersCsvPath}");

            const int maxDisplayedCenters = 10;
            var displayedCenterCount = Math.Min(result.Centers.Length, maxDisplayedCenters);
            for (var i = 0; i < displayedCenterCount; i++)
            {
                var center = result.Centers[i];
                Console.WriteLine(
                    $"Кластер {i + 1}: центр ({center[0].ToString("F3", CultureInfo.CurrentCulture)}, " +
                    $"{center[1].ToString("F3", CultureInfo.CurrentCulture)})");
            }

            if (result.ClusterCount > maxDisplayedCenters)
            {
                Console.WriteLine(
                    $"І ще {result.ClusterCount - maxDisplayedCenters} кластерів. " +
                    "Повний список збережено у файл.");
            }

            var plotPath = Visualizer.SaveClusterPlot(points, result);
            Console.WriteLine($"На графіку відображено {points.Length} із {points.Length} точок");
            Console.WriteLine($"Графік збережено: {plotPath}");

            break;
        }
        case "2":
            Console.WriteLine("Запуск бенчмарку; це може зайняти тривалий час...");
            Benchmark.Run();
            break;
        case "3":
            BenchmarkVisualizer.GeneratePlots();
            break;
        case "4":
        case null:
            Console.WriteLine("Роботу програми завершено.");
            return;
        default:
            Console.WriteLine("Невірний пункт меню. Введіть 1, 2, 3 або 4.");
            break;
    }
}

static string SaveClusterCentersCsv(MeanShiftResult result)
{
    const string outputPath = "Results/clusters.csv";
    var fullOutputPath = Path.GetFullPath(outputPath);
    Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);

    using var writer = new StreamWriter(fullOutputPath, append: false);
    writer.WriteLine("Cluster,X,Y");

    for (var i = 0; i < result.Centers.Length; i++)
    {
        var center = result.Centers[i];
        writer.WriteLine(
            $"{i + 1}," +
            $"{center[0].ToString("R", CultureInfo.InvariantCulture)}," +
            $"{center[1].ToString("R", CultureInfo.InvariantCulture)}");
    }

    return fullOutputPath;
}

static bool TryReadPositiveInt(string prompt, out int value)
{
    while (true)
    {
        Console.Write(prompt);
        var input = Console.ReadLine();
        if (input is null)
        {
            value = 0;
            return false;
        }

        if (int.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) &&
            value > 0)
        {
            return true;
        }

        Console.WriteLine("Введіть ціле число більше за нуль.");
    }
}

static bool TryReadPositiveDouble(string prompt, out double value)
{
    while (true)
    {
        Console.Write(prompt);
        var input = Console.ReadLine();
        if (input is null)
        {
            value = 0.0;
            return false;
        }

        var normalizedInput = input.Trim().Replace(',', '.');
        if (double.TryParse(
                normalizedInput,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value) &&
            double.IsFinite(value) &&
            value > 0.0)
        {
            return true;
        }

        Console.WriteLine("Введіть скінченне додатне число, наприклад 2 або 1,5.");
    }
}
