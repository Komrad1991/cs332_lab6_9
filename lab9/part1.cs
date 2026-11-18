using Lab6.Geometry;

namespace Lab9.Lighting;

public sealed record Material(Color3 Ambient, Color3 Diffuse);

public readonly record struct Color3(double R, double G, double B)
{
    public static Color3 operator +(Color3 a, Color3 b) => new(a.R + b.R, a.G + b.G, a.B + b.B);
    public static Color3 operator *(Color3 a, double k) => new(a.R * k, a.G * k, a.B * k);
    public static Color3 operator *(Color3 a, Color3 b) => new(a.R * b.R, a.G * b.G, a.B * b.B);
    public Color3 Clamp() => new(Math.Clamp(R, 0, 1), Math.Clamp(G, 0, 1), Math.Clamp(B, 0, 1));
}

/// <summary>Модель Ламберта (диффузное отражение) — используется для Gouraud и Phong.</summary>
public static class Lambert
{
    public static Color3 Shade(
        Point3 worldPos, Point3 normal, Point3 viewDir,
        Point3 lightPos, Color3 lightColor,
        Material mat)
    {
        var L = (lightPos - worldPos).Normalized();
        double ndl = Math.Max(0, Point3.Dot(normal, L));
        return (mat.Ambient + mat.Diffuse * lightColor * ndl).Clamp();
    }
}

/// <summary>Модель Тун (Toon): яркость квантуется на N уровней.</summary>
public static class Toon
{
    public static Color3 Shade(
        Point3 worldPos, Point3 normal, Point3 lightPos, Color3 lightColor,
        Material mat, int levels = 4)
    {
        var L = (lightPos - worldPos).Normalized();
        double ndl = Math.Max(0, Point3.Dot(normal, L));
        // Квантование: 0, 1/levels, 2/levels, ..., 1
        double quantized = Math.Round(ndl * levels) / levels;
        return (mat.Ambient + mat.Diffuse * lightColor * quantized).Clamp();
    }
}

public static class VertexNormals
{
    public static Point3[] Compute(Polyhedron m)
    {
        var res = new Point3[m.Vertices.Count];
        foreach (var f in m.Faces)
        {
            var n = f.Normal(m.Vertices).Normalized();
            foreach (var i in f.VertexIndices) res[i] += n;
        }
        for (int i = 0; i < res.Length; i++) res[i] = res[i].Normalized();
        return res;
    }
}