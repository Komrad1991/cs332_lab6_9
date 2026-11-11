//   1. Правильные многогранники (тетраэдр, гексаэдр, октаэдр, икосаэдр,
//      додекаэдр).
//   2. Аффинные преобразования МАТРИЦАМИ (относительно начала координат):
//      смещение, поворот, масштаб.
// Point3, Polygon, Polyhedron, Matrix4
using Lab6.Geometry;
using System.Globalization;

namespace Lab6.Geometry
{
    //точка в трехмерном пространстве
    public readonly record struct Point3(double X, double Y, double Z)
    {
        public static Point3 Zero => new(0, 0, 0);

        public static Point3 operator +(Point3 a, Point3 b) =>
            new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Point3 operator -(Point3 a, Point3 b) =>
            new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Point3 operator *(Point3 a, double k) =>
            new(a.X * k, a.Y * k, a.Z * k);

        //расстояние от точки до точкиыы
        public static double Distance(Point3 a, Point3 b)
        {
            var d = a - b;
            return Math.Sqrt(d.X * d.X + d.Y * d.Y + d.Z * d.Z);
        }

        //векторное произведение
        public static Point3 Cross(Point3 a, Point3 b) => new(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X);

        //скалярное
        public static double Dot(Point3 a, Point3 b) =>
            a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        //нормализация (в единичный)
        public Point3 Normalized()
        {
            double len = Math.Sqrt(X * X + Y * Y + Z * Z);
            return len < 1e-12 ? Zero : new Point3(X / len, Y / len, Z / len);
        }
        //противоположный
        public Point3 Negated() => new(-X, -Y, -Z);

        public override string ToString() =>
            $"({X:0.###}; {Y:0.###}; {Z:0.###})";
    }

    //грань
    public sealed class Polygon
    {
        public int[] VertexIndices { get; }

        public Polygon(params int[] vertexIndices)
        {
            if (vertexIndices.Length < 3)
                throw new ArgumentException("Грань должна содержать минимум три вершины.");
            VertexIndices = vertexIndices.ToArray();
        }

        //нормаль грани
        public Point3 Normal(IReadOnlyList<Point3> vertices)
        {
            var a = vertices[VertexIndices[0]];
            var b = vertices[VertexIndices[1]];
            var c = vertices[VertexIndices[2]];
            return Point3.Cross(b - a, c - a).Normalized();
        }

        //центроид
        public Point3 Center(IReadOnlyList<Point3> vertices)
        {
            double x = 0, y = 0, z = 0;
            foreach (int i in VertexIndices)
            {
                var v = vertices[i];
                x += v.X; y += v.Y; z += v.Z;
            }
            int n = VertexIndices.Length;
            return new Point3(x / n, y / n, z / n);
        }
    }

    public sealed class Polyhedron
    {
        public List<Point3> Vertices { get; } = new();
        public List<Polygon> Faces { get; } = new();

        //центроид всех вершин
        public Point3 Center()
        {
            if (Vertices.Count == 0) return Point3.Zero;
            double x = 0, y = 0, z = 0;
            foreach (var v in Vertices) { x += v.X; y += v.Y; z += v.Z; }
            int n = Vertices.Count;
            return new Point3(x / n, y / n, z / n);
        }

        //применить преобразование афинное ко всем вершинам
        public void Apply(Matrix4 matrix)
        {
            for (int i = 0; i < Vertices.Count; i++)
                Vertices[i] = matrix.TransformPoint(Vertices[i]);
        }

        //копирка
        public Polyhedron Clone()
        {
            var copy = new Polyhedron();
            copy.Vertices.AddRange(Vertices);
            copy.Faces.AddRange(Faces.Select(f => new Polygon(f.VertexIndices)));
            return copy;
        }
    }

