using Lab6.Geometry;

namespace Lab7.Builders;

public static class ProfilePresets
{
    public static IReadOnlyList<Point3> Cylinder(int steps = 8) =>
        Enumerable.Range(0, steps + 1)
                  .Select(i => new Point3(0.5, -1.0 + 2.0 * i / steps, 0))
                  .ToArray();

    public static IReadOnlyList<Point3> Cone(int steps = 8) =>
        Enumerable.Range(0, steps + 1)
                  .Select(i => new Point3(0.6 * (1.0 - (double)i / steps), -1.0 + 2.0 * i / steps, 0))
                  .ToArray();

    public static IReadOnlyList<Point3> Sphere(int steps = 12) =>
        Enumerable.Range(0, steps + 1)
                  .Select(i => {
                      double t = Math.PI * i / steps;
                      return new Point3(Math.Sin(t), -Math.Cos(t), 0);
                  }).ToArray();

    public static IReadOnlyList<Point3> Vase(int steps = 12) =>
        Enumerable.Range(0, steps + 1)
                  .Select(i => {
                      double t = -1.0 + 2.0 * i / steps;
                      double r = 0.3 + 0.4 * Math.Cos(t * Math.PI * 0.7);
                      return new Point3(r, t, 0);
                  }).ToArray();

    public static IReadOnlyList<Point3> Torus(int steps = 12) =>
        Enumerable.Range(0, steps + 1)
                  .Select(i => {
                      double t = 2 * Math.PI * i / steps;
                      return new Point3(0.6 + 0.25 * Math.Cos(t), 0.25 * Math.Sin(t), 0);
                  }).ToArray();
}

public static class RevolutionValidator
{
    public static string? Validate(IReadOnlyList<Point3> profile, string axis, int seg)
    {
        if (profile.Count < 2) return "Профиль должен содержать ≥2 точек.";
        if (seg < 3) return "Количество разбиений ≥3.";
        if (axis is not ("X" or "Y" or "Z")) return "Ось: X, Y или Z.";
        return null;
    }
}