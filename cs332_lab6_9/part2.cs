// ============================================================================
// part2.cs — Задания 3-4 (+ утилиты: RevolutionBuilder, SurfaceBuilder,
// FunctionParser — они не вызывают методы-задания из других частей).
//   3. Отражение относительно координатной плоскости (МАТРИЦА).
//   4. Масштабирование многогранника относительно СВОЕГО ЦЕНТРА (МАТРИЦА).
// ============================================================================
using System.Globalization;
using Lab6.Geometry;

namespace Lab6.Builders
{
    public static class RevolutionBuilder
    {
        public static Polyhedron Build(
            IReadOnlyList<Point3> profile, string axis, int segments)
        {
            if (profile.Count < 2)
                throw new ArgumentException("Образующая должна содержать минимум две точки.");
            if (segments < 3)
                throw new ArgumentException("Количество разбиений должно быть не меньше трёх.");

            var model = new Polyhedron();
            double step = 2 * Math.PI / segments;

            for (int ring = 0; ring <= segments; ring++)
            {
                double angle = ring * step;
                double cos = Math.Cos(angle), sin = Math.Sin(angle);

                foreach (var p in profile)
                {
                    var r = axis.Trim().ToUpperInvariant() switch
                    {
                        "X" => new Point3(p.X, p.Y * cos - p.Z * sin, p.Y * sin + p.Z * cos),
                        "Y" => new Point3(p.X * cos + p.Z * sin, p.Y, -p.X * sin + p.Z * cos),
                        "Z" => new Point3(p.X * cos - p.Y * sin, p.X * sin + p.Y * cos, p.Z),
                        _ => throw new ArgumentException("Ось должна быть X, Y или Z.")
                    };
                    model.Vertices.Add(r);
                }
            }

            int pc = profile.Count;
            for (int ring = 0; ring < segments; ring++)
            {
                int cur = ring * pc, next = (ring + 1) * pc;
                for (int i = 0; i < pc - 1; i++)
                    model.Faces.Add(new Polygon(
                        cur + i, cur + i + 1, next + i + 1, next + i));
            }
            return model;
        }
    }

    public static class SurfaceBuilder
    {
        public static Polyhedron Build(
            Func<double, double, double> f,
            double x0, double x1, double y0, double y1, int seg)
        {
            if (seg < 2)
                throw new ArgumentException("Количество разбиений должно быть не меньше двух.");

            var model = new Polyhedron();
            double dx = (x1 - x0) / seg, dy = (y1 - y0) / seg;

            for (int iy = 0; iy <= seg; iy++)
                for (int ix = 0; ix <= seg; ix++)
                {
                    double x = x0 + ix * dx, y = y0 + iy * dy;
                    model.Vertices.Add(new Point3(x, y, f(x, y)));
                }

            int len = seg + 1;
            for (int iy = 0; iy < seg; iy++)
                for (int ix = 0; ix < seg; ix++)
                {
                    int a = iy * len + ix;
                    model.Faces.Add(new Polygon(a, a + 1, a + len + 1, a + len));
                }
            return model;
        }
    }
}

namespace Lab6.ViewModels
{
    public static class FunctionParser
    {
        public static Func<double, double, double> Parse(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                throw new ArgumentException("Функция не задана.");

            var p = new Parser(Tokenize(expression));
            var fn = p.ParseExpression();
            if (p.Current is not null)
                throw new FormatException($"Некорректный символ: {p.Current}.");
            return fn;
        }