    /// <summary>Матрица аффинного преобразования 4x4. Вектор-строка: p' = p * M.</summary>
    public readonly struct Matrix4
    {
        public readonly double M11, M12, M13, M14;
        public readonly double M21, M22, M23, M24;
        public readonly double M31, M32, M33, M34;
        public readonly double M41, M42, M43, M44;

        public Matrix4(
            double m11, double m12, double m13, double m14,
            double m21, double m22, double m23, double m24,
            double m31, double m32, double m33, double m34,
            double m41, double m42, double m43, double m44)
        {
            M11 = m11; M12 = m12; M13 = m13; M14 = m14;
            M21 = m21; M22 = m22; M23 = m23; M24 = m24;
            M31 = m31; M32 = m32; M33 = m33; M34 = m34;
            M41 = m41; M42 = m42; M43 = m43; M44 = m44;
        }


        //еденичка
        public static Matrix4 Identity => new(
            1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);

        //сдвиг
        public static Matrix4 Translation(double tx, double ty, double tz) => new(
            1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0,
            tx, ty, tz, 1);

        //размер
        public static Matrix4 Scale(double sx, double sy, double sz) => new(
            sx, 0, 0, 0, 0, sy, 0, 0, 0, 0, sz, 0, 0, 0, 0, 1);

        //поворот
        public static Matrix4 RotationX(double r)
        {
            double c = Math.Cos(r), s = Math.Sin(r);
            return new Matrix4(
                1, 0, 0, 0, 0, c, s, 0, 0, -s, c, 0, 0, 0, 0, 1);
        }
        //поворот
        public static Matrix4 RotationY(double r)
        {
            double c = Math.Cos(r), s = Math.Sin(r);
            return new Matrix4(
                c, 0, -s, 0, 0, 1, 0, 0, s, 0, c, 0, 0, 0, 0, 1);
        }
        //поворот
        public static Matrix4 RotationZ(double r)
        {
            double c = Math.Cos(r), s = Math.Sin(r);
            return new Matrix4(
                c, s, 0, 0, -s, c, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
        }
        //отражение через плоскость
        public static Matrix4 Reflection(string plane) => plane.Trim().ToUpperInvariant() switch
        {
            "XY" => Scale(1, 1, -1),
            "XZ" => Scale(1, -1, 1),
            "YZ" => Scale(-1, 1, 1),
            _ => throw new ArgumentException("Плоскость должна быть XY, XZ или YZ.")
        };

        //обратная матрица
        public Matrix4 Inverse()
        {
            double a = M11, b = M12, c = M13;
            double d = M21, e = M22, f = M23;
            double g = M31, h = M32, i = M33;

            double det = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
            if (Math.Abs(det) < 1e-12)
                throw new InvalidOperationException("Матрица необратима.");

            double k = 1.0 / det;
            var rotInv = new Matrix4(
                 (e * i - f * h) * k, -(b * i - c * h) * k, (b * f - c * e) * k, 0,
                -(d * i - f * g) * k, (a * i - c * g) * k, -(a * f - c * d) * k, 0,
                 (d * h - e * g) * k, -(a * h - b * g) * k, (a * e - b * d) * k, 0,
                 0, 0, 0, 1);

            var tr = new Point3(M41, M42, M43);
            var invTr = rotInv.TransformPoint(tr.Negated());

            return new Matrix4(
                rotInv.M11, rotInv.M12, rotInv.M13, 0,
                rotInv.M21, rotInv.M22, rotInv.M23, 0,
                rotInv.M31, rotInv.M32, rotInv.M33, 0,
                invTr.X, invTr.Y, invTr.Z, 1);
        }

        //перемножение двух
        public static Matrix4 operator *(Matrix4 a, Matrix4 b) => new(
            a.M11 * b.M11 + a.M12 * b.M21 + a.M13 * b.M31 + a.M14 * b.M41,
            a.M11 * b.M12 + a.M12 * b.M22 + a.M13 * b.M32 + a.M14 * b.M42,
            a.M11 * b.M13 + a.M12 * b.M23 + a.M13 * b.M33 + a.M14 * b.M43,
            a.M11 * b.M14 + a.M12 * b.M24 + a.M13 * b.M34 + a.M14 * b.M44,
            a.M21 * b.M11 + a.M22 * b.M21 + a.M23 * b.M31 + a.M24 * b.M41,
            a.M21 * b.M12 + a.M22 * b.M22 + a.M23 * b.M32 + a.M24 * b.M42,
            a.M21 * b.M13 + a.M22 * b.M23 + a.M23 * b.M33 + a.M24 * b.M43,
            a.M21 * b.M14 + a.M22 * b.M24 + a.M23 * b.M34 + a.M24 * b.M44,
            a.M31 * b.M11 + a.M32 * b.M21 + a.M33 * b.M31 + a.M34 * b.M41,
            a.M31 * b.M12 + a.M32 * b.M22 + a.M33 * b.M32 + a.M34 * b.M42,
            a.M31 * b.M13 + a.M32 * b.M23 + a.M33 * b.M33 + a.M34 * b.M43,
            a.M31 * b.M14 + a.M32 * b.M24 + a.M33 * b.M34 + a.M34 * b.M44,
            a.M41 * b.M11 + a.M42 * b.M21 + a.M43 * b.M31 + a.M44 * b.M41,
            a.M41 * b.M12 + a.M42 * b.M22 + a.M43 * b.M32 + a.M44 * b.M42,
            a.M41 * b.M13 + a.M42 * b.M23 + a.M43 * b.M33 + a.M44 * b.M43,
            a.M41 * b.M14 + a.M42 * b.M24 + a.M43 * b.M34 + a.M44 * b.M44);


        public Point3 TransformPoint(Point3 p)
        {
            double x = p.X * M11 + p.Y * M21 + p.Z * M31 + M41;
            double y = p.X * M12 + p.Y * M22 + p.Z * M32 + M42;
            double z = p.X * M13 + p.Y * M23 + p.Z * M33 + M43;
            double w = p.X * M14 + p.Y * M24 + p.Z * M34 + M44;
            return Math.Abs(w) < 1e-12
                ? new Point3(x, y, z)
                : new Point3(x / w, y / w, z / w);
        }
    }
}

namespace Lab6.Builders
{
    public static class PlatonicSolids
    {
        private static readonly double Phi = (1 + Math.Sqrt(5)) / 2;

