// ============================================================================
// ui.cs — UI (программный, без XAML), сцена, камера, рендерер, ViewModel.
// ============================================================================

// ---- Алиасы: устраняют неоднозначность с System.Drawing / System.Net.Mime ----
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;   // ApplicationLifetime, IClassicDesktopStyleApplicationLifetime
using Avalonia.Controls.Primitives;             // ScrollBarVisibility
using Avalonia.Data;                             // Binding, BindingMode
using Avalonia.Input;                            // PointerPressedEventArgs, PointerWheelEventArgs
using Avalonia.Layout;                           // Orientation, VerticalAlignment
using Avalonia.Media;                            // SolidColorBrush, Pen, Brushes, DrawingContext, StreamGeometry
using Avalonia.Platform.Storage;                 // FilePickerOpenOptions, FilePickerSaveOptions, FilePickerFileType
using Lab6.Builders;
using Lab6.Geometry;
using Lab6.IO;
using Lab6.Rendering;
using Lab6.Transforms;
using Lab6.ViewModels;
using System.ComponentModel;
using System.IO;                                // File, StreamWriter, StreamReader, Stream
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Application = Avalonia.Application;
using Color = Avalonia.Media.Color;
using Point = Avalonia.Point;
using Size = Avalonia.Size;

namespace Lab6
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args) =>
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }

    public class App : Application
    {
        // ВАЖНО: подключаем стандартную тему оформления
        public override void Initialize()
        {
            Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }

    // --------------------------------------------------------------
    // Главное окно (программное построение интерфейса)
    // --------------------------------------------------------------
    public class MainWindow : Window
    {
        private readonly MainViewModel _vm = new();
        private SceneControl _scene = null!;

        private bool _isDragging;
        private Point _previousPointer;

        public MainWindow()
        {
            Title = "Lab6 — Многогранники";
            Width = 1280;
            Height = 800;

            _vm.AttachWindow(this);
            DataContext = _vm;
            Content = BuildLayout();

            _scene.PointerPressed += OnScenePointerPressed;
            _scene.PointerMoved += OnScenePointerMoved;
            _scene.PointerReleased += OnScenePointerReleased;
            _scene.PointerWheelChanged += OnScenePointerWheelChanged;
        }

        private Control BuildLayout()
        {
            var left = new StackPanel
            {
                Margin = new Thickness(8),
                Spacing = 6,
                Width = 340
            };

            // ---- Правильные многогранники ----
            left.Children.Add(Header("Правильные многогранники"));
            var solids = new WrapPanel();
            solids.Children.Add(Btn("Тетраэдр", _vm.CreateTetrahedronCommand));
            solids.Children.Add(Btn("Гексаэдр", _vm.CreateHexahedronCommand));
            solids.Children.Add(Btn("Октаэдр", _vm.CreateOctahedronCommand));
            solids.Children.Add(Btn("Икосаэдр", _vm.CreateIcosahedronCommand));
            solids.Children.Add(Btn("Додекаэдр", _vm.CreateDodecahedronCommand));
            left.Children.Add(solids);

            // ---- Проекция (задание 7) ----
            left.Children.Add(Header("Проекция"));
            var proj = new WrapPanel();
            proj.Children.Add(Btn("Перспективная", _vm.SetPerspectiveCommand));
            proj.Children.Add(Btn("Аксонометрическая", _vm.SetAxonometricCommand));
            left.Children.Add(proj);

            // ---- Задание 2: смещение ----
            left.Children.Add(Header("Смещение (задание 2)"));
            left.Children.Add(Row("dx", Num(nameof(MainViewModel.TranslateX))));
            left.Children.Add(Row("dy", Num(nameof(MainViewModel.TranslateY))));
            left.Children.Add(Row("dz", Num(nameof(MainViewModel.TranslateZ))));
            left.Children.Add(Btn("Применить смещение", _vm.ApplyTranslationCommand));

            // ---- Задание 2: поворот вокруг начала ----
            left.Children.Add(Header("Поворот вокруг начала координат (задание 2)"));
            left.Children.Add(Row("Ось",
                Combo(nameof(MainViewModel.AxisOptions), nameof(MainViewModel.RotationAxis))));
            left.Children.Add(Row("Угол°", Num(nameof(MainViewModel.RotationAngle))));
            left.Children.Add(Btn("Повернуть", _vm.ApplyRotationAroundOriginCommand));

            // ---- Задание 2: масштаб вокруг начала ----
            left.Children.Add(Header("Масштаб вокруг начала (задание 2)"));
            left.Children.Add(Row("k", Num(nameof(MainViewModel.ScaleFactor))));
            left.Children.Add(Btn("Масштабировать", _vm.ApplyScaleAroundOriginCommand));

            // ---- Задание 3: отражение ----
            left.Children.Add(Header("Отражение (задание 3)"));
            left.Children.Add(Row("Плоскость",
                Combo(nameof(MainViewModel.PlaneOptions), nameof(MainViewModel.ReflectionPlane))));
            left.Children.Add(Btn("Отразить", _vm.ApplyReflectionCommand));

            // ---- Задание 4: масштаб относительно центра ----
            left.Children.Add(Header("Масштаб относительно центра (задание 4)"));
            left.Children.Add(Row("k", Num(nameof(MainViewModel.CenterScaleFactor))));
            left.Children.Add(Btn("Масштабировать", _vm.ApplyScaleAroundCenterCommand));

            // ---- Задание 5: поворот вокруг оси через центр ----
            left.Children.Add(Header("Поворот вокруг оси через центр (задание 5)"));
            left.Children.Add(Row("Ось",
                Combo(nameof(MainViewModel.AxisOptions), nameof(MainViewModel.CenterRotationAxis))));
            left.Children.Add(Row("Угол°", Num(nameof(MainViewModel.CenterRotationAngle))));
            left.Children.Add(Btn("Повернуть", _vm.ApplyRotationAroundCenterAxisCommand));

            // ---- Задание 6: поворот вокруг произвольной прямой ----
            left.Children.Add(Header("Поворот вокруг произвольной прямой (задание 6)"));
            left.Children.Add(Row("A.x", Num(nameof(MainViewModel.AxisPointX))));
            left.Children.Add(Row("A.y", Num(nameof(MainViewModel.AxisPointY))));
            left.Children.Add(Row("A.z", Num(nameof(MainViewModel.AxisPointZ))));
            left.Children.Add(Row("B.x", Num(nameof(MainViewModel.BAxisPointX))));
            left.Children.Add(Row("B.y", Num(nameof(MainViewModel.BAxisPointY))));
            left.Children.Add(Row("B.z", Num(nameof(MainViewModel.BAxisPointZ))));
            left.Children.Add(Row("Угол°", Num(nameof(MainViewModel.CustomRotationAngle))));
            left.Children.Add(Btn("Повернуть", _vm.ApplyRotationAroundCustomLineCommand));

            // ---- Файлы ----
            left.Children.Add(Header("Файл"));
            var fileRow = new WrapPanel();
            fileRow.Children.Add(Btn("Загрузить OBJ", _vm.LoadModelCommand));
            fileRow.Children.Add(Btn("Сохранить OBJ", _vm.SaveModelCommand));
            left.Children.Add(fileRow);

            // ---- Статус ----
            left.Children.Add(Header("Статус"));
            var status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.DarkSlateBlue
            };
            status.Bind(TextBlock.TextProperty,
                new Binding(nameof(MainViewModel.Status)) { Source = _vm });
            left.Children.Add(status);

            var scroll = new ScrollViewer
            {
                Content = left,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            _scene = new SceneControl();

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*")
            };
            Grid.SetColumn(scroll, 0);
            Grid.SetColumn(_scene, 1);
            grid.Children.Add(scroll);
            grid.Children.Add(_scene);
            return grid;
        }

        // ---- helpers ----
        private static TextBlock Header(string text) => new()
        {
            Text = text,
            FontWeight = FontWeight.Bold,
            Margin = new Thickness(0, 6, 0, 0)
        };

        private static Button Btn(string text, ICommand cmd) => new()
        {
            Content = text,
            Margin = new Thickness(0, 0, 4, 4), // отступ справа и снизу для WrapPanel
            Command = cmd
        };

        private static Control Row(string label, Control ctl)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            p.Children.Add(new TextBlock
            {
                Text = label,
                Width = 60,
                VerticalAlignment = VerticalAlignment.Center
            });
            p.Children.Add(ctl);
            return p;
        }

        private TextBox Num(string prop)
        {
            var tb = new TextBox { Width = 90 };
            tb.Bind(TextBox.TextProperty,
                new Binding(prop) { Source = _vm, Mode = BindingMode.TwoWay });
            return tb;
        }

        private ComboBox Combo(string itemsProp, string selectedProp)
        {
            var cb = new ComboBox { Width = 90 };
            cb.Bind(ComboBox.ItemsSourceProperty,
                new Binding(itemsProp) { Source = _vm });
            cb.Bind(ComboBox.SelectedItemProperty,
                new Binding(selectedProp) { Source = _vm, Mode = BindingMode.TwoWay });
            return cb;
        }

        // ---- события сцены ----
        private void OnScenePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _isDragging = true;
            _previousPointer = e.GetPosition(_scene);
        }

        private void OnScenePointerReleased(object? sender, PointerReleasedEventArgs e) =>
            _isDragging = false;

        private void OnScenePointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging) return;
            var cur = e.GetPosition(_scene);
            double dx = cur.X - _previousPointer.X;
            double dy = cur.Y - _previousPointer.Y;
            _previousPointer = cur;

            var rot = Matrix4.RotationY(dx * 0.01) *
                      Matrix4.RotationX(dy * 0.01);
            _vm.Model.Apply(rot);
            _scene.InvalidateVisual();
        }

        private void OnScenePointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            double f = e.Delta.Y > 0 ? 1.1 : 1 / 1.1;
            var c = _vm.Model.Center();
            var m = Matrix4.Translation(-c.X, -c.Y, -c.Z) *
                    Matrix4.Scale(f, f, f) *
                    Matrix4.Translation(c.X, c.Y, c.Z);
            _vm.Model.Apply(m);
            _scene.InvalidateVisual();
        }
    }

    // --------------------------------------------------------------
    // Сцена
    // --------------------------------------------------------------
    public sealed class SceneControl : Control
    {
        private MainViewModel? _vm;

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (_vm is not null) _vm.PropertyChanged -= OnVmChanged;
            _vm = DataContext as MainViewModel;
            if (_vm is not null) _vm.PropertyChanged += OnVmChanged;
            InvalidateVisual();
        }

        private void OnVmChanged(object? s, PropertyChangedEventArgs e) =>
            InvalidateVisual();

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            if (_vm is null) return;

            ctx.FillRectangle(
                new SolidColorBrush(Colors.White),
                new Rect(0, 0, Bounds.Width, Bounds.Height));

            Renderer.Draw(_vm.Model, _vm.Camera, ctx,
                new Size(Bounds.Width, Bounds.Height));
        }
    }

    // --------------------------------------------------------------
    // Камера
    // --------------------------------------------------------------
    public sealed class Camera
    {
        public double Distance { get; set; } = 6.0;
        public double FieldOfView { get; set; } = 60.0;
        public ProjectionMode Projection { get; set; } = ProjectionMode.Perspective;
    }

    // --------------------------------------------------------------
    // Рендерер
    // --------------------------------------------------------------
    public static class Renderer
    {
        private sealed class InitialView
        {
            public Point3 Center { get; }
            public double Radius { get; }
            public InitialView(Polyhedron m)
            {
                Center = m.Center();
                double r = m.Vertices.Max(p => Point3.Distance(Center, p));
                Radius = r > 1e-12 ? r : 1;
            }
        }

        private static readonly ConditionalWeakTable<Polyhedron, InitialView>
            Views = new();

        public static void Draw(
            Polyhedron model, Camera camera,
            DrawingContext ctx, Size size)
        {
            if (size.Width <= 2 || size.Height <= 2 ||
                model.Vertices.Count == 0 || model.Faces.Count == 0)
                return;

            if (model.Vertices.Any(p =>
                    !double.IsFinite(p.X) || !double.IsFinite(p.Y) || !double.IsFinite(p.Z)))
                return;

            var view = Views.GetValue(model, m => new InitialView(m));
            var verts = model.Vertices
                .Select(p => (p - view.Center) * (1.0 / view.Radius))
                .ToArray();

            var rot = Matrix4.RotationY(Math.PI / 4) *
                      Matrix4.RotationX(Math.Asin(Math.Sqrt(1.0 / 3.0)));

            double ppu = Math.Min(size.Width, size.Height) * 0.35;
            const double distance = 4;

            var projected = new Point[verts.Length];
            var depths = new double[verts.Length];

            for (int i = 0; i < verts.Length; i++)
            {
                var p = rot.TransformPoint(verts[i]);
                var (x, y, d) = ProjectionMath.Project(
                    p, camera.Projection, ppu, distance);
                depths[i] = d;
                projected[i] = new Point(size.Width / 2 + x, size.Height / 2 - y);
            }

            var faces = model.Faces
                .Where(f => f.VertexIndices.Length >= 3 &&
                            f.VertexIndices.All(i => i >= 0 && i < verts.Length))
                .OrderByDescending(f => f.VertexIndices.Average(i => depths[i]));

            var light = new Point3(0.4, 0.8, -1).Normalized();

            using (ctx.PushClip(new Rect(0, 0, size.Width, size.Height)))
            {
                foreach (var face in faces)
                {
                    var geo = new StreamGeometry();
                    using (var s = geo.Open())
                    {
                        s.BeginFigure(projected[face.VertexIndices[0]], true);
                        for (int i = 1; i < face.VertexIndices.Length; i++)
                            s.LineTo(projected[face.VertexIndices[i]]);
                        s.EndFigure(true);
                    }

                    var n = face.Normal(verts);
                    double intensity = Math.Clamp(
                        0.35 + 0.65 * Math.Abs(Point3.Dot(n, light)), 0, 1);

                    ctx.DrawGeometry(
                        new SolidColorBrush(Color.FromRgb(
                            (byte)(60 + 160 * intensity),
                            (byte)(50 + 140 * intensity),
                            (byte)(40 + 110 * intensity))),
                        new Pen(Brushes.DarkSlateGray, 1.1),
                        geo);
                }
            }
        }
    }
}

