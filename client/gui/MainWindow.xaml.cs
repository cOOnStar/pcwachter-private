using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PCWachter.Desktop.Services;
using PCWachter.Desktop.ViewModels;

namespace PCWachter.Desktop;

public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private static readonly IntPtr MonitorDefaultToNearest = new(2);
    private const double StartupPadding = 16;

    private readonly AppUiStateStore _uiStateStore;
    private readonly AppUiState _uiState;
    private readonly MainViewModel _viewModel;
    private readonly TrayIconService _trayIconService;
    private readonly bool _restoreMaximized;

    public MainWindow()
    {
        _uiStateStore = new AppUiStateStore();
        _uiState = _uiStateStore.Load();
        _restoreMaximized = _uiState.WindowMaximized;

        InitializeComponent();
        ApplyPersistedWindowState();

        _viewModel = new MainViewModel(_uiStateStore, _uiState);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _trayIconService = new TrayIconService(OpenDashboardFromTray, TriggerScanFromTray, CloseFromTray);
        UpdateTrayStatus();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        if (PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(WindowProc);
        }

        EnsureWindowFitsWorkArea();

        if (_restoreMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Max_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        EnsureWindowVisibleOnStartup();
        await _viewModel.InitializeAsync();
        UpdateTrayStatus();
    }

    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        PersistWindowState();
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _trayIconService.Dispose();
        await _viewModel.DisposeAsync();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(MainViewModel.CurrentPage), StringComparison.Ordinal))
        {
            AnimateContentTransition();
        }
        else if (string.Equals(e.PropertyName, nameof(MainViewModel.CriticalFindingCount), StringComparison.Ordinal)
                 || string.Equals(e.PropertyName, nameof(MainViewModel.WarningFindingCount), StringComparison.Ordinal)
                 || string.Equals(e.PropertyName, nameof(MainViewModel.UnreadNotificationCount), StringComparison.Ordinal)
                 || string.Equals(e.PropertyName, nameof(MainViewModel.IsBusy), StringComparison.Ordinal)
                 || string.Equals(e.PropertyName, nameof(MainViewModel.IsDemoMode), StringComparison.Ordinal))
        {
            UpdateTrayStatus();
        }
    }

    private void AnimateContentTransition()
    {
        MainContentHost.Opacity = 0.0;
        if (MainContentHost.RenderTransform is not TranslateTransform)
        {
            MainContentHost.RenderTransform = new TranslateTransform();
        }

        var translate = (TranslateTransform)MainContentHost.RenderTransform;
        translate.Y = 10;

        var fade = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(170),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation
        {
            From = 10,
            To = 0,
            Duration = TimeSpan.FromMilliseconds(170),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        MainContentHost.BeginAnimation(OpacityProperty, fade);
        translate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            UpdateMaximizedBounds(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static void UpdateMaximizedBounds(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var workArea = monitorInfo.WorkArea;
        var monitorArea = monitorInfo.MonitorArea;

        mmi.MaxPosition.X = Math.Abs(workArea.Left - monitorArea.Left);
        mmi.MaxPosition.Y = Math.Abs(workArea.Top - monitorArea.Top);
        mmi.MaxSize.X = Math.Abs(workArea.Right - workArea.Left);
        mmi.MaxSize.Y = Math.Abs(workArea.Bottom - workArea.Top);

        Marshal.StructureToPtr(mmi, lParam, true);
    }

    private void ApplyPersistedWindowState()
    {
        if (_uiState.WindowWidth >= 640 && _uiState.WindowWidth <= 5000)
        {
            Width = _uiState.WindowWidth;
        }

        if (_uiState.WindowHeight >= 480 && _uiState.WindowHeight <= 4000)
        {
            Height = _uiState.WindowHeight;
        }

        if (_uiState.WindowLeft.HasValue && _uiState.WindowTop.HasValue)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = _uiState.WindowLeft.Value;
            Top = _uiState.WindowTop.Value;
        }
    }

    private void PersistWindowState()
    {
        Rect bounds = WindowState == WindowState.Maximized
            ? RestoreBounds
            : new Rect(Left, Top, Width, Height);

        _viewModel.PersistUiState(
            bounds.Width,
            bounds.Height,
            bounds.Left,
            bounds.Top,
            WindowState == WindowState.Maximized);
    }

    private void EnsureWindowFitsWorkArea()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var workArea = monitorInfo.WorkArea;
        double workWidth = Math.Max(0, workArea.Right - workArea.Left);
        double workHeight = Math.Max(0, workArea.Bottom - workArea.Top);

        double maxWidth = Math.Max(480, workWidth - (StartupPadding * 2));
        double maxHeight = Math.Max(420, workHeight - (StartupPadding * 2));

        if (Width > maxWidth)
        {
            Width = maxWidth;
        }

        if (Height > maxHeight)
        {
            Height = maxHeight;
        }

        if (_uiState.WindowLeft.HasValue && _uiState.WindowTop.HasValue)
        {
            double desiredLeft = Math.Clamp(_uiState.WindowLeft.Value, workArea.Left + StartupPadding, workArea.Right - Width - StartupPadding);
            double desiredTop = Math.Clamp(_uiState.WindowTop.Value, workArea.Top + StartupPadding, workArea.Bottom - Height - StartupPadding);
            Left = desiredLeft;
            Top = desiredTop;
            return;
        }

        Left = workArea.Left + Math.Max(0, (workWidth - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workHeight - Height) / 2);
    }

    private void EnsureWindowVisibleOnStartup()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = _restoreMaximized ? WindowState.Maximized : WindowState.Normal;
        }

        EnsureWindowFitsWorkArea();

        Rect bounds = WindowState == WindowState.Maximized
            ? RestoreBounds
            : new Rect(Left, Top, Width, Height);

        if (!HasEnoughVisibleArea(bounds))
        {
            WindowState = WindowState.Normal;
            CenterWindowOnPrimaryWorkArea();
        }

        Activate();

        // Bring to foreground reliably when shell/shortcut starts the app minimized or unfocused.
        Topmost = true;
        Topmost = false;
        Focus();
    }

    private void CenterWindowOnPrimaryWorkArea()
    {
        Rect workArea = SystemParameters.WorkArea;

        double maxWidth = Math.Max(640, workArea.Width - (StartupPadding * 2));
        double maxHeight = Math.Max(480, workArea.Height - (StartupPadding * 2));
        Width = Math.Clamp(Width, 640, maxWidth);
        Height = Math.Clamp(Height, 480, maxHeight);

        Left = workArea.Left + Math.Max(0, (workArea.Width - Width) / 2);
        Top = workArea.Top + Math.Max(0, (workArea.Height - Height) / 2);
    }

    private static bool HasEnoughVisibleArea(Rect bounds)
    {
        if (!IsFinite(bounds.Left) ||
            !IsFinite(bounds.Top) ||
            !IsFinite(bounds.Width) ||
            !IsFinite(bounds.Height) ||
            bounds.Width < 220 ||
            bounds.Height < 160)
        {
            return false;
        }

        Rect virtualBounds = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        Rect visibleBounds = Rect.Intersect(bounds, virtualBounds);
        return !visibleBounds.IsEmpty && visibleBounds.Width >= 220 && visibleBounds.Height >= 160;
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private void OpenDashboardFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Show();
            Activate();
            _viewModel.OpenDashboard();
        });
    }

    private void TriggerScanFromTray()
    {
        Dispatcher.Invoke(() =>
        {
            if (_viewModel.TriggerScanCommand.CanExecute(null))
            {
                _viewModel.TriggerScanCommand.Execute(null);
            }
        });
    }

    private void CloseFromTray()
    {
        Dispatcher.Invoke(Close);
    }

    private void UpdateTrayStatus()
    {
        string state = _viewModel.HasCriticalFindings
            ? "critical"
            : _viewModel.HasWarningFindings
                ? "warning"
                : "good";

        _trayIconService.UpdateStatus(
            state,
            _viewModel.UnresolvedIssueCount,
            _viewModel.UnreadNotificationCount,
            !_viewModel.IsBusy);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Point MaxSize;
        public Point MaxPosition;
        public Point MinTrackSize;
        public Point MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public RectStruct MonitorArea;
        public RectStruct WorkArea;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RectStruct
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("User32")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, IntPtr flags);
}
