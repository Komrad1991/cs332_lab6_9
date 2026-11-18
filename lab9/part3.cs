
using Lab6.Geometry;
using Lab9.Lighting;

namespace Lab9.Shading;

public static class PhongShading
{
    /// <summary>
    /// Per-pixel шейдинг: интерполируются нормали, мировые координаты и глубина.
    /// Цвет вычисляется в каждом пикселе (Lambert или Toon).
    /// </summary>
    public static void Triangle(
        GouraudRenderer buf,
        (double x, double y, double z) a,
        (double x, double y, double z) b,
        (double x, double y, double z) c,
        (double x, double y, double z) wa,
        (double x, double y, double z) wb,
        (double x, double y, double z) wc,
        Point3 na, Point3 nb, Point3 nc,
        Point3 lightPos, Color3 lightColor, Material mat, bool toon)
    {
        int minX = Math.Max(0, (int)Math.Floor(Math.Min(a.x, Math.Min(b.x, c.x))));
        int maxX = Math.Min(buf.Width - 1, (int)Math.Ceiling(Math.Max(a.x, Math.Max(b.x, c.x))));
        int minY = Math.Max(0, (int)Math.Floor(Math.Min(a.y, Math.Min(b.y, c.y))));
        int maxY = Math.Min(buf.Height - 1, (int)Math.Ceiling(Math.Max(a.y, Math.Max(b.y, c.y))));

        double denom = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
        if (Math.Abs(denom) < 1e-9) return;

        double inv = 1.0 / denom;

        var z = buf.InternalsZ;
        var px = buf.InternalsPixels;

        for (int py = minY; py <= maxY; py++)
        {
            for (int ix = minX; ix <= maxX; ix++)
            {
                double cx = ix + 0.5;
                double cy = py + 0.5;

                double u = ((b.y - c.y) * (cx - c.x) +
                            (c.x - b.x) * (cy - c.y)) * inv;

                double v = ((c.y - a.y) * (cx - c.x) +
                            (a.x - c.x) * (cy - c.y)) * inv;

                double w = 1 - u - v;

                if (u < -1e-9 || v < -1e-9 || w < -1e-9)
                    continue;

                double d = u * a.z + v * b.z + w * c.z;
                int idx = py * buf.Width + ix;

                if (d < z[idx])
                {
                    z[idx] = d;

                    var n = (na * u + nb * v + nc * w).Normalized();

                    var wp = new Point3(
                        u * wa.x + v * wb.x + w * wc.x,
                        u * wa.y + v * wb.y + w * wc.y,
                        u * wa.z + v * wb.z + w * wc.z);

                    var col = toon
                        ? Toon.Shade(wp, n, lightPos, lightColor, mat)
                        : Lambert.Shade(
                            wp, n, new Point3(0, 0, -1),
                            lightPos, lightColor, mat);

                    px[idx] = GouraudRenderer.Pack(col);
                }
            }
        }
    }
}

public static class Phong
{
    public static void DrawModel(
        GouraudRenderer buf, Polyhedron m, Point3 lightPos,
        Color3 lightColor, Color3 ambient, Color3 diffuse,
        bool cullBackfaces, Point3 viewDir, bool toon)
    {
        buf.Clear();

        var center = m.Center();

        double r = Math.Max(
            1e-9,
            m.Vertices.Max(p => Point3.Distance(center, p)));

        var verts = m.Vertices
            .Select(p => (p - center) * (1.0 / r))
            .ToArray();

        var temp = new Polyhedron();
        temp.Vertices.AddRange(verts);
        temp.Faces.AddRange(m.Faces);

        var normals = VertexNormals.Compute(temp);

        var lightNorm = (lightPos - center) * (1.0 / r);

        double ppu = Math.Min(buf.Width, buf.Height) * 0.4;

        var scr = new (double x, double y, double z)[verts.Length];
        var world = new (double x, double y, double z)[verts.Length];

        for (int i = 0; i < verts.Length; i++)
        {
            double f = 4.0 / Math.Max(0.1, 4.0 + verts[i].Z);

            scr[i] = (
                buf.Width / 2 + verts[i].X * ppu * f,
                buf.Height / 2 - verts[i].Y * ppu * f,
                verts[i].Z);

            world[i] = (
                verts[i].X,
                verts[i].Y,
                verts[i].Z);
        }

        var mat = new Material(ambient, diffuse);

        // Камера находится в точке (0, 0, -4)
        var cameraPos = new Point3(0, 0, -4);

        foreach (var face in m.Faces)
        {
            var faceNormal = face.Normal(verts).Normalized();
            var faceCenter = face.Center(verts);

            // Направление от грани к камере
            var toCamera = (cameraPos - faceCenter).Normalized();

            // Отсекаем грани, направленные от камеры
            if (cullBackfaces &&
                Point3.Dot(faceNormal, toCamera) <= 1e-6)
                continue;

            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                var i0 = face.VertexIndices[0];
                var i1 = face.VertexIndices[i];
                var i2 = face.VertexIndices[i + 1];

                PhongShading.Triangle(
                    buf,
                    scr[i0], scr[i1], scr[i2],
                    world[i0], world[i1], world[i2],
                    normals[i0], normals[i1], normals[i2],
                    lightNorm, lightColor, mat, toon);
            }
        }
    }
}

