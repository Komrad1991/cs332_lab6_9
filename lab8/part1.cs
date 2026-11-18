using Lab6.Geometry;

namespace Lab8.Culling;

public static class BackfaceCuller
{
    /// <summary>Вектор обзора — направление, куда смотрит камера (из камеры в сцену).</summary>
    public static Point3 DefaultViewDir => new Point3(0, 0, -1);

    /// <summary>Проверка: видна ли грань с учётом нормали и вектора обзора.</summary>
    public static bool IsVisible(Polygon f, IReadOnlyList<Point3> verts, Point3 viewDir)
    {
        var n = f.Normal(verts);
        var center = f.Center(verts);
        // Если нормаль смотрит "от объекта наружу" и на камеру — грань лицевая
        // Для параллельной проекции: лицо видно, если dot(n, viewDir) < 0 (нормаль против обзора)
        return Point3.Dot(n, viewDir) < -1e-6;
    }

    /// <summary>Возвращает только лицевые грани.</summary>
    public static IEnumerable<Polygon> Cull(Polyhedron model, Point3 viewDir) =>
        model.Faces.Where(f => IsVisible(f, model.Vertices, viewDir));

    /// <summary>Нормаль грани, направленная наружу от центра модели.</summary>
    public static Point3 OutwardNormal(Polygon f, IReadOnlyList<Point3> verts, Point3 modelCenter)
    {
        var n = f.Normal(verts);
        var fc = f.Center(verts);
        return Point3.Dot(n, fc - modelCenter) < 0 ? n.Negated() : n;
    }
}

public static class NormalComputer
{
    /// <summary>Вычисление нормали в каждой вершине усреднением нормалей смежных граней.</summary>
    public static Point3[] VertexNormals(Polyhedron m)
    {
        var result = new Point3[m.Vertices.Count];
        foreach (var f in m.Faces)
        {
            var n = f.Normal(m.Vertices);
            foreach (var i in f.VertexIndices) result[i] += n;
        }
        for (int i = 0; i < result.Length; i++) result[i] = result[i].Normalized();
        return result;
    }
}