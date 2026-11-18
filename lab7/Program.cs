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
using Avalonia.Platform.Storage;
using Lab6.Builders;
using Lab6.Geometry;
using Lab6.Rendering;
using Lab6.Transforms;
using Lab7.Builders;
using Lab7.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Application = Avalonia.Application;
using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;
using Size = Avalonia.Size;
using Vector = Avalonia.Vector;

namespace Lab7
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
        private readonly Action _a; public RelayCommand(Action a) => _a = a;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _a();
    }
    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _a; public AsyncRelayCommand(Func<Task> a) => _a = a;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => true;
        public async void Execute(object? p) => await _a();
    }

    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private Polyhedron _model = PlatonicSolids.Hexahedron();
        private string _status = "Гексаэдр";

        // Кэш «вписывания» — пересчитывается только при смене модели или по команде FitCmd.
        // Именно поэтому трансформации (перенос/масштаб/поворот) видны на экране:
        // рендер больше не нормирует модель каждый кадр.
        private Point3 _fitCenter = default;
        private double _fitRadius = 1;

        public Point3 FitCenter => _fitCenter;
        public double FitRadius => _fitRadius;

        public Camera Camera { get; } = new();

        public Polyhedron Model
        {
            get => _model;
            private set
            {
                _model = value;
                Refit();
                OnPropertyChanged();
            }
        }
        public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }

        // для вращения
        public string ProfilePreset { get; set; } = "Цилиндр";
        public string[] ProfilePresetNames { get; } = { "Цилиндр", "Конус", "Сфера", "Ваза", "Тор" };
        public string RevAxis { get; set; } = "Y";
        public string[] Axes { get; } = { "X", "Y", "Z" };
        public int RevSegments { get; set; } = 24;

        // для поверхности
        public string FunctionName { get; set; } = "Седло";
        public string[] FunctionNames => SurfaceFunctionPresets.All.Select(p => p.Name).ToArray();
        public double X0 { get; set; } = -2; public double X1 { get; set; } = 2;
        public double Y0 { get; set; } = -2; public double Y1 { get; set; } = 2;
        public int SurfSeg { get; set; } = 20;

        // трансформации
        public double TfX { get; set; }
        public double TfY { get; set; }
        public double TfZ { get; set; }
        public string RotAxis { get; set; } = "Y";
        public double RotAngle { get; set; } = 30;
        public double ScaleK { get; set; } = 1.2;

        public ICommand LoadCmd { get; }
        public AsyncRelayCommand SaveCmd { get; }
        public ICommand MakeRevCmd { get; }
        public ICommand MakeSurfaceCmd { get; }
        public ICommand ApplyTranslateCmd { get; }
        public ICommand ApplyRotateCmd { get; }
        public ICommand ApplyScaleCmd { get; }
        public ICommand PresetTetraCmd { get; }
        public ICommand PresetCubeCmd { get; }
        public ICommand PresetOctaCmd { get; }
        public ICommand FitCmd { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private Window? _owner;
        public void Attach(Window w) => _owner = w;

        public MainViewModel()
        {
            Refit(); // вписать исходный гексаэдр

            PresetTetraCmd = new RelayCommand(() => { Model = PlatonicSolids.Tetrahedron(); Status = "Тетраэдр"; });
            PresetCubeCmd = new RelayCommand(() => { Model = PlatonicSolids.Hexahedron(); Status = "Куб"; });
            PresetOctaCmd = new RelayCommand(() => { Model = PlatonicSolids.Octahedron(); Status = "Октаэдр"; });

            LoadCmd = new AsyncRelayCommand(LoadAsync);
            SaveCmd = new AsyncRelayCommand(SaveAsync);

            FitCmd = new RelayCommand(() =>
            {
                Refit();
                OnPropertyChanged(nameof(FitCenter));
                OnPropertyChanged(nameof(FitRadius));
                Status = "Модель вписана в окно";
            });

            MakeRevCmd = new RelayCommand(() => {
                var prof = ProfilePreset switch
                {
                    "Цилиндр" => ProfilePresets.Cylinder(),
                    "Конус" => ProfilePresets.Cone(),
                    "Сфера" => ProfilePresets.Sphere(),
                    "Ваза" => ProfilePresets.Vase(),
                    "Тор" => ProfilePresets.Torus(),
                    _ => ProfilePresets.Cylinder()
                };
                var err = RevolutionValidator.Validate(prof, RevAxis, RevSegments);
                if (err is not null) { Status = err; return; }
                Model = RevolutionBuilder.Build(prof, RevAxis, RevSegments);
                Status = $"Вращение: {ProfilePreset}, ось {RevAxis}, {RevSegments} разбиений";
            });

            MakeSurfaceCmd = new RelayCommand(() => {
                var err = SurfaceValidator.Validate(X0, X1, Y0, Y1, SurfSeg);
                if (err is not null) { Status = err; return; }
                var preset = SurfaceFunctionPresets.All.First(p => p.Name == FunctionName);
                Model = SurfaceBuilder.Build(preset.F, X0, X1, Y0, Y1, SurfSeg);
                Status = $"График: {FunctionName} на [{X0},{X1}]×[{Y0},{Y1}]";
            });

            // ВАЖНО: эти команды НЕ вызывают Refit() — поэтому трансформация остаётся видимой.
            ApplyTranslateCmd = new RelayCommand(() => {
                Model.Apply(ModelTransforms.Translate(TfX, TfY, TfZ));
                OnPropertyChanged(nameof(Model));
                Status = $"Смещение ({TfX};{TfY};{TfZ})";
            });
            ApplyRotateCmd = new RelayCommand(() => {
                Model.Apply(ModelTransforms.RotateAroundOrigin(RotAxis, RotAngle));
                OnPropertyChanged(nameof(Model));
                Status = $"Поворот {RotAxis} на {RotAngle}°";
            });
            ApplyScaleCmd = new RelayCommand(() => {
                Model.Apply(ModelTransforms.ScaleAroundOrigin(ScaleK));
                OnPropertyChanged(nameof(Model));
                Status = $"Масштаб ×{ScaleK}";
            });
        }

        /// <summary>
        /// Пересчитывает центр и радиус «вписывания» по текущей модели.
        /// Вызывается только при смене модели и по команде FitCmd.
        /// </summary>
        public void Refit()
        {
            var c = _model.Center();
            double r = _model.Vertices.Count == 0
                ? 1
                : _model.Vertices.Max(v => Point3.Distance(c, v));
            _fitCenter = c;
            _fitRadius = r < 1e-9 ? 1 : r;
        }

        private async Task LoadAsync()
        {
            if (_owner is null) return;
            var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Открыть OBJ",
                FileTypeFilter = new[] { new FilePickerFileType("Wavefront OBJ") { Patterns = new[] { "*.obj" } } }
            });
            if (files.Count == 0) return;
            try
            {
                await using var s = await files[0].OpenReadAsync();
                Model = ModelFileService.LoadObj(s);
                var st = ModelFileService.Analyze(Model);
                Status = $"{files[0].Name}: {ModelFileService.Describe(st)}";
            }
            catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
        }

        private async Task SaveAsync()
        {
            if (_owner is null) return;
            var f = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Сохранить OBJ",
                SuggestedFileName = "model.obj",
                DefaultExtension = "obj",
                FileTypeChoices = new[] { new FilePickerFileType("Wavefront OBJ") { Patterns = new[] { "*.obj" } } }
            });
            if (f is null) return;
            try
            {
                await using var s = await f.OpenWriteAsync();
                ModelFileService.SaveObj(Model, s, "lab7 export");
                Status = "Сохранено: " + f.Name;
            }
            catch (Exception ex) { Status = "Ошибка: " + ex.Message; }
        }

        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public sealed class Camera
    {
        public double Distance { get; set; } = 6;
        public ProjectionMode Projection { get; set; } = ProjectionMode.Perspective;
    }

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
                    double u = ((y2 - y3) * (cx - x3) + (x3 - x2) * (cy - y3)) * inv;
                    double v = ((y3 - y1) * (cx - x3) + (x1 - x3) * (cy - y3)) * inv;
                    double w = 1 - u - v;
                    if (u < -1e-9 || v < -1e-9 || w < -1e-9) continue;

                    double z = u * z1 + v * z2 + w * z3;
                    int idx = py * Width + px;
                    if (z < _depth[idx])
                    {
                        _depth[idx] = z;
                        _color[idx] = color;
                    }
                }
        }
    }

    public sealed class SceneControl : Control
    {
        private MainViewModel? _vm;
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (_vm is not null) _vm.PropertyChanged -= OnChanged;
            _vm = DataContext as MainViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnChanged;
            InvalidateVisual();
        }
        private void OnChanged(object? s, PropertyChangedEventArgs e) => InvalidateVisual();

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            if (_vm is null) return;
            int w = (int)Bounds.Width, h = (int)Bounds.Height;
            if (w < 4 || h < 4) return;

            ctx.FillRectangle(new SolidColorBrush(Colors.White), new Rect(0, 0, w, h));
            RenderScene.DrawZBuffer(
                _vm.Model, _vm.Camera, ctx, new Size(w, h),
                _vm.FitCenter, _vm.FitRadius);
        }
    }

    public static class RenderScene
    {
        /// <summary>
        /// Z-буферный рендер. Параметры fitCenter/fitRadius берутся из ViewModel и
        /// НЕ пересчитываются по текущей модели — поэтому перенос/масштаб/поворот видны.
        /// </summary>
        public static unsafe void DrawZBuffer(
            Polyhedron model, Camera cam, DrawingContext ctx, Size size,
            Point3 fitCenter, double fitRadius)
        {
            int w = (int)size.Width, h = (int)size.Height;
            if (model.Vertices.Count == 0 || w < 4 || h < 4) return;
            if (fitRadius < 1e-9) fitRadius = 1;

            var zb = new ZBuffer(w, h);

            // Нормировка по КЭШУ, а не по текущей модели.
            var verts = model.Vertices
                .Select(p => (p - fitCenter) * (1.0 / fitRadius))
                .ToArray();

            var rot = Matrix4.RotationY(Math.PI / 4) * Matrix4.RotationX(Math.Asin(Math.Sqrt(1.0 / 3.0)));

            double ppu = Math.Min(w, h) * 0.4;
            var projected = new (double x, double y, double z)[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var p = rot.TransformPoint(verts[i]);
                var (x, y, d) = ProjectionMath.Project(p, cam.Projection, ppu, 4);
                projected[i] = (w / 2 + x, h / 2 - y, d);
            }

            var light = new Point3(0.4, 0.8, -1).Normalized();

            foreach (var face in model.Faces)
            {
                var n = face.Normal(verts);
                double k = Math.Clamp(0.35 + 0.65 * Math.Abs(Point3.Dot(n, light)), 0, 1);
                uint color = 0xFF000000u | ((uint)((byte)(60 + 160 * k)) << 16) | ((uint)((byte)(80 + 130 * k)) << 8) | (uint)((byte)(100 + 100 * k));

                var idx = face.VertexIndices;
                for (int i = 1; i < idx.Length - 1; i++)
                {
                    var a = projected[idx[0]];
                    var b = projected[idx[i]];
                    var cc = projected[idx[i + 1]];
                    zb.Triangle(a.x, a.y, a.z, b.x, b.y, b.z, cc.x, cc.y, cc.z, color);
                }
            }

            var bmp = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
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

    public class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private SceneControl _scene = null!;
        private bool _drag; private Point _prev;

        public MainWindow()
        {
            Title = "Lab7 — OBJ / Вращение / Поверхность";
            Width = 1280; Height = 800;
            _vm.Attach(this); DataContext = _vm;
            Content = BuildLayout();
            _scene.PointerPressed += (_, e) => { _drag = true; _prev = e.GetPosition(_scene); };
            _scene.PointerReleased += (_, _) => _drag = false;
            _scene.PointerMoved += (_, e) => {
                if (!_drag) return;
                var cur = e.GetPosition(_scene);
                double dx = cur.X - _prev.X, dy = cur.Y - _prev.Y;
                _prev = cur;
                _vm.Model.Apply(Matrix4.RotationY(dx * 0.01) * Matrix4.RotationX(dy * 0.01));
                _scene.InvalidateVisual();
            };
        }

        private Control BuildLayout()
        {
            var p = new StackPanel { Margin = new Thickness(8), Spacing = 5, Width = 340 };
            p.Children.Add(H("Загрузка / Сохранение"));
            var frow = new WrapPanel();
            frow.Children.Add(B("Загрузить OBJ", _vm.LoadCmd));
            frow.Children.Add(B("Сохранить OBJ", _vm.SaveCmd));
            p.Children.Add(frow);
            var prow = new WrapPanel();
            prow.Children.Add(B("Тетраэдр", _vm.PresetTetraCmd));
            prow.Children.Add(B("Куб", _vm.PresetCubeCmd));
            prow.Children.Add(B("Октаэдр", _vm.PresetOctaCmd));
            p.Children.Add(prow);
            p.Children.Add(B("Вписать в окно", _vm.FitCmd));   // <-- новая кнопка

            p.Children.Add(H("Фигура вращения"));
            p.Children.Add(R("Профиль", Combo(nameof(MainViewModel.ProfilePresetNames), nameof(MainViewModel.ProfilePreset))));
            p.Children.Add(R("Ось", Combo(nameof(MainViewModel.Axes), nameof(MainViewModel.RevAxis))));
            p.Children.Add(R("Разб.", Num(nameof(MainViewModel.RevSegments))));
            p.Children.Add(B("Построить", _vm.MakeRevCmd));

            p.Children.Add(H("График поверхности"));
            p.Children.Add(R("f(x,y)", Combo(nameof(MainViewModel.FunctionNames), nameof(MainViewModel.FunctionName))));
            p.Children.Add(R("x0", Num(nameof(MainViewModel.X0))));
            p.Children.Add(R("x1", Num(nameof(MainViewModel.X1))));
            p.Children.Add(R("y0", Num(nameof(MainViewModel.Y0))));
            p.Children.Add(R("y1", Num(nameof(MainViewModel.Y1))));
            p.Children.Add(R("Разб.", Num(nameof(MainViewModel.SurfSeg))));
            p.Children.Add(B("Построить", _vm.MakeSurfaceCmd));

            p.Children.Add(H("Аффинные преобразования"));
            p.Children.Add(R("dx", Num(nameof(MainViewModel.TfX))));
            p.Children.Add(R("dy", Num(nameof(MainViewModel.TfY))));
            p.Children.Add(R("dz", Num(nameof(MainViewModel.TfZ))));
            p.Children.Add(B("Сместить", _vm.ApplyTranslateCmd));
            p.Children.Add(R("Ось", Combo(nameof(MainViewModel.Axes), nameof(MainViewModel.RotAxis))));
            p.Children.Add(R("Угол°", Num(nameof(MainViewModel.RotAngle))));
            p.Children.Add(B("Повернуть", _vm.ApplyRotateCmd));
            p.Children.Add(R("k", Num(nameof(MainViewModel.ScaleK))));
            p.Children.Add(B("Масштаб", _vm.ApplyScaleCmd));

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
            sp.Children.Add(new TextBlock { Text = l, Width = 60, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(c); return sp;
        }
        private TextBox Num(string prop)
        {
            var tb = new TextBox { Width = 90 };
            tb.Bind(TextBox.TextProperty, new Binding(prop) { Source = _vm, Mode = BindingMode.TwoWay });
            return tb;
        }
        private ComboBox Combo(string items, string sel)
        {
            var cb = new ComboBox { Width = 110 };
            cb.Bind(ComboBox.ItemsSourceProperty, new Binding(items) { Source = _vm });
            cb.Bind(ComboBox.SelectedItemProperty, new Binding(sel) { Source = _vm, Mode = BindingMode.TwoWay });
            return cb;
        }
    }
}