        //тетраэдр
        public static Polyhedron Tetrahedron()
        {
            var m = new Polyhedron();
            m.Vertices.AddRange(new[]
            {
                new Point3( 1,  1,  1), new Point3(-1, -1,  1),
                new Point3(-1,  1, -1), new Point3( 1, -1, -1)
            });
            m.Faces.AddRange(new[]
            {
                new Polygon(0, 1, 2), new Polygon(0, 3, 1),
                new Polygon(0, 2, 3), new Polygon(1, 3, 2)
            });
            return m;
        }

        //гексаэдр
        public static Polyhedron Hexahedron(double size = 1)
        {
            var m = new Polyhedron();
            m.Vertices.AddRange(new[]
            {
                new Point3(-size,-size,-size), new Point3( size,-size,-size),
                new Point3( size, size,-size), new Point3(-size, size,-size),
                new Point3(-size,-size, size), new Point3( size,-size, size),
                new Point3( size, size, size), new Point3(-size, size, size)
            });
            m.Faces.AddRange(new[]
            {
                new Polygon(0,1,2,3), new Polygon(4,5,6,7),
                new Polygon(0,1,5,4), new Polygon(2,3,7,6),
                new Polygon(0,3,7,4), new Polygon(1,2,6,5)
            });
            return m;
        }
        //октаэдр
        public static Polyhedron Octahedron(double size = 1)
        {
            var m = new Polyhedron();
            m.Vertices.AddRange(new[]
            {
                new Point3( size,0,0), new Point3(-size,0,0),
                new Point3(0, size,0), new Point3(0,-size,0),
                new Point3(0,0, size), new Point3(0,0,-size)
            });
            m.Faces.AddRange(new[]
            {
                new Polygon(0,2,4), new Polygon(2,1,4),
                new Polygon(1,3,4), new Polygon(3,0,4),
                new Polygon(2,0,5), new Polygon(1,2,5),
                new Polygon(3,1,5), new Polygon(0,3,5)
            });
            return m;
        }

