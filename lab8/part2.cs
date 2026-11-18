using Lab6.Geometry;
using Lab6.Rendering;
using Lab8.Cameras;

namespace Lab8.Rendering;

public sealed class ZBuffer
{
    public int Width { get; }
    public int Height { get; }
    private readonly double[] _depth;
    private readonly uint[] _color;

    public ZBuffer(int w, int h)
    {
        Width = w; Height = h;
        _depth = new double[w * h];
        _color = new uint[w * h];
        Clear();
    }

    public void Clear()
    {
        Array.Fill(_depth, double.PositiveInfinity);
        Array.Fill(_color, 0xFFFFFFFF);
    }

    public ReadOnlySpan<uint> Pixels => _color;
    public ReadOnlySpan<double> Depth => _depth;

    public void Triangle(
        double x1, double y1, double z1,
        double x2, double y2, double z2,
        double x3, double y3, double z3,
        uint color)
    {
        int minX = Math.Max(0, (int)Math.Floor(Math.Min(x1, Math.Min(x2, x3))));
        int maxX = Math.Min(Width - 1, (int)Math.Ceiling(Math.Max(x1, Math.Max(x2, x3))));
        int minY = Math.Max(0, (int)Math.Floor(Math.Min(y1, Math.Min(y2, y3))));
        int maxY = Math.Min(Height - 1, (int)Math.Ceiling(Math.Max(y1, Math.Max(y2, y3))));

        double denom = (y2 - y3) * (x1 - x3) + (x3 - x2) * (y1 - y3);
        if (Math.Abs(denom) < 1e-9) return;
        double inv = 1.0 / denom;

        for (int py = minY; py <= maxY; py++)
            for (int px = minX; px <= maxX; px++)
            {
                double cx = px + 0.5, cy = py + 0.5;
                double a = ((y2 - y3) * (cx - x3) + (x3 - x2) * (cy - y3)) * inv;
                double b = ((y3 - y1) * (cx - x3) + (x1 - x3) * (cy - y3)) * inv;
                double g = 1 - a - b;
                if (a < -1e-9 || b < -1e-9 || g < -1e-9) continue;

                double z = a * z1 + b * z2 + g * z3;
                int idx = py * Width + px;
                if (z < _depth[idx])
                {
                    _depth[idx] = z;
                    _color[idx] = color;
                }
            }
    }
}

public static class ZBufferRenderer
{
    /// <summary>
    /// Проецирует все модели через камеру и растеризует их в Z-буфер.
    /// Принимает готовые (уже повёрнутые и позиционированные) модели в мировых координатах.
    /// </summary>
    public static void Render(IEnumerable<Polyhedron> models, CameraObject cam, ZBuffer buf)
    {
        buf.Clear();
        double ppu = Math.Min(buf.Width, buf.Height) * 0.45;
        var light = new Point3(0.4, 0.8, -1).Normalized();

        foreach (var model in models)
        {
            if (model.Vertices.Count == 0) continue;

            var screen = new (double x, double y, double z)[model.Vertices.Count];
            for (int i = 0; i < model.Vertices.Count; i++)
            {
                var (x, y, z) = cam.ProjectNdc(model.Vertices[i]);
                screen[i] = (buf.Width / 2.0 + x * ppu,
                             buf.Height / 2.0 - y * ppu,
                             z);
            }

            foreach (var f in model.Faces)
            {
                var n = f.Normal(model.Vertices);
                double k = Math.Clamp(0.3 + 0.7 * Math.Abs(Point3.Dot(n, light)), 0, 1);
                byte rr = (byte)(60 + 160 * k), gg = (byte)(80 + 130 * k), bb = (byte)(100 + 100 * k);
                uint color = 0xFF000000u | ((uint)rr << 16) | ((uint)gg << 8) | bb;

                var idx = f.VertexIndices;
                for (int i = 1; i < idx.Length - 1; i++)
                {
                    var a = screen[idx[0]];
                    var b = screen[idx[i]];
                    var c = screen[idx[i + 1]];
                    buf.Triangle(a.x, a.y, a.z, b.x, b.y, b.z, c.x, c.y, c.z, color);
                }
            }
        }
    }
}