namespace MeanShiftCoursework;

public static class Visualizer
{
    private const string DefaultOutputPath = "Results/mean_shift_clusters.png";

    public static string SaveClusterPlot(
        double[][] points,
        MeanShiftResult result,
        string outputPath = DefaultOutputPath)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        if (points.Length != result.Labels.Length)
        {
            throw new ArgumentException("Each point must have exactly one cluster label.", nameof(result));
        }

        var clusterPoints = Enumerable.Range(0, result.ClusterCount)
            .Select(_ => (X: new List<double>(), Y: new List<double>()))
            .ToArray();

        for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
        {
            var point = points[pointIndex];
            if (point is null || point.Length != 2 ||
                !double.IsFinite(point[0]) || !double.IsFinite(point[1]))
            {
                throw new ArgumentException(
                    $"Point at index {pointIndex} must contain exactly two finite coordinates.",
                    nameof(points));
            }

            var clusterId = result.Labels[pointIndex];
            if ((uint)clusterId >= (uint)clusterPoints.Length)
            {
                throw new ArgumentException(
                    $"Cluster label at point index {pointIndex} is outside the available cluster range.",
                    nameof(result));
            }

            clusterPoints[clusterId].X.Add(point[0]);
            clusterPoints[clusterId].Y.Add(point[1]);
        }

        var plot = new ScottPlot.Plot();
        for (var clusterId = 0; clusterId < clusterPoints.Length; clusterId++)
        {
            var cluster = clusterPoints[clusterId];
            if (cluster.X.Count == 0)
            {
                continue;
            }

            var scatter = plot.Add.ScatterPoints(cluster.X.ToArray(), cluster.Y.ToArray());
            scatter.Color = GetClusterColor(clusterId);
            scatter.MarkerShape = ScottPlot.MarkerShape.FilledCircle;
            scatter.MarkerSize = 4;
        }

        if (result.Centers.Length > 0)
        {
            var centers = plot.Add.ScatterPoints(
                result.Centers.Select(center => center[0]).ToArray(),
                result.Centers.Select(center => center[1]).ToArray());
            centers.Color = ScottPlot.Colors.Black;
            centers.MarkerShape = ScottPlot.MarkerShape.Asterisk;
            centers.MarkerSize = 14;
        }

        plot.Title("Кластеризація методом Mean Shift");
        plot.XLabel("X");
        plot.YLabel("Y");

        var fullOutputPath = Path.GetFullPath(outputPath);
        var outputDirectory = Path.GetDirectoryName(fullOutputPath);
        if (!string.IsNullOrEmpty(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        plot.SavePng(fullOutputPath, 1400, 900);
        return fullOutputPath;
    }

    private static ScottPlot.Color GetClusterColor(int clusterId)
    {
        if (clusterId == 0)
        {
            return ScottPlot.Colors.HotPink;
        }

        if (clusterId == 1)
        {
            return ScottPlot.Colors.Green;
        }

        if (clusterId == 2)
        {
            return ScottPlot.Colors.Cyan;
        }

        var hue = ((clusterId - 3) * 137.508 + 24.0) % 360.0;
        const double saturation = 0.72;
        const double lightness = 0.48;
        var chroma = (1.0 - Math.Abs(2.0 * lightness - 1.0)) * saturation;
        var hueSection = hue / 60.0;
        var secondComponent = chroma * (1.0 - Math.Abs(hueSection % 2.0 - 1.0));

        var (red, green, blue) = hueSection switch
        {
            < 1.0 => (chroma, secondComponent, 0.0),
            < 2.0 => (secondComponent, chroma, 0.0),
            < 3.0 => (0.0, chroma, secondComponent),
            < 4.0 => (0.0, secondComponent, chroma),
            < 5.0 => (secondComponent, 0.0, chroma),
            _ => (chroma, 0.0, secondComponent)
        };

        var match = lightness - chroma / 2.0;
        return new ScottPlot.Color(
            red: (int)Math.Round((red + match) * 255.0),
            green: (int)Math.Round((green + match) * 255.0),
            blue: (int)Math.Round((blue + match) * 255.0));
    }
}
