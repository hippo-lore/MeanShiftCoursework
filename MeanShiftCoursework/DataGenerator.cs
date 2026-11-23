namespace MeanShiftCoursework;

public static class DataGenerator
{
    private static readonly (double X, double Y)[] ClusterCenters =
    [
        (-5.0, -5.0),
        (0.0, 5.0),
        (5.0, -3.0)
    ];

    private const double StandardDeviation = 1.0;

    public static double[][] GeneratePoints(int pointCount, int seed = 42)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pointCount);

        var random = new Random(seed);
        var points = new double[pointCount][];

        for (var i = 0; i < pointCount; i++)
        {
            var center = ClusterCenters[random.Next(ClusterCenters.Length)];
            var (offsetX, offsetY) = GenerateGaussianPair(random);

            points[i] =
            [
                center.X + offsetX * StandardDeviation,
                center.Y + offsetY * StandardDeviation
            ];
        }

        return points;
    }

    private static (double X, double Y) GenerateGaussianPair(Random random)
    {
        var radius = Math.Sqrt(-2.0 * Math.Log(1.0 - random.NextDouble()));
        var angle = 2.0 * Math.PI * random.NextDouble();

        return (radius * Math.Cos(angle), radius * Math.Sin(angle));
    }
}