public static class Texturing
{
    public static Color3 SampleChecker(
        double u, double v, Color3 c1, Color3 c2, int tiles = 8)
    {
        int uu = (int)Math.Floor((u - Math.Floor(u)) * tiles);
        int vv = (int)Math.Floor((v - Math.Floor(v)) * tiles);

        return ((uu + vv) & 1) == 0 ? c1 : c2;
    }

    public static void DrawFace(
        GouraudRenderer buf,
        Polyhedron m,
        Point3[] verts,
        (double x, double y, double z)[] scr,
        (double u, double v)[] uvPerVertex,
        Color3 c1, Color3 c2,
        bool cullBackfaces, Point3 viewDir)
    {
        // Камера находится в точке (0, 0, -4)
        var cameraPos = new Point3(0, 0, -4);

        foreach (var face in m.Faces)
        {
            var faceNormal = face.Normal(verts).Normalized();
            var faceCenter = face.Center(verts);

            var toCamera = (cameraPos - faceCenter).Normalized();

            // Отсечение невидимых граней
            if (cullBackfaces &&
                Point3.Dot(faceNormal, toCamera) <= 1e-6)
                continue;

            for (int i = 1; i < face.VertexIndices.Length - 1; i++)
            {
                var i0 = face.VertexIndices[0];
                var i1 = face.VertexIndices[i];
                var i2 = face.VertexIndices[i + 1];

                var (ax, ay, az) = scr[i0];
                var (bx, by, bz) = scr[i1];
                var (cx, cy, cz) = scr[i2];

                var (ua, va) = uvPerVertex[i0];
                var (ub, vb) = uvPerVertex[i1];
                var (uc, vc) = uvPerVertex[i2];

                int minX = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
                int maxX = Math.Min(buf.Width - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
                int maxY = Math.Min(buf.Height - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));

                double den = (by - cy) * (ax - cx) +
                             (cx - bx) * (ay - cy);

                if (Math.Abs(den) < 1e-9)
                    continue;

                double invDen = 1.0 / den;

                for (int py = minY; py <= maxY; py++)
                {
                    for (int px = minX; px <= maxX; px++)
                    {
                        double sx = px + 0.5;
                        double sy = py + 0.5;

                        double uu = ((by - cy) * (sx - cx) +
                                     (cx - bx) * (sy - cy)) * invDen;

                        double vv = ((cy - ay) * (sx - cx) +
                                     (ax - cx) * (sy - cy)) * invDen;

                        double ww = 1 - uu - vv;

                        if (uu < -1e-9 || vv < -1e-9 || ww < -1e-9)
                            continue;

                        double z = uu * az + vv * bz + ww * cz;

                        int idx = py * buf.Width + px;

                        if (z < buf.InternalsZ[idx])
                        {
                            buf.InternalsZ[idx] = z;

                            double u = uu * ua + vv * ub + ww * uc;
                            double v = uu * va + vv * vb + ww * vc;

                            var col = SampleChecker(u, v, c1, c2);

                            buf.InternalsPixels[idx] =
                                GouraudRenderer.Pack(col);
                        }
                    }
                }
            }
        }
    }
}
