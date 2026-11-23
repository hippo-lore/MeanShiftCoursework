namespace MeanShiftCoursework;

public static class ParallelMeanShift
{
    public static MeanShiftResult Cluster(
        double[][] points,
        double bandwidth,
        double tolerance = 1e-3,
        int maxIterations = 300,
        int maxDegreeOfParallelism = 4)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (!double.IsFinite(bandwidth) || bandwidth <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(bandwidth), "Bandwidth must be finite and greater than zero.");
        }

        if (!double.IsFinite(tolerance) || tolerance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), "Tolerance must be finite and greater than zero.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxIterations);

        if (maxDegreeOfParallelism is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxDegreeOfParallelism),
                "The degree of parallelism must be between 1 and 20.");
        }

        ValidatePoints(points);

        if (points.Length == 0)
        {
            return new MeanShiftResult([], []);
        }

        var modes = new (double X, double Y)[points.Length];
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = maxDegreeOfParallelism
        };

        // Each iteration reads the input points and writes only its own mode slot.
        Parallel.For(0, points.Length, parallelOptions, pointIndex =>
        {
            modes[pointIndex] = FindMode(points, pointIndex, bandwidth, tolerance, maxIterations);
        });

        // Merge in input order to match the sequential implementation's cluster numbering.
        var centers = new List<(double X, double Y)>();
        var labels = new int[points.Length];
        var mergeDistance = bandwidth / 2.0;

        for (var pointIndex = 0; pointIndex < modes.Length; pointIndex++)
        {
            var mode = modes[pointIndex];
            var closestCenter = -1;
            var closestDistanceSquared = double.PositiveInfinity;

            for (var centerIndex = 0; centerIndex < centers.Count; centerIndex++)
            {
                var offsetX = mode.X - centers[centerIndex].X;
                var offsetY = mode.Y - centers[centerIndex].Y;
                var distanceSquared = offsetX * offsetX + offsetY * offsetY;

                if (distanceSquared <= mergeDistance * mergeDistance &&
                    distanceSquared < closestDistanceSquared)
                {
                    closestCenter = centerIndex;
                    closestDistanceSquared = distanceSquared;
                }
            }

            if (closestCenter < 0)
            {
                closestCenter = centers.Count;
                centers.Add(mode);
            }

            labels[pointIndex] = closestCenter;
        }

        var centerPoints = centers
            .Select(center => new[] { center.X, center.Y })
            .ToArray();

        return new MeanShiftResult(centerPoints, labels);
    }

    private static (double X, double Y) FindMode(
        double[][] points,
        int pointIndex,
        double bandwidth,
        double tolerance,
        int maxIterations)
    {
        var x = points[pointIndex][0];
        var y = points[pointIndex][1];

        for (var iteration = 0; iteration < maxIterations; iteration++)
        {
            var weightedX = 0.0;
            var weightedY = 0.0;
            var totalWeight = 0.0;

            for (var neighborIndex = 0; neighborIndex < points.Length; neighborIndex++)
            {
                var offsetX = (points[neighborIndex][0] - x) / bandwidth;
                var offsetY = (points[neighborIndex][1] - y) / bandwidth;
                var scaledDistanceSquared = offsetX * offsetX + offsetY * offsetY;

                if (scaledDistanceSquared > 1.0)
                {
                    continue;
                }

                var weight = Math.Exp(-0.5 * scaledDistanceSquared);
                weightedX += points[neighborIndex][0] * weight;
                weightedY += points[neighborIndex][1] * weight;
                totalWeight += weight;
            }

            if (totalWeight == 0.0)
            {
                throw new InvalidOperationException("No points were found within the bandwidth.");
            }

            var nextX = weightedX / totalWeight;
            var nextY = weightedY / totalWeight;
            var shiftX = nextX - x;
            var shiftY = nextY - y;

            x = nextX;
            y = nextY;

            if (shiftX * shiftX + shiftY * shiftY <= tolerance * tolerance)
            {
                break;
            }
        }

        return (x, y);
    }

    private static void ValidatePoints(double[][] points)
    {
        for (var i = 0; i < points.Length; i++)
        {
            if (points[i] is null || points[i].Length != 2 ||
                !double.IsFinite(points[i][0]) || !double.IsFinite(points[i][1]))
            {
                throw new ArgumentException(
                    $"Point at index {i} must contain exactly two finite coordinates.",
                    nameof(points));
            }
        }
    }
}