        private static List<string> Tokenize(string expr)
        {
            var tokens = new List<string>();
            int i = 0;
            while (i < expr.Length)
            {
                char c = expr[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                if (char.IsLetter(c))
                {
                    int s = i;
                    while (i < expr.Length && char.IsLetterOrDigit(expr[i])) i++;
                    tokens.Add(expr[s..i].ToLowerInvariant());
                    continue;
                }
                if (char.IsDigit(c) || c == '.')
                {
                    int s = i;
                    while (i < expr.Length && (char.IsDigit(expr[i]) || expr[i] == '.')) i++;
                    tokens.Add(expr[s..i]);
                    continue;
                }
                if ("+-*/^(),".Contains(c)) { tokens.Add(c.ToString()); i++; continue; }
                throw new FormatException($"Недопустимый символ: {c}.");
            }
            return tokens;
        }

        private sealed class Parser
        {
            private readonly List<string> _t;
            private int _pos;
            public Parser(List<string> t) => _t = t;
            public string? Current => _pos < _t.Count ? _t[_pos] : null;

            public Func<double, double, double> ParseExpression()
            {
                var l = ParseTerm();
                while (Current is "+" or "-")
                {
                    string op = Current!; Next();
                    var r = ParseTerm();
                    var a = l; var b = r;
                    l = op == "+"
                        ? (x, y) => a(x, y) + b(x, y)
                        : (x, y) => a(x, y) - b(x, y);
                }
                return l;
            }

            private Func<double, double, double> ParseTerm()
            {
                var l = ParseFactor();
                while (Current is "*" or "/")
                {
                    string op = Current!; Next();
                    var r = ParseFactor();
                    var a = l; var b = r;
                    l = op == "*"
                        ? (x, y) => a(x, y) * b(x, y)
                        : (x, y) => a(x, y) / b(x, y);
                }
                return l;
            }

            private Func<double, double, double> ParseFactor()
            {
                var v = ParseUnary();
                if (Current == "^")
                {
                    Next();
                    var e = ParseFactor();
                    var a = v; var b = e;
                    return (x, y) => Math.Pow(a(x, y), b(x, y));
                }
                return v;
            }

            private Func<double, double, double> ParseUnary()
            {
                if (Current == "-") { Next(); var v = ParseUnary(); var a = v; return (x, y) => -a(x, y); }
                if (Current == "+") { Next(); return ParseUnary(); }
                return ParsePrimary();
            }

            private Func<double, double, double> ParsePrimary()
            {
                if (Current is null)
                    throw new FormatException("Неожиданный конец выражения.");
                string tk = Current!;

                if (tk == "(")
                {
                    Next();
                    var inner = ParseExpression();
                    if (Current != ")") throw new FormatException("Ожидалась )");
                    Next();
                    return inner;
                }
                if (double.TryParse(tk, NumberStyles.Float, CultureInfo.InvariantCulture, out double n))
                { Next(); return (_, _) => n; }
                if (tk == "x") { Next(); return (x, _) => x; }
                if (tk == "y") { Next(); return (_, y) => y; }
                if (tk == "pi") { Next(); return (_, _) => Math.PI; }
                if (tk == "e") { Next(); return (_, _) => Math.E; }

                if (IsFn(tk))
                {
                    Next();
                    if (Current != "(") throw new FormatException($"После {tk} нужна (");
                    Next();
                    var arg = ParseExpression();
                    if (Current != ")") throw new FormatException("Ожидалась )");
                    Next();
                    var a = arg;
                    return tk switch
                    {
                        "sin" => (x, y) => Math.Sin(a(x, y)),
                        "cos" => (x, y) => Math.Cos(a(x, y)),
                        "tan" => (x, y) => Math.Tan(a(x, y)),
                        "asin" => (x, y) => Math.Asin(a(x, y)),
                        "acos" => (x, y) => Math.Acos(a(x, y)),
                        "atan" => (x, y) => Math.Atan(a(x, y)),
                        "sqrt" => (x, y) => Math.Sqrt(a(x, y)),
                        "abs" => (x, y) => Math.Abs(a(x, y)),
                        "log" => (x, y) => Math.Log(a(x, y)),
                        "exp" => (x, y) => Math.Exp(a(x, y)),
                        _ => throw new FormatException($"Неизвестная функция: {tk}.")
                    };
                }
                throw new FormatException($"Неизвестный элемент: {tk}.");
            }

            private void Next() { if (_pos < _t.Count) _pos++; }
            private static bool IsFn(string t) => t is
                "sin" or "cos" or "tan" or "asin" or "acos" or "atan" or
                "sqrt" or "abs" or "log" or "exp";
        }
    }
}

namespace Lab6.Transforms
{
    public static partial class ModelTransforms
    {
        // ---------- Задание 3: отражение относительно плоскости ----------
        public static Matrix4 Reflect(string plane) => Matrix4.Reflection(plane);

        // ---------- Задание 4: масштаб относительно центра ----------
        // Композиция T(-c) * S * T(c).
        public static Matrix4 ScaleAroundCenter(Polyhedron model, double factor)
        {
            var c = model.Center();
            return Matrix4.Translation(-c.X, -c.Y, -c.Z) *
                   Matrix4.Scale(factor, factor, factor) *
                   Matrix4.Translation(c.X, c.Y, c.Z);
        }
    }
}