using Lab6.Geometry;
using Lab6.Rendering;

namespace Lab8.Cameras;

public sealed class CameraObject
{
    public Point3 Position { get; set; } = new(0, 0, 4);
    public Point3 Target { get; set; } = Point3.Zero;
    public Point3 Up { get; set; } = new(0, 1, 0);
    public double FovDegrees { get; set; } = 60;
    public double Near { get; set; } = 0.1;
    public double Far { get; set; } = 100;
    public ProjectionMode Projection { get; set; } = ProjectionMode.Perspective;

    public Point3 ViewDirection
    {
        get
        {
            var d = Target - Position;
            double len2 = d.X * d.X + d.Y * d.Y + d.Z * d.Z;
            return len2 < 1e-12 ? new Point3(0, 0, -1) : d.Normalized();
        }
    }

    public Matrix4 ViewMatrix()
    {
        var z = (Position - Target).Normalized();
        var x = Point3.Cross(Up, z).Normalized();
        var y = Point3.Cross(z, x);
        return new Matrix4(
            x.X, y.X, z.X, 0,
            x.Y, y.Y, z.Y, 0,
            x.Z, y.Z, z.Z, 0,
            -Point3.Dot(x, Position), -Point3.Dot(y, Position), -Point3.Dot(z, Position), 1);
    }

    public Matrix4 PerspectiveMatrix()
    {
        double f = 1.0 / Math.Tan(FovDegrees * Math.PI / 360);
        double range = Near - Far;
        return new Matrix4(
            f, 0, 0, 0,
            0, f, 0, 0,
            0, 0, (Near + Far) / range, -1,
            0, 0, 2 * Near * Far / range, 0);
    }

    /// <summary>
    /// Орбита вокруг Target в плоскости XZ (ось Y — вверх).
    /// Сохраняет текущую высоту камеры (Position.Y).
    /// </summary>
    public void Orbit(double angleRad, double radius)
    {
        double dx = Position.X - Target.X;
        double dz = Position.Z - Target.Z;
        double a = Math.Atan2(dz, dx) + angleRad;
        Position = new Point3(
            Target.X + radius * Math.Cos(a),
            Position.Y,
            Target.Z + radius * Math.Sin(a));
    }

    /// <summary>
    /// Проекция в NDC: [-1, 1] для X/Y, Z — монотонно убывает с приближением к камере.
    /// Работает и для перспективной, и для параллельной проекции.
    /// </summary>
    public (double x, double y, double z) ProjectNdc(Point3 world)
    {
        var v = ViewMatrix().TransformPoint(world);
        if (Projection == ProjectionMode.Perspective)
        {
            var p = PerspectiveMatrix().TransformPoint(v);
            return (p.X, p.Y, p.Z);
        }
        // Параллельная (ортографическая) проекция: боковые координаты в пространстве камеры
        return (v.X, v.Y, v.Z);
    }

    public (double x, double y, double z) Project(Point3 world) => ProjectNdc(world);
}