namespace Lab6.ViewModels
{
    // ---- ICommand-обёртки (были в MainViewModel.cs) ----
    public sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _execute();
        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        public AsyncRelayCommand(Func<Task> execute) => _execute = execute;
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? p) => true;
        public async void Execute(object? p) => await _execute();
        public void RaiseCanExecuteChanged() =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public sealed class MainViewModel : INotifyPropertyChanged
    {
        private Polyhedron _model = PlatonicSolids.Hexahedron();
        private string _status = "Гексаэдр";
        private ProjectionMode _projection = ProjectionMode.Perspective;

        // задание 2
        private double _translateX, _translateY, _translateZ;
        private string _rotationAxis = "X";
        private double _rotationAngle = 30;
        private double _scaleFactor = 1.2;

        // задание 3
        private string _reflectionPlane = "XY";

        // задание 4
        private double _centerScaleFactor = 1.2;

        // задание 5
        private string _centerRotationAxis = "Y";
        private double _centerRotationAngle = 45;

        // задание 6
        private double _axisPointX, _axisPointY, _axisPointZ;
        private double _bAxisPointX = 1, _bAxisPointY = 1, _bAxisPointZ = 1;
        private double _customRotationAngle = 45;

        public string[] AxisOptions { get; } = { "X", "Y", "Z" };
        public string[] PlaneOptions { get; } = { "XY", "XZ", "YZ" };

        public event PropertyChangedEventHandler? PropertyChanged;

        public Camera Camera { get; } = new();

        public Polyhedron Model
        {
            get => _model;
            private set { _model = value; OnPropertyChanged(); }
        }

        public string Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        public ProjectionMode Projection
        {
            get => _projection;
            private set => Set(ref _projection, value);
        }

        public double TranslateX { get => _translateX; set => Set(ref _translateX, value); }
        public double TranslateY { get => _translateY; set => Set(ref _translateY, value); }
        public double TranslateZ { get => _translateZ; set => Set(ref _translateZ, value); }

        public string RotationAxis
        {
            get => _rotationAxis;
            set => Set(ref _rotationAxis, AxisOptions.Contains(value) ? value : "X");
        }
        public double RotationAngle { get => _rotationAngle; set => Set(ref _rotationAngle, value); }
        public double ScaleFactor { get => _scaleFactor; set => Set(ref _scaleFactor, value); }

        public string ReflectionPlane
        {
            get => _reflectionPlane;
            set => Set(ref _reflectionPlane, PlaneOptions.Contains(value) ? value : "XY");
        }

        public double CenterScaleFactor { get => _centerScaleFactor; set => Set(ref _centerScaleFactor, value); }

        public string CenterRotationAxis
        {
            get => _centerRotationAxis;
            set => Set(ref _centerRotationAxis, AxisOptions.Contains(value) ? value : "Y");
        }
        public double CenterRotationAngle { get => _centerRotationAngle; set => Set(ref _centerRotationAngle, value); }

        public double AxisPointX { get => _axisPointX; set => Set(ref _axisPointX, value); }
        public double AxisPointY { get => _axisPointY; set => Set(ref _axisPointY, value); }
        public double AxisPointZ { get => _axisPointZ; set => Set(ref _axisPointZ, value); }
        public double BAxisPointX { get => _bAxisPointX; set => Set(ref _bAxisPointX, value); }
        public double BAxisPointY { get => _bAxisPointY; set => Set(ref _bAxisPointY, value); }
        public double BAxisPointZ { get => _bAxisPointZ; set => Set(ref _bAxisPointZ, value); }
        public double CustomRotationAngle { get => _customRotationAngle; set => Set(ref _customRotationAngle, value); }

        public ICommand CreateTetrahedronCommand { get; }
        public ICommand CreateHexahedronCommand { get; }
        public ICommand CreateOctahedronCommand { get; }
        public ICommand CreateIcosahedronCommand { get; }
        public ICommand CreateDodecahedronCommand { get; }
        public ICommand SetPerspectiveCommand { get; }
        public ICommand SetAxonometricCommand { get; }
        public ICommand ApplyTranslationCommand { get; }
        public ICommand ApplyRotationAroundOriginCommand { get; }
        public ICommand ApplyScaleAroundOriginCommand { get; }
        public ICommand ApplyReflectionCommand { get; }
        public ICommand ApplyScaleAroundCenterCommand { get; }
        public ICommand ApplyRotationAroundCenterAxisCommand { get; }
        public ICommand ApplyRotationAroundCustomLineCommand { get; }
        public AsyncRelayCommand LoadModelCommand { get; }
        public AsyncRelayCommand SaveModelCommand { get; }

        private Window? _ownerWindow;
        public void AttachWindow(Window owner) => _ownerWindow = owner;

        public MainViewModel()
        {
            CreateTetrahedronCommand = new RelayCommand(() => CreateModel(PlatonicSolids.Tetrahedron(), "Тетраэдр"));
            CreateHexahedronCommand = new RelayCommand(() => CreateModel(PlatonicSolids.Hexahedron(), "Гексаэдр"));
            CreateOctahedronCommand = new RelayCommand(() => CreateModel(PlatonicSolids.Octahedron(), "Октаэдр"));
            CreateIcosahedronCommand = new RelayCommand(() => CreateModel(PlatonicSolids.Icosahedron(), "Икосаэдр"));
            CreateDodecahedronCommand = new RelayCommand(() => CreateModel(PlatonicSolids.Dodecahedron(), "Додекаэдр"));

            SetPerspectiveCommand = new RelayCommand(() =>
            {
                Camera.Projection = ProjectionMode.Perspective;
                Projection = ProjectionMode.Perspective;
                Status = "Перспективная проекция";
            });
            SetAxonometricCommand = new RelayCommand(() =>
            {
                Camera.Projection = ProjectionMode.Axonometric;
                Projection = ProjectionMode.Axonometric;
                Status = "Аксонометрическая проекция";
            });

            ApplyTranslationCommand = new RelayCommand(() =>
            {
                Apply(ModelTransforms.Translate(TranslateX, TranslateY, TranslateZ));
                Status = $"Смещение ({TranslateX}; {TranslateY}; {TranslateZ})";
            });

            ApplyRotationAroundOriginCommand = new RelayCommand(() =>
            {
                Apply(ModelTransforms.RotateAroundOrigin(RotationAxis, RotationAngle));
                Status = $"Поворот вокруг {RotationAxis} на {RotationAngle}° (начало координат)";
            });

            ApplyScaleAroundOriginCommand = new RelayCommand(() =>
            {
                Apply(ModelTransforms.ScaleAroundOrigin(ScaleFactor));
                Status = $"Масштаб ×{ScaleFactor} (от начала координат)";
            });

            ApplyReflectionCommand = new RelayCommand(() =>
            {
                Apply(ModelTransforms.Reflect(ReflectionPlane));
                Status = $"Отражение относительно {ReflectionPlane}";
            });

            ApplyScaleAroundCenterCommand = new RelayCommand(() =>
            {
                Apply(ModelTransforms.ScaleAroundCenter(Model, CenterScaleFactor));
                Status = $"Масштаб ×{CenterScaleFactor} (относительно центра)";
            });

            ApplyRotationAroundCenterAxisCommand = new RelayCommand(() =>
            {
                Apply(ModelTransforms.RotateAroundCenterAxis(
                    Model, CenterRotationAxis, CenterRotationAngle));
                Status = $"Поворот вокруг {CenterRotationAxis} на {CenterRotationAngle}° (ось через центр)";
            });

            ApplyRotationAroundCustomLineCommand = new RelayCommand(() =>
            {
                var a = new Point3(AxisPointX, AxisPointY, AxisPointZ);
                var b = new Point3(BAxisPointX, BAxisPointY, BAxisPointZ);
                if (Point3.Distance(a, b) < 1e-9)
                {
                    Status = "Ошибка: точки A и B совпадают";
                    return;
                }
                Apply(ModelTransforms.RotateAroundCustomLine(a, b, CustomRotationAngle));
                Status = $"Поворот вокруг A-B на {CustomRotationAngle}°";
            });

            LoadModelCommand = new AsyncRelayCommand(() => LoadAsync(_ownerWindow));
            SaveModelCommand = new AsyncRelayCommand(() => SaveAsync(_ownerWindow));
        }

        private void Apply(Matrix4 m)
        {
            Model.Apply(m);
            OnPropertyChanged(nameof(Model));
        }

        private void CreateModel(Polyhedron m, string name)
        {
            Model = m;
            Status = name;
        }

        private async Task LoadAsync(Window? owner)
        {
            if (owner is null) return;
            var file = await owner.StorageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    Title = "Открыть OBJ-модель",
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Wavefront OBJ")
                        { Patterns = new[] { "*.obj" } }
                    }
                });
            if (file.Count == 0) return;
            try
            {
                await using var s = await file[0].OpenReadAsync();
                Model = ObjImporter.Load(s);
                Status = $"Загружено: {file[0].Name}";
            }
            catch (Exception ex) { Status = "Ошибка загрузки: " + ex.Message; }
        }

        private async Task SaveAsync(Window? owner)
        {
            if (owner is null) return;
            var file = await owner.StorageProvider.SaveFilePickerAsync(
                new FilePickerSaveOptions
                {
                    Title = "Сохранить OBJ-модель",
                    SuggestedFileName = "model.obj",
                    DefaultExtension = "obj",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Wavefront OBJ")
                        { Patterns = new[] { "*.obj" } }
                    }
                });
            if (file is null) return;
            try
            {
                await using var s = await file.OpenWriteAsync();
                ObjExporter.Save(Model, s);
                Status = $"Сохранено: {file.Name}";
            }
            catch (Exception ex) { Status = "Ошибка сохранения: " + ex.Message; }
        }

        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        // ---- ЭТО и был баг: Set<T> отсутствовал ----
        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }
    }
}