        //икосаэдр
        public static Polyhedron Icosahedron(double size = 1)
        {
            double phi = Phi;
            double[][] raw =
            {
                new[]{-1.0, phi, 0}, new[]{ 1.0, phi, 0},
                new[]{-1.0,-phi, 0}, new[]{ 1.0,-phi, 0},
                new[]{ 0.0,-1, phi}, new[]{ 0.0, 1, phi},
                new[]{ 0.0,-1,-phi}, new[]{ 0.0, 1,-phi},
                new[]{ phi, 0,-1}, new[]{ phi, 0, 1},
                new[]{-phi, 0,-1}, new[]{-phi, 0, 1}
            };
            var m = new Polyhedron();
            foreach (var v in raw)
                m.Vertices.Add(new Point3(v[0], v[1], v[2]).Normalized() * size);

            int[][] f =
            {
                new[]{ 0,11, 5}, new[]{ 0, 5, 1}, new[]{ 0, 1, 7},
                new[]{ 0, 7,10}, new[]{ 0,10,11}, new[]{ 1, 5, 9},
                new[]{ 5,11, 4}, new[]{11,10, 2}, new[]{10, 7, 6},
                new[]{ 7, 1, 8}, new[]{ 3, 9, 4}, new[]{ 3, 4, 2},
                new[]{ 3, 2, 6}, new[]{ 3, 6, 8}, new[]{ 3, 8, 9},
                new[]{ 4, 9, 5}, new[]{ 2, 4,11}, new[]{ 6, 2,10},
                new[]{ 8, 6, 7}, new[]{ 9, 8, 1}
            };
            foreach (var face in f) m.Faces.Add(new Polygon(face));
            return m;
        }
        //додекаэдр
        public static Polyhedron Dodecahedron(double size = 1)
        {
            if (!double.IsFinite(size) || size <= 0)
                throw new ArgumentException("Size must be positive.");

            double phi = Phi, inv = 1.0 / phi;
            var m = new Polyhedron();

            foreach (double x in new[] { -1.0, 1.0 })
                foreach (double y in new[] { -1.0, 1.0 })
                    foreach (double z in new[] { -1.0, 1.0 })
                        m.Vertices.Add(new Point3(x, y, z));

            foreach (double a in new[] { -1.0, 1.0 })
                foreach (double b in new[] { -1.0, 1.0 })
                    m.Vertices.Add(new Point3(0, a * inv, b * phi));

            foreach (double a in new[] { -1.0, 1.0 })
                foreach (double b in new[] { -1.0, 1.0 })
                    m.Vertices.Add(new Point3(a * inv, b * phi, 0));

            foreach (double a in new[] { -1.0, 1.0 })
                foreach (double b in new[] { -1.0, 1.0 })
                    m.Vertices.Add(new Point3(a * phi, 0, b * inv));

            double scale = size / Math.Sqrt(3);
            for (int i = 0; i < m.Vertices.Count; i++)
                m.Vertices[i] = m.Vertices[i] * scale;

            const double eps = 1e-8;
            int count = m.Vertices.Count;
            var done = new HashSet<string>();
            var center = m.Center();

            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    for (int k = j + 1; k < count; k++)
                    {
                        var a = m.Vertices[i]; var b = m.Vertices[j]; var c = m.Vertices[k];
                        var n = Point3.Cross(b - a, c - a);
                        if (Point3.Dot(n, n) < eps * eps) continue;
                        n = n.Normalized();

                        bool pos = false, neg = false;
                        for (int t = 0; t < count; t++)
                        {
                            double dd = Point3.Dot(n, m.Vertices[t] - a);
                            if (dd > eps) pos = true;
                            if (dd < -eps) neg = true;
                        }
                        if (pos && neg) continue;

                        var idxs = new List<int>();
                        for (int t = 0; t < count; t++)
                            if (Math.Abs(Point3.Dot(n, m.Vertices[t] - a)) < eps) idxs.Add(t);
                        if (idxs.Count != 5) continue;

                        string key = string.Join(",", idxs.OrderBy(x => x));
                        if (!done.Add(key)) continue;

                        var fc = Point3.Zero;
                        foreach (var ii in idxs) fc += m.Vertices[ii];
                        fc = fc * (1.0 / 5);

                        var outw = (fc - center).Normalized();
                        var reference = Math.Abs(outw.Z) < 0.9
                            ? new Point3(0, 0, 1) : new Point3(0, 1, 0);
                        var u = Point3.Cross(reference, outw).Normalized();
                        var v = Point3.Cross(outw, u).Normalized();

                        var ord = idxs.OrderBy(ii =>
                        {
                            var dir = m.Vertices[ii] - fc;
                            return Math.Atan2(Point3.Dot(dir, v), Point3.Dot(dir, u));
                        }).ToArray();

                        var fn = Point3.Cross(
                            m.Vertices[ord[1]] - m.Vertices[ord[0]],
                            m.Vertices[ord[2]] - m.Vertices[ord[0]]);
                        if (Point3.Dot(fn, outw) < 0) Array.Reverse(ord);

                        m.Faces.Add(new Polygon(ord));
                    }
            return m;
        }
    }
}

namespace Lab6.Transforms
{
    public static partial class ModelTransforms
    {
        // ---------- Задание 2: аффинные преобразования (относительно начала) ----------
        public static Matrix4 Translate(double x, double y, double z) =>
            Matrix4.Translation(x, y, z);

        //перевод из градусов в радианы
        public static Matrix4 RotateAroundOrigin(string axis, double degrees)
        {
            double r = degrees * Math.PI / 180;
            return axis.Trim().ToUpperInvariant() switch
            {
                "X" => Matrix4.RotationX(r),
                "Y" => Matrix4.RotationY(r),
                "Z" => Matrix4.RotationZ(r),
                _ => throw new ArgumentException("Axis must be X, Y or Z")
            };
        }
        //масштаб
        public static Matrix4 ScaleAroundOrigin(double factor) =>
            Matrix4.Scale(factor, factor, factor);
    }
}