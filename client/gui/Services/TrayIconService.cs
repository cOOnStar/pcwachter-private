using DrawingColor = System.Drawing.Color;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingIcon = System.Drawing.Icon;
using DrawingPen = System.Drawing.Pen;
using DrawingSolidBrush = System.Drawing.SolidBrush;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PCWachter.Desktop.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly DrawingIcon _baseIcon;
    private readonly ToolStripMenuItem _scanMenuItem;
    private DrawingIcon? _currentIcon;

    public TrayIconService(Action openDashboardAction, Action startScanAction, Action exitAction)
    {
        _baseIcon = LoadBaseIcon();
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "PCWaechter"
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("Dashboard öffnen", null, (_, _) => openDashboardAction());
        _scanMenuItem = new ToolStripMenuItem("Scan starten", null, (_, _) => startScanAction());
        menu.Items.Add(_scanMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => exitAction());
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => openDashboardAction();

        UpdateStatus("good", 0, 0, true);
    }

    public void UpdateStatus(string state, int unresolvedCount, int unreadCount, bool canStartScan)
    {
        _scanMenuItem.Enabled = canStartScan;
        DrawingColor badgeColor = state switch
        {
            "critical" => DrawingColor.FromArgb(234, 67, 53),
            "warning" => DrawingColor.FromArgb(249, 173, 49),
            _ => DrawingColor.FromArgb(54, 181, 82)
        };

        ReplaceIcon(CreateBadgedIcon(_baseIcon, badgeColor));

        string stateLabel = state switch
        {
            "critical" => "kritisch",
            "warning" => "warnung",
            _ => "ok"
        };

        _notifyIcon.Text = BuildTooltipText(stateLabel, unresolvedCount, unreadCount);
    }

    private static string BuildTooltipText(string stateLabel, int unresolvedCount, int unreadCount)
    {
        string text = $"PCWaechter | Zustand: {stateLabel} | Offen: {unresolvedCount} | Hinweis: {unreadCount}";
        return text.Length > 63 ? text[..63] : text;
    }

    private void ReplaceIcon(DrawingIcon icon)
    {
        _notifyIcon.Icon = icon;
        _currentIcon?.Dispose();
        _currentIcon = icon;
    }

    private static DrawingIcon CreateBadgedIcon(DrawingIcon baseIcon, DrawingColor badgeColor)
    {
        using DrawingBitmap bitmap = baseIcon.ToBitmap();
        using DrawingGraphics graphics = DrawingGraphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var badgeRect = new System.Drawing.Rectangle(bitmap.Width - 14, bitmap.Height - 14, 12, 12);
        using var fill = new DrawingSolidBrush(badgeColor);
        using var outline = new DrawingPen(DrawingColor.White, 1.2f);
        graphics.FillEllipse(fill, badgeRect);
        graphics.DrawEllipse(outline, badgeRect);

        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using DrawingIcon temp = DrawingIcon.FromHandle(hIcon);
            return (DrawingIcon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    private static DrawingIcon LoadBaseIcon()
    {
        Assembly assembly = typeof(TrayIconService).Assembly;
        string? resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith("tray.ico", StringComparison.OrdinalIgnoreCase));

        if (resourceName is not null)
        {
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                try
                {
                    using DrawingIcon icon = new DrawingIcon(stream);
                    return (DrawingIcon)icon.Clone();
                }
                catch (ArgumentException)
                {
                    // Fall through to a safe default icon when the embedded icon cannot be parsed.
                }
            }
        }

        return System.Drawing.SystemIcons.Shield;
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon = null;
        _currentIcon?.Dispose();
        _baseIcon.Dispose();
        _notifyIcon.Dispose();
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr handle);
}

