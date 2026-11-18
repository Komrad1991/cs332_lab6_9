using Lab6.Geometry;
using Lab6.IO;

namespace Lab7.Services;

public sealed record ModelStats(int Vertices, int Faces, double Radius, Point3 Center);

public static class ModelFileService
{
    public static ModelStats Analyze(Polyhedron m)
    {
        var c = m.Center();
        double r = m.Vertices.Count == 0 ? 0 : m.Vertices.Max(v => Point3.Distance(c, v));
        return new ModelStats(m.Vertices.Count, m.Faces.Count, r, c);
    }

    public static Polyhedron LoadObj(Stream s) => ObjImporter.Load(s);
    public static Polyhedron LoadObj(string path) => ObjImporter.Load(path);

    public static void SaveObj(Polyhedron m, Stream s, string? comment = null)
    {
        using var w = new StreamWriter(s, leaveOpen: true);
        w.WriteLine($"# {comment ?? "Lab7 export"}");
        var st = Analyze(m);
        w.WriteLine($"# vertices={st.Vertices} faces={st.Faces} radius={st.Radius:0.####}");
        foreach (var v in m.Vertices)
            w.WriteLine(FormattableString.Invariant($"v {v.X:0.######} {v.Y:0.######} {v.Z:0.######}"));
        foreach (var f in m.Faces)
            w.WriteLine("f " + string.Join(' ', f.VertexIndices.Select(i => i + 1)));
        w.Flush();
    }

    public static string Describe(ModelStats st) =>
        $"Вершин: {st.Vertices}, граней: {st.Faces}, радиус: {st.Radius:0.###}";
}