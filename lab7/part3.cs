using Lab6.Geometry;

namespace Lab7.Builders;

public static class SurfaceFunctionPresets
{
    public record Preset(string Name, Func<double, double, double> F);

    public static readonly Preset[] All =
    {
        new("Плоскость",      (x,y) => x + y),
        new("Седло",          (x,y) => x*x - y*y),
        new("Купол",          (x,y) => Math.Sqrt(Math.Max(0, 4 - x*x - y*y))),
        new("Волна",          (x,y) => Math.Sin(x) * Math.Cos(y)),
        new("Гиперболоид",    (x,y) => x*x - y*y - 1),
        new("Рябь",           (x,y) => Math.Sin(x*x + y*y) * 0.3),
        new("Гаусс",          (x,y) => Math.Exp(-(x*x + y*y) / 2))
    };
}

public static class SurfaceValidator
{
    public static string? Validate(double x0, double x1, double y0, double y1, int seg)
    {
        if (!double.IsFinite(x0) || !double.IsFinite(x1) || x0 >= x1) return "Некорректный диапазон X.";
        if (!double.IsFinite(y0) || !double.IsFinite(y1) || y0 >= y1) return "Некорректный диапазон Y.";
        if (seg < 2 || seg > 200) return "Разбиение ∈ [2, 200].";
        return null;
    }
}

// Утилита: настройка "камеры" поверх готовой модели для отображения
public static class AutoFit
{
    public static (Point3 center, double radius) Fit(Polyhedron m)
    {
        var c = m.Center();
        double r = m.Vertices.Count == 0 ? 1 : m.Vertices.Max(v => Point3.Distance(c, v));
        return (c, r < 1e-9 ? 1 : r);
    }
}