using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lab6.Builders;
using Lab6.Geometry;
using Lab6.Rendering;
using Lab8.Cameras;
using Lab8.Culling;
using Lab8.Rendering;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Application = Avalonia.Application;
using Size = Avalonia.Size;
using Point = Avalonia.Point;
using Vector = Avalonia.Vector;

namespace Lab8
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args) =>
            AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace()
                .StartWithClassicDesktopLifetime(args);
    }

    public class App : Application
    {
        public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }

    public sealed class RelayCommand : ICommand
    {
        private readonly Action _a;
        public RelayCommand(Action a) => _a = a;

        // По контракту ICommand событие должно существовать, но нам оно не нужно.
        // Пустые add/remove подавляют предупреждение CS0067.
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _a();
    }

    public enum RenderMode { BackfaceCullOnly, ZBuffer }

    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private Polyhedron _model = PlatonicSolids.Hexahedron();
        private string _status = "Куб";
        private RenderMode _mode = RenderMode.BackfaceCullOnly;
        private bool _animate;
        private bool _orbitCam;
        private bool _showSecondObject;
        private double _angle;
        private Point3 _viewDir = new(0, 0, -1);

        public CameraObject Cam { get; } = new();

        public Polyhedron Model { get => _model; private set { _model = value; OnP(); } }
        public string Status { get => _status; private set { _status = value; OnP(); } }
        public RenderMode Mode { get => _mode; set { _mode = value; OnP(); RequestRender?.Invoke(); } }
        public bool Animate { get => _animate; set { _animate = value; OnP(); } }
        public bool OrbitCam { get => _orbitCam; set { _orbitCam = value; OnP(); } }
        public double Angle { get => _angle; set { _angle = value; OnP(); } }
        public bool ShowSecondObject { get => _showSecondObject; set { _showSecondObject = value; OnP(); RequestRender?.Invoke(); } }

        // ===== Вектор обзора (управляет Cam.Target) =====
        public double ViewDirX
        {
            get => _viewDir.X;
            set { _viewDir = new Point3(value, _viewDir.Y, _viewDir.Z); UpdateTarget(); OnP(); RequestRender?.Invoke(); }
        }
        public double ViewDirY
        {
            get => _viewDir.Y;
            set { _viewDir = new Point3(_viewDir.X, value, _viewDir.Z); UpdateTarget(); OnP(); RequestRender?.Invoke(); }
        }
        public double ViewDirZ
        {
            get => _viewDir.Z;
            set { _viewDir = new Point3(_viewDir.X, _viewDir.Y, value); UpdateTarget(); OnP(); RequestRender?.Invoke(); }
        }

        // ===== Позиция камеры =====
        public double CamX
        {
            get => Cam.Position.X;
            set { Cam.Position = new Point3(value, Cam.Position.Y, Cam.Position.Z); UpdateTarget(); OnP(); RequestRender?.Invoke(); }
        }
        public double CamY
        {
            get => Cam.Position.Y;
            set { Cam.Position = new Point3(Cam.Position.X, value, Cam.Position.Z); UpdateTarget(); OnP(); RequestRender?.Invoke(); }
        }
        public double CamZ
        {
            get => Cam.Position.Z;
            set { Cam.Position = new Point3(Cam.Position.X, Cam.Position.Y, value); UpdateTarget(); OnP(); RequestRender?.Invoke(); }
        }

        // ===== Тип проекции =====
        public bool UsePerspective
        {
            get => Cam.Projection == ProjectionMode.Perspective;
            set
            {
                Cam.Projection = value ? ProjectionMode.Perspective : ProjectionMode.Axonometric;
                Status = value ? "Перспективная проекция" : "Параллельная проекция";
                OnP(); RequestRender?.Invoke();
            }
        }

        public ICommand LoadCubeCmd { get; }
        public ICommand LoadSphereCmd { get; }
        public ICommand ResetAngleCmd { get; }
        public ICommand ModeCullCmd { get; }
        public ICommand ModeZCmd { get; }
        public ICommand ToggleAnimateCmd { get; }
        public ICommand ToggleOrbitCmd { get; }
        public ICommand PerspCmd { get; }
        public ICommand ParallelCmd { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? RequestRender;

        public MainViewModel()
        {
            UpdateTarget();

            LoadCubeCmd = new RelayCommand(() =>
            {
                Model = PlatonicSolids.Hexahedron();
                Status = "Куб";
            });
            LoadSphereCmd = new RelayCommand(() =>
            {
                Model = SphereBuilder.Build(1.0, 12, 16);
                Status = "Шар";
            });
            ResetAngleCmd = new RelayCommand(() => { Angle = 0; });
            ModeCullCmd = new RelayCommand(() => { Mode = RenderMode.BackfaceCullOnly; Status = "Отсечение нелицевых"; });
            ModeZCmd = new RelayCommand(() => { Mode = RenderMode.ZBuffer; Status = "Z-буфер"; });
            ToggleAnimateCmd = new RelayCommand(() => { Animate = !Animate; Status = Animate ? "Анимация ВКЛ" : "Анимация ВЫКЛ"; });
            ToggleOrbitCmd = new RelayCommand(() => { OrbitCam = !OrbitCam; Status = OrbitCam ? "Орбита камеры ВКЛ" : "Орбита камеры ВЫКЛ"; });
            PerspCmd = new RelayCommand(() => { UsePerspective = true; });
            ParallelCmd = new RelayCommand(() => { UsePerspective = false; });

            var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += (_, _) =>
            {
                bool dirty = false;
                if (Animate) { Angle += 2; dirty = true; }

                if (OrbitCam)
                {
                    Cam.Target = new Point3(0, 0, 0);
                    double radius = Math.Sqrt(Cam.Position.X * Cam.Position.X + Cam.Position.Z * Cam.Position.Z);
                    if (radius < 0.1) radius = 4;
                    Cam.Orbit(0.05, radius);
                    _viewDir = new Point3(
                        Cam.Target.X - Cam.Position.X,
                        Cam.Target.Y - Cam.Position.Y,
                        Cam.Target.Z - Cam.Position.Z).Normalized();
                    OnP(nameof(ViewDirX)); OnP(nameof(ViewDirY)); OnP(nameof(ViewDirZ));
                    OnP(nameof(CamX)); OnP(nameof(CamY)); OnP(nameof(CamZ));
                    dirty = true;
                }

                if (dirty) RequestRender?.Invoke();
            };
            timer.Start();
        }

        private void UpdateTarget()
        {
            Cam.Target = new Point3(
                Cam.Position.X + _viewDir.X,
                Cam.Position.Y + _viewDir.Y,
                Cam.Position.Z + _viewDir.Z);
        }

        public Point3 ViewDir => _viewDir.Normalized();
        private void OnP([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public sealed class SceneControl : Control
    {
        private MainViewModel? _vm;
        public SceneControl() { ClipToBounds = true; }

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (_vm is not null)
            {
                _vm.PropertyChanged -= OnChange;
                _vm.RequestRender -= InvalidateVisual;
            }
            _vm = DataContext as MainViewModel;
            if (_vm is not null)
            {
                _vm.PropertyChanged += OnChange;
                _vm.RequestRender += InvalidateVisual;
            }
            InvalidateVisual();
        }
        private void OnChange(object? s, PropertyChangedEventArgs e) => InvalidateVisual();

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            if (_vm is null || Bounds.Width < 8 || Bounds.Height < 8) return;
            var w = (int)Bounds.Width; var h = (int)Bounds.Height;
            ctx.FillRectangle(Brushes.White, new Rect(0, 0, w, h));

            var models = BuildWorldModels(_vm);

            if (_vm.Mode == RenderMode.ZBuffer)
                RenderZBuffer(models, _vm, ctx, w, h);
            else
                RenderBackface(models, _vm, ctx, w, h);
        }

        private static List<Polyhedron> BuildWorldModels(MainViewModel vm)
        {
            var model = vm.Model.Clone();
            var rot = Matrix4.RotationY(vm.Angle * Math.PI / 180) * Matrix4.RotationX(0.6);
            model.Apply(rot);

            CenterAndScale(model, 1.0);

            var list = new List<Polyhedron> { model };

            if (vm.Mode == RenderMode.ZBuffer && vm.ShowSecondObject)
            {
                var second = PlatonicSolids.Hexahedron();
                second.Apply(Matrix4.RotationY(0.4) * Matrix4.RotationX(0.3));
                CenterAndScale(second, 0.55);
                second.Apply(TranslationMatrix(0.75, 0.35, 0.2));
                list.Add(second);
            }
            return list;
        }

        private static void CenterAndScale(Polyhedron m, double targetRadius)
        {
            var c = m.Center();
            double r = Math.Max(1e-9, m.Vertices.Max(p => Point3.Distance(c, p)));
            m.Apply(TranslationMatrix(-c.X, -c.Y, -c.Z));
            m.Apply(ScaleMatrix(targetRadius / r));
        }

        private static Matrix4 TranslationMatrix(double tx, double ty, double tz) =>
            new Matrix4(1, 0, 0, tx,
                        0, 1, 0, ty,
                        0, 0, 1, tz,
                        0, 0, 0, 1);

        private static Matrix4 ScaleMatrix(double s) =>
            new Matrix4(s, 0, 0, 0,
                        0, s, 0, 0,
                        0, 0, s, 0,
                        0, 0, 0, 1);

        private static void RenderBackface(List<Polyhedron> models, MainViewModel vm,
                                           DrawingContext ctx, int w, int h)
        {
            double ppu = Math.Min(w, h) * 0.45;
            var viewDir = vm.Cam.ViewDirection;
            var light = new Point3(0.4, 0.8, -1).Normalized();

            foreach (var model in models)
            {
                if (model.Vertices.Count == 0) continue;

                var screen = new Point[model.Vertices.Count];
                var depth = new double[model.Vertices.Count];
                for (int i = 0; i < model.Vertices.Count; i++)
                {
                    var (x, y, d) = vm.Cam.ProjectNdc(model.Vertices[i]);
                    screen[i] = new Point(w / 2.0 + x * ppu, h / 2.0 - y * ppu);
                    depth[i] = d;
                }

                var center = model.Center();
                var faces = model.Faces
                    .Where(f => Point3.Dot(
                        BackfaceCuller.OutwardNormal(f, model.Vertices, center), viewDir) < -1e-6)
                    .OrderByDescending(f => f.VertexIndices.Average(i => depth[i]));

                foreach (var f in faces)
                {
                    var geo = new StreamGeometry();
                    using (var g = geo.Open())
                    {
                        g.BeginFigure(screen[f.VertexIndices[0]], true);
                        for (int i = 1; i < f.VertexIndices.Length; i++)
                            g.LineTo(screen[f.VertexIndices[i]]);
                        g.EndFigure(true);
                    }
                    var n = f.Normal(model.Vertices);
                    double k = Math.Clamp(0.3 + 0.7 * Math.Abs(Point3.Dot(n, light)), 0, 1);
                    var brush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(
                        (byte)(50 + 180 * k), (byte)(80 + 130 * k), (byte)(110 + 90 * k)));
                    ctx.DrawGeometry(brush, new Pen(Brushes.Black, 1), geo);
                }
            }
        }

        private static unsafe void RenderZBuffer(List<Polyhedron> models, MainViewModel vm,
                                                 DrawingContext ctx, int w, int h)
        {
            var zb = new ZBuffer(w, h);
            ZBufferRenderer.Render(models, vm.Cam, zb);

            var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96),
                                          PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var fb = bmp.Lock())
            {
                var ptr = (uint*)fb.Address;
                int stride = fb.RowBytes / 4;
                var src = zb.Pixels;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        ptr[y * stride + x] = src[y * w + x];
            }
            ctx.DrawImage(bmp, new Rect(0, 0, w, h));
        }
    }

    // Хелпер для построения шара (без добавления новых файлов)
    internal static class SphereBuilder
    {
        public static Polyhedron Build(double radius, int stacks, int slices)
        {
            var poly = new Polyhedron();

            for (int i = 0; i <= stacks; i++)
            {
                double phi = Math.PI * i / stacks;
                for (int j = 0; j < slices; j++)
                {
                    double theta = 2 * Math.PI * j / slices;
                    poly.Vertices.Add(new Point3(
                        radius * Math.Sin(phi) * Math.Cos(theta),
                        radius * Math.Cos(phi),
                        radius * Math.Sin(phi) * Math.Sin(theta)));
                }
            }

            for (int i = 0; i < stacks; i++)
            {
                for (int j = 0; j < slices; j++)
                {
                    int a = i * slices + j;
                    int b = i * slices + (j + 1) % slices;
                    int c = (i + 1) * slices + j;
                    int d = (i + 1) * slices + (j + 1) % slices;
                    poly.Faces.Add(new Polygon(new[] { a, b, d }));
                    poly.Faces.Add(new Polygon(new[] { a, d, c }));
                }
            }

            return poly;
        }
    }

    public class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private SceneControl _scene = null!;

        public MainWindow()
        {
            Title = "Lab8 — Отсечение / Z-буфер / Камера";
            Width = 1280; Height = 800;
            DataContext = _vm;
            Content = BuildLayout();
        }

        private Control BuildLayout()
        {
            var p = new StackPanel { Margin = new Thickness(8), Spacing = 5, Width = 360 };

            p.Children.Add(H("Режим"));
            var row = new WrapPanel();
            row.Children.Add(B("Отсечение", _vm.ModeCullCmd));
            row.Children.Add(B("Z-буфер", _vm.ModeZCmd));
            p.Children.Add(row);

            p.Children.Add(H("Проекция"));
            var projRow = new WrapPanel();
            projRow.Children.Add(B("Перспективная", _vm.PerspCmd));
            projRow.Children.Add(B("Параллельная", _vm.ParallelCmd));
            p.Children.Add(projRow);

            p.Children.Add(B("Анимация", _vm.ToggleAnimateCmd));
            p.Children.Add(B("Орбита камеры", _vm.ToggleOrbitCmd));

            p.Children.Add(H("Вектор обзора"));
            p.Children.Add(R("vx", Num(nameof(MainViewModel.ViewDirX))));
            p.Children.Add(R("vy", Num(nameof(MainViewModel.ViewDirY))));
            p.Children.Add(R("vz", Num(nameof(MainViewModel.ViewDirZ))));

            p.Children.Add(H("Камера (позиция)"));
            p.Children.Add(R("x", Num(nameof(MainViewModel.CamX))));
            p.Children.Add(R("y", Num(nameof(MainViewModel.CamY))));
            p.Children.Add(R("z", Num(nameof(MainViewModel.CamZ))));

            p.Children.Add(H("Объект"));
            p.Children.Add(B("Куб", _vm.LoadCubeCmd));
            p.Children.Add(B("Шар", _vm.LoadSphereCmd));
            p.Children.Add(B("Сброс угла", _vm.ResetAngleCmd));

            var secondCheck = new CheckBox { Content = "Второй объект (Z-буфер)", Margin = new Thickness(0, 4, 0, 0) };
            secondCheck.Bind(CheckBox.IsCheckedProperty,
                new Binding(nameof(MainViewModel.ShowSecondObject)) { Source = _vm, Mode = BindingMode.TwoWay });
            p.Children.Add(secondCheck);

            var st = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkSlateBlue };
            st.Bind(TextBlock.TextProperty, new Binding(nameof(MainViewModel.Status)) { Source = _vm });
            p.Children.Add(H("Статус")); p.Children.Add(st);

            var scroll = new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _scene = new SceneControl();
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(scroll, 0); Grid.SetColumn(_scene, 1);
            grid.Children.Add(scroll); grid.Children.Add(_scene);
            return grid;
        }

        private static TextBlock H(string t) => new() { Text = t, FontWeight = FontWeight.Bold, Margin = new Thickness(0, 6, 0, 0) };
        private static Button B(string t, ICommand c) => new() { Content = t, Command = c, Margin = new Thickness(0, 0, 4, 4) };
        private static Control R(string l, Control c)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            sp.Children.Add(new TextBlock { Text = l, Width = 40, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(c); return sp;
        }
        private TextBox Num(string prop)
        {
            var tb = new TextBox { Width = 80 };
            tb.Bind(TextBox.TextProperty, new Binding(prop) { Source = _vm, Mode = BindingMode.TwoWay });
            return tb;
        }
    }
}