using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Lab6.Builders;
using Lab6.Geometry;
using Lab9.Lighting;
using Lab9.Shading;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Application = Avalonia.Application;
using Color = Avalonia.Media.Color;
using Vector = Avalonia.Vector;
using Size = Avalonia.Size;

namespace Lab9
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
        // ICommand требует событие, но нам оно не нужно — пустые add/remove убирают warning CS0067.
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _a();
    }

    public enum ShadingMode { Flat, Gouraud, Phong, Toon, Textured }

    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private Polyhedron _model = PlatonicSolids.Hexahedron();
        private string _status = "Куб";
        private ShadingMode _mode = ShadingMode.Gouraud;
        private Point3 _lightPos = new(2, 3, 4);
        private Color3 _lightColor = new(1, 1, 1);
        private Color3 _ambient = new(0.2, 0.2, 0.2);
        private Color3 _diffuse = new(0.8, 0.6, 0.3);
        private bool _animate;

        // Аффинные преобразования
        private double _rotXDeg = 30;
        private double _rotYDeg = 45;
        private double _rotZDeg = 0;
        private double _scale = 1.0;

        public Polyhedron Model { get => _model; private set { _model = value; OnP(); } }
        public string Status { get => _status; private set { _status = value; OnP(); } }
        public ShadingMode Mode { get => _mode; set { _mode = value; OnP(); } }
        public bool Animate { get => _animate; set { _animate = value; OnP(); } }

        public double LightX { get => _lightPos.X; set { _lightPos = new Point3(value, _lightPos.Y, _lightPos.Z); OnP(); } }
        public double LightY { get => _lightPos.Y; set { _lightPos = new Point3(_lightPos.X, value, _lightPos.Z); OnP(); } }
        public double LightZ { get => _lightPos.Z; set { _lightPos = new Point3(_lightPos.X, _lightPos.Y, value); OnP(); } }

        public double R { get => _diffuse.R; set { _diffuse = new Color3(value, _diffuse.G, _diffuse.B); OnP(); } }
        public double G { get => _diffuse.G; set { _diffuse = new Color3(_diffuse.R, value, _diffuse.B); OnP(); } }
        public double B { get => _diffuse.B; set { _diffuse = new Color3(_diffuse.R, _diffuse.G, value); OnP(); } }

        // Аффинные преобразования (ТЗ: «возможность применения аффинных преобразований к объекту»)
        public double RotXDeg { get => _rotXDeg; set { _rotXDeg = value; OnP(); } }
        public double RotYDeg { get => _rotYDeg; set { _rotYDeg = value; OnP(); } }
        public double RotZDeg { get => _rotZDeg; set { _rotZDeg = value; OnP(); } }
        public double Scale { get => _scale; set { _scale = Math.Clamp(value, 0.1, 3.0); OnP(); } }

        public ICommand TetraCmd { get; }
        public ICommand CubeCmd { get; }
        public ICommand OctaCmd { get; }
        public ICommand CmdFlat { get; }
        public ICommand CmdGouraud { get; }
        public ICommand CmdPhong { get; }
        public ICommand CmdToon { get; }
        public ICommand CmdTexture { get; }
        public ICommand CmdAnim { get; }
        public ICommand CmdSpin { get; }
        public ICommand CmdResetTransform { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? RequestRender;

        public MainViewModel()
        {
            TetraCmd = new RelayCommand(() => { Model = PlatonicSolids.Tetrahedron(); Status = "Тетраэдр"; });
            CubeCmd = new RelayCommand(() => { Model = PlatonicSolids.Hexahedron(); Status = "Куб"; });
            OctaCmd = new RelayCommand(() => { Model = PlatonicSolids.Octahedron(); Status = "Октаэдр"; });
            CmdFlat = new RelayCommand(() => { Mode = ShadingMode.Flat; Status = "Flat"; });
            CmdGouraud = new RelayCommand(() => { Mode = ShadingMode.Gouraud; Status = "Gouraud (Lambert)"; });
            CmdPhong = new RelayCommand(() => { Mode = ShadingMode.Phong; Status = "Phong (Lambert)"; });
            CmdToon = new RelayCommand(() => { Mode = ShadingMode.Toon; Status = "Toon (Phong + квантование)"; });
            CmdTexture = new RelayCommand(() => { Mode = ShadingMode.Textured; Status = "Textured"; });
            CmdAnim = new RelayCommand(() => { Animate = !Animate; Status = Animate ? "Анимация ВКЛ" : "Анимация ВЫКЛ"; });
            // Поворот через угол VM, а не мутацию модели
            CmdSpin = new RelayCommand(() => { RotYDeg += 30; Status = $"Поворот Y: {RotYDeg:F0}°"; });
            CmdResetTransform = new RelayCommand(() => { RotXDeg = 30; RotYDeg = 45; RotZDeg = 0; Scale = 1.0; Status = "Преобразования сброшены"; });

            var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            timer.Tick += (_, _) =>
            {
                // Анимация: меняем угол поворота (сеттер RotYDeg сам дернет OnP → RequestRender)
                if (Animate) RotYDeg += 2;
            };
            timer.Start();
        }

        public Color3 LightColor => _lightColor;
        public Color3 Ambient => _ambient;
        public Color3 Diffuse => _diffuse;

        private void OnP([CallerMemberName] string? n = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
            RequestRender?.Invoke();
        }
    }

    public sealed class SceneControl : Control
    {
        private MainViewModel? _vm;
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (_vm is not null) _vm.RequestRender -= InvalidateVisual;
            _vm = DataContext as MainViewModel;
            if (_vm is not null) _vm.RequestRender += InvalidateVisual;
            InvalidateVisual();
        }

        public override unsafe void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            if (_vm is null || Bounds.Width < 8 || Bounds.Height < 8) return;
            int w = (int)Bounds.Width, h = (int)Bounds.Height;
            ctx.FillRectangle(Brushes.White, new Rect(0, 0, w, h));

            var buf = new GouraudRenderer(w, h);

            // Копия исходной модели до вращения
            var originalModel = _vm.Model.Clone();

            // UV вычисляются в исходной системе координат
            var textureUv = CalculateTextureUV(originalModel);

            // Отдельная копия для отображения и трансформаций
            var model = originalModel.Clone();

            var rotX = Matrix4.RotationX(_vm.RotXDeg * Math.PI / 180.0);
            var rotY = Matrix4.RotationY(_vm.RotYDeg * Math.PI / 180.0);
            var rotZ = Matrix4.RotationZ(_vm.RotZDeg * Math.PI / 180.0);
            var scl = ScaleMatrix(_vm.Scale);
            model.Apply(rotX * rotY * rotZ * scl);

            var lightPos = new Point3(_vm.LightX, _vm.LightY, _vm.LightZ);
            var viewDir = new Point3(0, 0, -1);

            switch (_vm.Mode)
            {
                case ShadingMode.Flat:
                    Flat.DrawModel(buf, model, lightPos, _vm.LightColor,
                        _vm.Ambient, _vm.Diffuse, true, viewDir);
                    break;
                case ShadingMode.Gouraud:
                    Gouraud.DrawModel(buf, model, lightPos, _vm.LightColor,
                        _vm.Ambient, _vm.Diffuse, true, viewDir);
                    break;
                case ShadingMode.Phong:
                    Phong.DrawModel(buf, model, lightPos, _vm.LightColor,
                        _vm.Ambient, _vm.Diffuse, true, viewDir, toon: false);
                    break;
                case ShadingMode.Toon:
                    Phong.DrawModel(buf, model, lightPos, _vm.LightColor,
                        _vm.Ambient, _vm.Diffuse, true, viewDir, toon: true);
                    break;
                case ShadingMode.Textured:
                    DrawTextured(buf, model, textureUv, viewDir);
                    break;
            }

            var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96),
                                          PixelFormat.Bgra8888, AlphaFormat.Premul);
            using (var fb = bmp.Lock())
            {
                var ptr = (uint*)fb.Address;
                int stride = fb.RowBytes / 4;
                var src = buf.Pixels;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        ptr[y * stride + x] = src[y * w + x];
            }
            ctx.DrawImage(bmp, new Rect(0, 0, w, h));
        }

        private static Matrix4 ScaleMatrix(double s) =>
            new Matrix4(s, 0, 0, 0,
                        0, s, 0, 0,
                        0, 0, s, 0,
                        0, 0, 0, 1);

        private static (double u, double v)[] CalculateTextureUV(Polyhedron model)
        {
            var center = model.Center();

            double radius = Math.Max(
                1e-9,
                model.Vertices.Max(p => Point3.Distance(center, p))
            );

            var uv = new (double u, double v)[model.Vertices.Count];

            for (int i = 0; i < model.Vertices.Count; i++)
            {
                // Важно: используем исходные координаты,
                // до применения вращения и масштабирования.
                var p = (model.Vertices[i] - center) * (1.0 / radius);

                double u = 0.5 + Math.Atan2(p.Z, p.X) / (2.0 * Math.PI);
                double v = 0.5 - Math.Asin(Math.Clamp(p.Y, -1.0, 1.0)) / Math.PI;

                uv[i] = (u, v);
            }

            return uv;
        }

        private static void DrawTextured(
            GouraudRenderer buf,
            Polyhedron model,
            (double u, double v)[] uv,
            Point3 viewDir)
        {
            buf.Clear();

            var center = model.Center();

            double radius = Math.Max(
                1e-9,
                model.Vertices.Max(p => Point3.Distance(center, p))
            );

            var verts = model.Vertices
                .Select(p => (p - center) * (1.0 / radius))
                .ToArray();

            double ppu = Math.Min(buf.Width, buf.Height) * 0.4;

            var scr = new (double x, double y, double z)[verts.Length];

            for (int i = 0; i < verts.Length; i++)
            {
                double f = 4.0 / Math.Max(0.1, 4.0 + verts[i].Z);

                scr[i] = (
                    buf.Width / 2 + verts[i].X * ppu * f,
                    buf.Height / 2 - verts[i].Y * ppu * f,
                    verts[i].Z
                );
            }

            Texturing.DrawFace(
                buf,
                model,
                verts,
                scr,
                uv,
                new Color3(0.9, 0.4, 0.4),
                new Color3(0.1, 0.3, 0.9),
                true,
                viewDir
            );
        }
    }

    public class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        public MainWindow()
        {
            Title = "Lab9 — Освещение / Шейдинг / Текстурирование";
            Width = 1280; Height = 800;
            DataContext = _vm;
            Content = BuildLayout();
        }

        private Control BuildLayout()
        {
            var p = new StackPanel { Margin = new Thickness(8), Spacing = 5, Width = 340 };
            p.Children.Add(H("Модель"));
            var r = new WrapPanel();
            r.Children.Add(B("Тетраэдр", _vm.TetraCmd));
            r.Children.Add(B("Куб", _vm.CubeCmd));
            r.Children.Add(B("Октаэдр", _vm.OctaCmd));
            p.Children.Add(r);

            p.Children.Add(H("Модель освещения / шейдинг"));
            var rm = new WrapPanel();
            rm.Children.Add(B("Flat", _vm.CmdFlat));
            rm.Children.Add(B("Gouraud", _vm.CmdGouraud));
            rm.Children.Add(B("Phong", _vm.CmdPhong));
            rm.Children.Add(B("Toon", _vm.CmdToon));
            rm.Children.Add(B("Текстура", _vm.CmdTexture));
            p.Children.Add(rm);
            p.Children.Add(B("Анимация", _vm.CmdAnim));

            p.Children.Add(H("Аффинные преобразования"));
            p.Children.Add(R("rotX°", Num(nameof(MainViewModel.RotXDeg))));
            p.Children.Add(R("rotY°", Num(nameof(MainViewModel.RotYDeg))));
            p.Children.Add(R("rotZ°", Num(nameof(MainViewModel.RotZDeg))));
            p.Children.Add(R("scale", Num(nameof(MainViewModel.Scale))));
            var rr = new WrapPanel();
            rr.Children.Add(B("+30° Y", _vm.CmdSpin));
            rr.Children.Add(B("Сброс", _vm.CmdResetTransform));
            p.Children.Add(rr);

            p.Children.Add(H("Источник света"));
            p.Children.Add(R("x", Num(nameof(MainViewModel.LightX))));
            p.Children.Add(R("y", Num(nameof(MainViewModel.LightY))));
            p.Children.Add(R("z", Num(nameof(MainViewModel.LightZ))));

            p.Children.Add(H("Цвет объекта (diffuse)"));
            p.Children.Add(R("r", Num(nameof(MainViewModel.R))));
            p.Children.Add(R("g", Num(nameof(MainViewModel.G))));
            p.Children.Add(R("b", Num(nameof(MainViewModel.B))));

            var st = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkSlateBlue };
            st.Bind(TextBlock.TextProperty, new Binding(nameof(MainViewModel.Status)) { Source = _vm });
            p.Children.Add(H("Статус")); p.Children.Add(st);

            var scroll = new ScrollViewer { Content = p, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var scene = new SceneControl();
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            Grid.SetColumn(scroll, 0); Grid.SetColumn(scene, 1);
            grid.Children.Add(scroll); grid.Children.Add(scene);
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