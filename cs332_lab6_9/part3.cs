// ============================================================================
// part3.cs — Задания 5-7 (+ IO: ObjImporter/ObjExporter).
//   5. Вращение вокруг оси через ЦЕНТР многогранника, параллельно коорд. оси.
//   6. Поворот вокруг ПРОИЗВОЛЬНОЙ прямой (две точки), заданный угол.
//   7. Переключение перспектива / аксонометрия.
// ============================================================================
using System.Globalization;
using Lab6.Geometry;

namespace Lab6.Rendering
{
    public enum ProjectionMode { Perspective, Axonometric }

    /// <summary>Математика проецирования (задание 7).</summary>
    public static class ProjectionMath
    {
        public static (double x, double y, double depth) Project(
            Point3 p, ProjectionMode mode, double pixelsPerUnit, double distance)
        {
            if (mode == ProjectionMode.Perspective)
            {
                double f = distance / Math.Max(0.1, distance + p.Z);
                return (p.X * pixelsPerUnit * f, p.Y * pixelsPerUnit * f, p.Z);
            }
            return (p.X * pixelsPerUnit, p.Y * pixelsPerUnit, p.Z);
        }
    }
}

namespace Lab6.Transforms
{
    public static partial class ModelTransforms
    {
        // ---------- Задание 5: поворот вокруг оси через центр ----------
        public static Matrix4 RotateAroundCenterAxis(
            Polyhedron model, string axis, double degrees)
        {
            var c = model.Center();
            double r = degrees * Math.PI / 180;

            var rot = axis.Trim().ToUpperInvariant() switch
            {
                "X" => Matrix4.RotationX(r),
                "Y" => Matrix4.RotationY(r),
                "Z" => Matrix4.RotationZ(r),
                _ => throw new ArgumentException("Axis must be X, Y or Z")
            };

            return Matrix4.Translation(-c.X, -c.Y, -c.Z) *
                   rot *
                   Matrix4.Translation(c.X, c.Y, c.Z);
        }

        // ---------- Задание 6: поворот вокруг произвольной прямой ----------
        public static Matrix4 RotateAroundCustomLine(
            Point3 a, Point3 b, double degrees)
        {
            var dir = (b - a).Normalized();
            if (dir == Point3.Zero)
                throw new ArgumentException("Точки A и B не должны совпадать.");

            return RotationAroundLine(a, dir, degrees * Math.PI / 180);
        }

        private static Matrix4 RotationAroundLine(
            Point3 point, Point3 direction, double radians)
        {
            var d = direction.Normalized();
            if (d == Point3.Zero)
                throw new ArgumentException("Направление не может быть нулевым.");

            var toOrigin = Matrix4.Translation(-point.X, -point.Y, -point.Z);

            double yz = Math.Sqrt(d.Y * d.Y + d.Z * d.Z);
            Matrix4 alignYz;
            if (yz < 1e-12) alignYz = Matrix4.Identity;
            else
            {
                double c = d.Z / yz, s = d.Y / yz;
                alignYz = new Matrix4(1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1);
            }

            double len = Math.Sqrt(d.X * d.X + yz * yz);
            Matrix4 alignXz;
            if (len < 1e-12) alignXz = Matrix4.Identity;
            else
            {
                double c = yz / len, s = -d.X / len;
                alignXz = new Matrix4(c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1);
            }

            return toOrigin *
                   alignYz * alignXz *
                   Matrix4.RotationZ(radians) *
                   alignXz.Inverse() * alignYz.Inverse() *
                   toOrigin.Inverse();
        }
    }
}

namespace Lab6.IO
{
    public static class ObjExporter
    {
        public static void Save(Polyhedron model, string path)
        {
            using var s = File.Create(path);
            Save(model, s);
        }

        public static void Save(Polyhedron model, Stream stream)
        {
            using var w = new StreamWriter(stream, leaveOpen: true);
            w.WriteLine("# Lab6 exported model");
            w.WriteLine($"# vertices: {model.Vertices.Count}, faces: {model.Faces.Count}");
            foreach (var v in model.Vertices)
                w.WriteLine(FormattableString.Invariant(
                    $"v {v.X:0.######} {v.Y:0.######} {v.Z:0.######}"));
            foreach (var f in model.Faces)
                w.WriteLine("f " + string.Join(' ',
                    f.VertexIndices.Select(i => i + 1)));
            w.Flush();
        }
    }

    public static class ObjImporter
    {
        public static Polyhedron Load(string path)
        {
            using var s = File.OpenRead(path);
            return Load(s);
        }

        public static Polyhedron Load(Stream stream)
        {
            var model = new Polyhedron();
            using var r = new StreamReader(stream);
            while (!r.EndOfStream)
            {
                var line = r.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts[0] == "v" && parts.Length >= 4)
                {
                    model.Vertices.Add(new Point3(
                        Parse(parts[1]), Parse(parts[2]), Parse(parts[3])));
                }
                else if (parts[0] == "f" && parts.Length >= 4)
                {
                    var idx = new int[parts.Length - 1];
                    for (int i = 1; i < parts.Length; i++)
                    {
                        int v = int.Parse(parts[i].Split('/')[0],
                            CultureInfo.InvariantCulture);
                        idx[i - 1] = v > 0 ? v - 1 : model.Vertices.Count + v;
                    }
                    model.Faces.Add(new Polygon(idx));
                }
            }
            if (model.Vertices.Count == 0)
                throw new InvalidDataException("В OBJ-файле не найдено вершин.");
            return model;
        }

        private static double Parse(string s) =>
            double.Parse(s, CultureInfo.InvariantCulture);
    }
}