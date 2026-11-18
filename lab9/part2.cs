using Lab6.Geometry;
using Lab9.Lighting;

namespace Lab9.Shading;

public sealed class GouraudRenderer
{
    public int Width { get; }
    public int Height { get; }
    private readonly uint[] _fb;
    private readonly double[] _z;

    public GouraudRenderer(int w, int h)
    {
        Width = w; Height = h;
        _fb = new uint[w * h];
        _z = new double[w * h];
    }

    public ReadOnlySpan<uint> Pixels => _fb;
    public double[] InternalsZ => _z;
    public uint[] InternalsPixels => _fb;

    public void Clear() { Array.Fill(_fb, 0xFFFFFFFFu); Array.Fill(_z, double.PositiveInfinity); }

    public void Triangle(
        (double x, double y, double z) a, (double x, double y, double z) b, (double x, double y, double z) c,
        Color3 ca, Color3 cb, Color3 cc)
    {
        int minX = Math.Max(0, (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))));
        int maxX = Math.Min(Width - 1, (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))));
        int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))));
        int maxY = Math.Min(Height - 1, (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))));

        double denom = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
        if (Math.Abs(denom) < 1e-9) return;
        double inv = 1.0 / denom;

        for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                double cx = px + 0.5, cy = py + 0.5;
                double u = ((b.y - c.y) * (cx - c.x) + (c.x - b.x) * (cy - c.y)) * inv;
                double v = ((c.y - a.y) * (cx - c.x) + (a.x - c.x) * (cy - c.y)) * inv;
                double w = 1 - u - v;
                if (u < -1e-9 || v < -1e-9 || w < -1e-9) continue;

                double z = u * a.z + v * b.z + w * c.z;
                int idx = py * Width + px;
                if (z < _z[idx])
                {
                    _z[idx] = z;
                    var col = (ca * u + cb * v + cc * w).Clamp();
                    _fb[idx] = Pack(col);
                }
            }
    }

    public static uint Pack(Color3 c) =>
        0xFF000000u | ((uint)(c.R * 255) << 16) | ((uint)(c.G * 255) << 8) | (uint)(c.B * 255);
}

/// <summary>Плоский шейдинг: один цвет на треугольник по нормали грани.</summary>
public static class Flat
{
    public static void DrawModel(
        GouraudRenderer buf, Polyhedron m, Point3 lightPos, Color3 lightColor,
        Color3 ambient, Color3 diffuse, bool cullBackfaces, Point3 viewDir)
    {
        buf.Clear();
        var c = m.Center();
        double r = Math.Max(1e-9, m.Vertices.Max(p => Point3.Distance(c, p)));
        var verts = m.Vertices.Select(p => (p - c) * (1.0 / r)).ToArray();

        // Пересчёт источника света в нормированное пространство (см. комментарий к Gouraud)
        var lightNorm = (lightPos - c) * (1.0 / r);

        double ppu = Math.Min(buf.Width, buf.Height) * 0.4;
        var scr = new (double x, double y, double z)[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            double f = 4.0 / Math.Max(0.1, 4.0 + verts[i].Z);
            scr[i] = (buf.Width / 2 + verts[i].X * ppu * f, buf.Height / 2 - verts[i].Y * ppu * f, verts[i].Z);
        }

        var mat = new Material(ambient, diffuse);
        foreach (var f in m.Faces)
        {
            var n = f.Normal(verts).Normalized();
            var fc = f.Center(verts);

            var cameraPos = new Point3(0, 0, -4);
            var toCamera = (cameraPos - fc).Normalized();

            if (cullBackfaces && Point3.Dot(n, toCamera) <= 1e-6)
                continue;
            var color = Lambert.Shade(fc, n, fc.Negated().Normalized(),
                                      lightNorm, lightColor, mat);
            for (int i = 1; i < f.VertexIndices.Length - 1; i++)
            {
                var i0 = f.VertexIndices[0]; var i1 = f.VertexIndices[i]; var i2 = f.VertexIndices[i + 1];
                buf.Triangle(scr[i0], scr[i1], scr[i2], color, color, color);
            }
        }
    }
}

/// <summary>Шейдинг Гуро: цвет вычисляется в каждой вершине по Ламберту и интерполируется.</summary>
public static class Gouraud
{
    public static void DrawModel(
        GouraudRenderer buf, Polyhedron m, Point3 lightPos, Color3 lightColor,
        Color3 ambient, Color3 diffuse, bool cullBackfaces, Point3 viewDir)
    {
        buf.Clear();
        var c = m.Center();
        double r = Math.Max(1e-9, m.Vertices.Max(p => Point3.Distance(c, p)));
        var verts = m.Vertices.Select(p => (p - c) * (1.0 / r)).ToArray();

        var temp = new Polyhedron();
        temp.Vertices.AddRange(verts);
        temp.Faces.AddRange(m.Faces);
        var normals = VertexNormals.Compute(temp);

        // Важно: пересчёт источника света в нормированное пространство,
        // иначе Lambert.Shade смешивал бы координаты разных масштабов.
        var lightNorm = (lightPos - c) * (1.0 / r);

        double ppu = Math.Min(buf.Width, buf.Height) * 0.4;
        var scr = new (double x, double y, double z)[verts.Length];
        var col = new Color3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            double f = 4.0 / Math.Max(0.1, 4.0 + verts[i].Z);
            scr[i] = (buf.Width / 2 + verts[i].X * ppu * f, buf.Height / 2 - verts[i].Y * ppu * f, verts[i].Z);
            col[i] = Lambert.Shade(verts[i], normals[i], verts[i].Negated().Normalized(),
                                   lightNorm, lightColor, new Material(ambient, diffuse));
        }

        foreach (var f in m.Faces)
        {
            var n = f.Normal(verts).Normalized();
            var fc = f.Center(verts);

            var cameraPos = new Point3(0, 0, -4);
            var toCamera = (cameraPos - fc).Normalized();

            if (cullBackfaces && Point3.Dot(n, toCamera) <= 1e-6)
                continue;
            for (int i = 1; i < f.VertexIndices.Length - 1; i++)
            {
                var i0 = f.VertexIndices[0]; var i1 = f.VertexIndices[i]; var i2 = f.VertexIndices[i + 1];
                buf.Triangle(scr[i0], scr[i1], scr[i2], col[i0], col[i1], col[i2]);
            }
        }
    }
}