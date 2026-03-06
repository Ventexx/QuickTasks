// QuickTasks.cs — single-file .NET 8 WPF app
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Path = System.IO.Path;
using File = System.IO.File;

namespace TaskSnips
{
    // --- Data classes ---
    public class TaskEntry
    {
        public string Text { get; set; } = "";
        public bool Starred { get; set; } = false;
        public long StarOrder { get; set; } = 0;
        public int Order { get; set; } = 0;
        public string? Group { get; set; } = null;
    }

    public class AppSettings
    {
        public string Theme { get; set; } = "Dark";
        public string GlobalHotkey { get; set; } = "Ctrl+Alt+,";
        public string NewTaskHotkey { get; set; } = "Ctrl+N";
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public Dictionary<string, bool> CollapsedGroups { get; set; } = new();
        public HashSet<string> StarredGroups { get; set; } = new();
        public double EditorLeft { get; set; } = double.NaN;
        public double EditorTop { get; set; } = double.NaN;
    }

    public class ThemeColors
    {
        public string Background { get; set; } = "";
        public string Foreground { get; set; } = "";
        public string Border { get; set; } = "";
        public string SearchBg { get; set; } = "";
        public string HighlightBg { get; set; } = "";
        public string AccentColor { get; set; } = "";
        public string AccentBorder { get; set; } = "";
    }

    public static class Themes
    {
        public static Dictionary<string, ThemeColors> GetThemes()
        {
            return new Dictionary<string, ThemeColors>
            {
                ["Catppuccin Latte"] = new ThemeColors
                {
                    Background = "#EFF1F5",
                    Foreground = "#4C4F69",
                    Border = "#CCD0DA",
                    SearchBg = "#E6E9EF",
                    HighlightBg = "#DCE0E8",
                    AccentColor = "#1E66F5",
                    AccentBorder = "#1E55D0"
                },
                ["Catppuccin Frappe"] = new ThemeColors
                {
                    Background = "#303446",
                    Foreground = "#C6D0F5",
                    Border = "#626880",
                    SearchBg = "#292C3C",
                    HighlightBg = "#414559",
                    AccentColor = "#8CAAEE",
                    AccentBorder = "#7A96E0"
                },
                ["Catppuccin Macchiato"] = new ThemeColors
                {
                    Background = "#24273A",
                    Foreground = "#CAD3F5",
                    Border = "#5B6078",
                    SearchBg = "#1E2030",
                    HighlightBg = "#363A4F",
                    AccentColor = "#8AADF4",
                    AccentBorder = "#7A9AE6"
                },
                ["Catppuccin Mocha"] = new ThemeColors
                {
                    Background = "#1E1E2E",
                    Foreground = "#CDD6F4",
                    Border = "#585B70",
                    SearchBg = "#181825",
                    HighlightBg = "#313244",
                    AccentColor = "#89B4FA",
                    AccentBorder = "#7AA2F7"
                },
                ["Dark"] = new ThemeColors
                {
                    Background = "#222222",
                    Foreground = "#FFFFFF",
                    Border = "#555555",
                    SearchBg = "#333333",
                    HighlightBg = "#444444",
                    AccentColor = "#00FFFF",
                    AccentBorder = "#00CCCC"
                },
                ["Dracula"] = new ThemeColors
                {
                    Background = "#282A36",
                    Foreground = "#F8F8F2",
                    Border = "#6272A4",
                    SearchBg = "#44475A",
                    HighlightBg = "#44475A",
                    AccentColor = "#8BE9FD",
                    AccentBorder = "#6DCFE0"
                },
                ["Gruvbox Dark"] = new ThemeColors
                {
                    Background = "#282828",
                    Foreground = "#EBDBB2",
                    Border = "#504945",
                    SearchBg = "#3C3836",
                    HighlightBg = "#504945",
                    AccentColor = "#83A598",
                    AccentBorder = "#6F8A88"
                },
                ["Gruvbox Light"] = new ThemeColors
                {
                    Background = "#FBF1C7",
                    Foreground = "#3C3836",
                    Border = "#D5C4A1",
                    SearchBg = "#F2E5BC",
                    HighlightBg = "#EBDBB2",
                    AccentColor = "#458588",
                    AccentBorder = "#3A7178"
                },
                ["Light"] = new ThemeColors
                {
                    Background = "#FFFFFF",
                    Foreground = "#000000",
                    Border = "#CCCCCC",
                    SearchBg = "#F0F0F0",
                    HighlightBg = "#E0E0E0",
                    AccentColor = "#40E0D0",
                    AccentBorder = "#20B2AA"
                }
            };
        }

        // Returns a slightly dimmed version of the accent color for the Settings button
        public static Color DimAccentColor(Color c)
        {
            float factor = 0.78f;
            return Color.FromRgb(
                (byte)(c.R * factor),
                (byte)(c.G * factor),
                (byte)(c.B * factor));
        }
    }

    // --- App entrypoint ---
    public partial class App : Application
    {
        [STAThread]
        public static void Main()
        {
            new App().Run(new MainWindow());
        }
    }

    // --- Main window ---
    public partial class MainWindow : Window
    {
        const int HOTKEY_ID = 9000;
        const int EMERGENCY_HOTKEY_ID = 9001;

        [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        string dataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TaskSnips");
        string openTasksFile => Path.Combine(dataDir, "open_tasks.json");
        string finishedTasksFile => Path.Combine(dataDir, "finished_tasks.txt");
        string settingsFile => Path.Combine(dataDir, "settings.json");

        List<TaskEntry> tasks = new();
        AppSettings settings = new();

        Border mainBorder = null!;
        StackPanel contentPanel = null!;
        Border addButton = null!;
        Border settingsButton = null!;
        TextBlock openTaskCountText = null!;
        TextBlock openTasksLabel = null!;
        TextBlock finishedTaskCountText = null!;
        Border _openBtn = null!;
        Border _doneBtn = null!;
        TextBlock _finishedLabel = null!;
        Dictionary<TaskEntry, Border> taskToBorder = new();

        bool childWindowOpen = false;
        long starCounter = 0;
        List<DateTime> emergencyPresses = new();
        Action? _updateCounters;

        public MainWindow()
        {
            Title = "QuickTasks";
            Width = 450;
            MinHeight = 80;
            MaxHeight = 600;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;

            LoadSettings();
            LoadTasks();
            ApplyWindowPosition();

            SourceInitialized += (_, _) =>
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                var src = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
                src.AddHook(WndProc);
                RegisterGlobalHotkey(hwnd);
            };

            Deactivated += (_, _) =>
            {
                if (!childWindowOpen)
                    Hide();
            };

            PreviewKeyDown += MainWindow_PreviewKeyDown;

            Content = BuildUI();
            ApplyTheme();
            BuildTaskList();
        }

        void ApplyWindowPosition()
        {
            if (!double.IsNaN(settings.WindowLeft) && !double.IsNaN(settings.WindowTop))
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = settings.WindowLeft;
                Top = settings.WindowTop;
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            LocationChanged += (_, _) =>
            {
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
                SaveSettings();
            };
        }

        void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.N && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (settings.NewTaskHotkey?.Equals("Ctrl+N", StringComparison.OrdinalIgnoreCase) ?? true)
                {
                    OpenEditor(null);
                    e.Handled = true;
                }
            }
        }

        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;
            if (msg == WM_HOTKEY)
            {
                var id = wParam.ToInt32();
                if (id == HOTKEY_ID)
                {
                    if (IsVisible) Hide();
                    else { Show(); Activate(); Focus(); }
                    handled = true;
                }
                else if (id == EMERGENCY_HOTKEY_ID)
                {
                    EmergencyHotkeyPressed();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        void EmergencyHotkeyPressed()
        {
            var now = DateTime.Now;
            emergencyPresses.Add(now);
            emergencyPresses.RemoveAll(t => (now - t).TotalSeconds > 3);
            if (emergencyPresses.Count >= 3)
            {
                settings.GlobalHotkey = "Ctrl+Alt+,";
                SaveSettings();
                Show();
                Activate();
                Focus();
                emergencyPresses.Clear();
            }
        }

        void RegisterGlobalHotkey(IntPtr hwnd)
        {
            try
            {
                UnregisterHotKey(hwnd, HOTKEY_ID);
                UnregisterHotKey(hwnd, EMERGENCY_HOTKEY_ID);
            }
            catch { }

            (uint mod, uint key) = ParseHotkey(settings.GlobalHotkey);
            if (key == 0)
            {
                (mod, key) = ParseHotkey("Ctrl+Alt+,");
                settings.GlobalHotkey = "Ctrl+Alt+,";
                SaveSettings();
            }

            RegisterHotKey(hwnd, HOTKEY_ID, mod, key);
            RegisterHotKey(hwnd, EMERGENCY_HOTKEY_ID, 0x0002 | 0x0004, 0xBE);
        }

        (uint mod, uint key) ParseHotkey(string s)
        {
            uint mod = 0, key = 0;
            var parts = s.Split('+', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).ToArray();
            foreach (var p in parts)
            {
                switch (p.ToLowerInvariant())
                {
                    case "ctrl":  mod |= 0x0002; break;
                    case "alt":   mod |= 0x0001; break;
                    case "shift": mod |= 0x0004; break;
                    default:
                        var token = p.ToUpperInvariant();
                        if (token == ".")  key = 0xBE;
                        else if (token == ",") key = 0xBC;
                        else
                        {
                            if (Enum.TryParse<Key>(token, true, out var parsed))
                                key = (uint)KeyInterop.VirtualKeyFromKey(parsed);
                            else
                                key = 0;
                        }
                        break;
                }
            }
            return (mod, key);
        }

        // ---------------------------------------------------------------
        // UI building
        // ---------------------------------------------------------------
        FrameworkElement BuildUI()
        {
            var outer = new Border { Background = Brushes.Transparent, Padding = new Thickness(0) };

            mainBorder = new Border
            {
                Padding = new Thickness(8),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(2)
            };

            var dock = new DockPanel();

            // ── Drag handle (mirrors QuickSnips exactly) ──────────────
            var dragHandle = new Border
            {
                Height = 20,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Margin = new Thickness(0, 0, 0, 6)
            };
            dragHandle.MouseLeftButtonDown += (_, ev) =>
            {
                if (ev.ChangedButton == MouseButton.Left)
                    DragMove();
            };

            var dragIcon = new Border
            {
                Width = 30,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dragLines = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dragLine1 = new Border
            {
                Height = 2,
                Width = 20,
                Background = Brushes.DarkGray,
                Margin = new Thickness(0, 3, 0, 1)
            };
            var dragLine2 = new Border
            {
                Height = 2,
                Width = 20,
                Background = Brushes.DarkGray,
                Margin = new Thickness(0, 1, 0, 3)
            };

            dragLines.Children.Add(dragLine1);
            dragLines.Children.Add(dragLine2);
            dragIcon.Child = dragLines;
            dragHandle.Child = dragIcon;

            DockPanel.SetDock(dragHandle, Dock.Top);
            dock.Children.Add(dragHandle);

            // ── Bottom status bar ──────────────────────────────────────
            var statusBar = new Border
            {
                Height = 44,
                Margin = new Thickness(0, 6, 0, 0),
                BorderThickness = new Thickness(0, 1, 0, 0)
            };
            DockPanel.SetDock(statusBar, Dock.Bottom);

            var statusGrid = new Grid();
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left: Settings button + Add Task button
            var leftPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };

            settingsButton = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Focusable = true,
                FocusVisualStyle = null,
                Margin = new Thickness(0, 0, 6, 0)
            };
            var settingsIcon = new TextBlock
            {
                Text = "🛠️",
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            settingsButton.Child = settingsIcon;
            settingsButton.MouseLeftButtonUp += (_, _) => OpenSettings();
            settingsButton.GotFocus += (_, _) =>
            {
                settingsButton.BorderThickness = new Thickness(2);
                var th = GetTheme();
                settingsButton.BorderBrush = (Brush)new BrushConverter().ConvertFromString(th.AccentBorder)!;
            };
            settingsButton.LostFocus += (_, _) =>
            {
                settingsButton.BorderThickness = new Thickness(0);
                settingsButton.BorderBrush = Brushes.Transparent;
            };
            leftPanel.Children.Add(settingsButton);

            addButton = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Focusable = true,
                FocusVisualStyle = null
            };
            var addIcon = new TextBlock
            {
                Text = "+",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0),
                Foreground = Brushes.Black
            };
            addButton.Child = addIcon;
            addButton.MouseLeftButtonUp += (_, _) => OpenEditor(null);
            addButton.GotFocus += (_, _) =>
            {
                addButton.BorderThickness = new Thickness(2);
                var th = GetTheme();
                addButton.BorderBrush = (Brush)new BrushConverter().ConvertFromString(th.AccentBorder)!;
            };
            addButton.LostFocus += (_, _) =>
            {
                addButton.BorderThickness = new Thickness(0);
                addButton.BorderBrush = Brushes.Transparent;
            };
            leftPanel.Children.Add(addButton);

            Grid.SetColumn(leftPanel, 0);
            statusGrid.Children.Add(leftPanel);

            // Right: label + number-badge button, side by side, matching left button sizing
            var rightPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            openTasksLabel = new TextBlock
            {
                Text = "Open",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(0, 0, 4, 0)
            };

            openTaskCountText = new TextBlock
            {
                Text = "0",
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            };
            var openBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                Child = openTaskCountText
            };
            openBtn.MouseLeftButtonUp += (_, _) =>
            {
                try
                {
                    var readable = Path.ChangeExtension(openTasksFile, ".txt");
                    if (!File.Exists(readable)) SaveTasks();
                    Process.Start(new ProcessStartInfo(readable) { UseShellExecute = true });
                }
                catch { }
            };
            openBtn.GotFocus += (_, _) => { var th = GetTheme(); openBtn.BorderThickness = new Thickness(2); openBtn.BorderBrush = (Brush)new BrushConverter().ConvertFromString(th.AccentBorder)!; };
            openBtn.LostFocus += (_, _) => { openBtn.BorderThickness = new Thickness(0); openBtn.BorderBrush = Brushes.Transparent; };

            var finishedLabel = new TextBlock
            {
                Text = "Done",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11,
                Margin = new Thickness(0, 0, 4, 0)
            };

            finishedTaskCountText = new TextBlock
            {
                Text = "0",
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 11
            };
            var doneBtn = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(0),
                BorderBrush = Brushes.Transparent,
                Cursor = Cursors.Hand,
                Child = finishedTaskCountText
            };
            doneBtn.MouseLeftButtonUp += (_, _) =>
            {
                try
                {
                    if (!File.Exists(finishedTasksFile)) File.WriteAllText(finishedTasksFile, "");
                    Process.Start(new ProcessStartInfo(finishedTasksFile) { UseShellExecute = true });
                }
                catch { }
            };
            doneBtn.GotFocus += (_, _) => { var th = GetTheme(); doneBtn.BorderThickness = new Thickness(2); doneBtn.BorderBrush = (Brush)new BrushConverter().ConvertFromString(th.AccentBorder)!; };
            doneBtn.LostFocus += (_, _) => { doneBtn.BorderThickness = new Thickness(0); doneBtn.BorderBrush = Brushes.Transparent; };

            rightPanel.Children.Add(openTasksLabel);
            rightPanel.Children.Add(openBtn);
            rightPanel.Children.Add(finishedLabel);
            rightPanel.Children.Add(doneBtn);

            // Store references for theme updates
            _openBtn = openBtn;
            _doneBtn = doneBtn;
            _finishedLabel = finishedLabel;

            Grid.SetColumn(rightPanel, 2);
            statusGrid.Children.Add(rightPanel);

            statusBar.Child = statusGrid;
            dock.Children.Add(statusBar);

            // ── Task scroll area (fills all remaining space) ───────────
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                FocusVisualStyle = null,
                Focusable = false,
                MaxHeight = 510
            };
            contentPanel = new StackPanel { FocusVisualStyle = null };
            scroll.Content = contentPanel;
            dock.Children.Add(scroll); // last = fills remaining space

            mainBorder.Child = dock;
            outer.Child = mainBorder;

            _updateCounters = () =>
            {
                openTaskCountText.Text = tasks.Count.ToString();
                // Count finished tasks from file
                try
                {
                    if (File.Exists(finishedTasksFile))
                    {
                        var lines = File.ReadAllLines(finishedTasksFile).Where(l => !string.IsNullOrWhiteSpace(l)).Count();
                        finishedTaskCountText.Text = lines.ToString();
                    }
                    else
                    {
                        finishedTaskCountText.Text = "0";
                    }
                }
                catch { finishedTaskCountText.Text = "?"; }
            };

            Dispatcher.BeginInvoke(new Action(() => _updateCounters?.Invoke()), DispatcherPriority.Background);

            return outer;
        }

        ThemeColors GetTheme()
        {
            var themes = Themes.GetThemes();
            return themes.ContainsKey(settings.Theme) ? themes[settings.Theme] : themes["Dark"];
        }

        void ApplyTheme()
        {
            var theme = GetTheme();

            var bgBrush = (Brush)new BrushConverter().ConvertFromString(theme.Background)!;
            var fgBrush = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!;
            Foreground = fgBrush;

            if (mainBorder != null)
            {
                mainBorder.Background = bgBrush;
                mainBorder.BorderBrush = (Brush)new BrushConverter().ConvertFromString(theme.Border)!;
            }

            if (addButton != null)
            {
                var accentColor = (Color)ColorConverter.ConvertFromString(theme.AccentColor);
                addButton.Background = new SolidColorBrush(accentColor);
            }

            if (settingsButton != null)
            {
                var accentColor = (Color)ColorConverter.ConvertFromString(theme.AccentColor);
                var dimColor = Themes.DimAccentColor(accentColor);
                settingsButton.Background = new SolidColorBrush(dimColor);
            }

            if (openTasksLabel != null)
                openTasksLabel.Foreground = fgBrush;

            if (openTaskCountText != null)
                openTaskCountText.Foreground = Brushes.Black;

            if (finishedTaskCountText != null)
                finishedTaskCountText.Foreground = Brushes.Black;

            if (_finishedLabel != null)
                _finishedLabel.Foreground = fgBrush;

            if (_openBtn != null)
            {
                var accentColor = (Color)ColorConverter.ConvertFromString(theme.AccentColor);
                var dimColor = Themes.DimAccentColor(accentColor);
                _openBtn.Background = new SolidColorBrush(accentColor);
                _doneBtn.Background = new SolidColorBrush(dimColor);
            }

            // Update status bar border color
            if (mainBorder?.Child is DockPanel dp)
            {
                foreach (var child in dp.Children)
                {
                    if (child is Border sb && sb.BorderThickness == new Thickness(0, 1, 0, 0))
                    {
                        sb.BorderBrush = (Brush)new BrushConverter().ConvertFromString(theme.Border)!;
                        break;
                    }
                }
            }
        }

        void BuildTaskList()
        {
            contentPanel.Children.Clear();
            taskToBorder.Clear();

            var grouped = tasks
                .Where(t => !string.IsNullOrWhiteSpace(t.Group))
                .GroupBy(t => t.Group ?? "")
                .OrderByDescending(g => settings.StarredGroups != null && settings.StarredGroups.Contains(g.Key))
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var theme = GetTheme();

            foreach (var g in grouped)
            {
                var groupName = g.Key!;
                bool isGroupStarred = settings.StarredGroups != null && settings.StarredGroups.Contains(groupName);

                var header = new Border
                {
                    Padding = new Thickness(6),
                    Margin = new Thickness(0, 2, 0, 2),
                    Cursor = Cursors.Hand,
                    Background = (Brush)new BrushConverter().ConvertFromString(theme.HighlightBg)!,
                    Tag = groupName  // used for drag-drop targeting
                };
                var headerPanel = new DockPanel();
                var arrow = new TextBlock
                {
                    Text = settings.CollapsedGroups.ContainsKey(groupName) && settings.CollapsedGroups[groupName] ? "▶" : "▼",
                    Width = 20,
                    Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!
                };

                // Star indicator for starred groups
                if (isGroupStarred)
                {
                    var starMark = new TextBlock
                    {
                        Text = "⭐",
                        FontSize = 11,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 4, 0)
                    };
                    DockPanel.SetDock(starMark, Dock.Right);
                    headerPanel.Children.Add(starMark);
                }

                var nameText = new TextBlock
                {
                    Text = groupName,
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!
                };
                headerPanel.Children.Add(arrow);
                headerPanel.Children.Add(nameText);
                header.Child = headerPanel;

                header.MouseLeftButtonUp += (_, e) =>
                {
                    if (e.ClickCount == 2)
                    {
                        e.Handled = true;
                        StartInlineGroupEdit(g.Key!, header, headerPanel, nameText, theme);
                        return;
                    }
                    var current = settings.CollapsedGroups.ContainsKey(groupName) && settings.CollapsedGroups[groupName];
                    settings.CollapsedGroups[groupName] = !current;
                    SaveSettings();
                    BuildTaskList();
                };

                header.MouseRightButtonUp += (_, _) =>
                {
                    var menu = new ContextMenu();

                    var starItem = new MenuItem { Header = isGroupStarred ? "Unstar group" : "⭐ Star group" };
                    starItem.Click += (_, _) =>
                    {
                        settings.StarredGroups ??= new HashSet<string>();
                        if (isGroupStarred) settings.StarredGroups.Remove(groupName);
                        else settings.StarredGroups.Add(groupName);
                        SaveSettings();
                        BuildTaskList();
                    };

                    var renameItem = new MenuItem { Header = "Rename group" };
                    renameItem.Click += (_, _) =>
                        StartInlineGroupEdit(groupName, header, headerPanel, nameText, theme);

                    menu.Items.Add(starItem);
                    menu.Items.Add(renameItem);
                    menu.IsOpen = true;
                };

                contentPanel.Children.Add(header);

                bool collapsed = settings.CollapsedGroups.ContainsKey(groupName) && settings.CollapsedGroups[groupName];
                if (!collapsed)
                {
                    var sorted = g.ToList()
                        .OrderByDescending(t => t.Starred)
                        .ThenBy(t => t.Starred ? t.StarOrder : t.Order)
                        .ThenBy(t => t.Order)
                        .ToList();

                    for (int i = 0; i < sorted.Count; i++)
                    {
                        if (i > 0) contentPanel.Children.Add(MakeSeparator(theme));
                        var border = CreateTaskBorder(sorted[i], indent: 16);
                        contentPanel.Children.Add(border);
                        taskToBorder[sorted[i]] = border;
                    }
                }
            }

            var ungrouped = tasks.Where(t => string.IsNullOrWhiteSpace(t.Group))
                .OrderByDescending(t => t.Starred)
                .ThenBy(t => t.Starred ? t.StarOrder : t.Order)
                .ThenBy(t => t.Order)
                .ToList();

            bool anyGrouped = contentPanel.Children.Count > 0;
            for (int i = 0; i < ungrouped.Count; i++)
            {
                // Separator between ungrouped tasks, but NOT between last group and first ungrouped
                if (i > 0) contentPanel.Children.Add(MakeSeparator(theme));
                var border = CreateTaskBorder(ungrouped[i], indent: 0);
                contentPanel.Children.Add(border);
                taskToBorder[ungrouped[i]] = border;
            }

            if (tasks.Count == 0)
            {
                var placeholder = new TextBlock
                {
                    Text = "Add tasks with + or press Ctrl+N when active.",
                    Foreground = Brushes.Gray,
                    FontStyle = FontStyles.Italic,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(4, 12, 4, 4)
                };
                contentPanel.Children.Add(placeholder);
            }

            _updateCounters?.Invoke();
        }

        Border MakeSeparator(ThemeColors theme)
        {
            var sep = new Border
            {
                Height = 1,
                Margin = new Thickness(8, 0, 8, 0),
                Opacity = 0.3
            };
            var borderColor = (Color)ColorConverter.ConvertFromString(theme.Border);
            sep.Background = new SolidColorBrush(borderColor);
            return sep;
        }

        Border CreateTaskBorder(TaskEntry task, double indent = 0)
        {
            var theme = GetTheme();

            var container = new Border
            {
                BorderThickness = new Thickness(0),
                Padding = new Thickness(6),
                Background = (Brush)new BrushConverter().ConvertFromString(theme.Background)!,
                Focusable = false,
                FocusVisualStyle = null,
                Tag = task  // store reference for drop targeting
            };

            var row = new DockPanel { Margin = new Thickness(indent, 0, 0, 0) };

            // Drag handle — shown on hover, right-docked
            var dragHandle = new TextBlock
            {
                Text = "⠿",
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Gray,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.SizeAll,
                Opacity = 0,
                ToolTip = "Drag to group"
            };
            DockPanel.SetDock(dragHandle, Dock.Right);
            row.Children.Add(dragHandle);

            var circle = new Border
            {
                Width = 20,
                Height = 20,
                CornerRadius = new CornerRadius(10),
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(theme.Border)!,
                BorderThickness = new Thickness(1),
                Background = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = Cursors.Hand
            };
            var checkText = new TextBlock
            {
                Text = "",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!
            };
            circle.Child = checkText;
            DockPanel.SetDock(circle, Dock.Left);
            row.Children.Add(circle);

            // Red exclamation between checkbox and text for starred tasks
            if (task.Starred)
            {
                var excl = new TextBlock
                {
                    Text = "❗",
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Red,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 4, 0)
                };
                DockPanel.SetDock(excl, Dock.Left);
                row.Children.Add(excl);
            }

            // Text block (double-click to inline edit)
            var tb = new TextBlock
            {
                Text = task.Text,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                FontWeight = FontWeights.Normal,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.IBeam
            };
            row.Children.Add(tb);

            container.Child = row;

            // hover highlight + drag handle visibility
            container.MouseEnter += (_, _) =>
            {
                container.Background = (Brush)new BrushConverter().ConvertFromString(theme.HighlightBg)!;
                dragHandle.Opacity = 1;
            };
            container.MouseLeave += (_, _) =>
            {
                container.Background = (Brush)new BrushConverter().ConvertFromString(theme.Background)!;
                dragHandle.Opacity = 0;
            };

            // Drag initiation from the handle only
            bool _dragging = false;
            Point _dragStart = new Point();
            dragHandle.MouseLeftButtonDown += (_, e) =>
            {
                _dragging = false;
                _dragStart = e.GetPosition(null);
                dragHandle.CaptureMouse();
                e.Handled = true;
            };
            dragHandle.MouseMove += (_, e) =>
            {
                if (e.LeftButton != MouseButtonState.Pressed) { dragHandle.ReleaseMouseCapture(); return; }
                var pos = e.GetPosition(null);
                if (!_dragging && (Math.Abs(pos.X - _dragStart.X) > 4 || Math.Abs(pos.Y - _dragStart.Y) > 4))
                {
                    _dragging = true;
                    _draggedTask = task;
                    _dragSourceContainer = container;
                    // Highlight drop zones
                    HighlightGroupDropZones(true);
                }
            };
            dragHandle.MouseLeftButtonUp += (_, e) =>
            {
                dragHandle.ReleaseMouseCapture();
                if (_dragging && _draggedTask != null)
                {
                    // Find which group header the mouse is over
                    var pos = e.GetPosition(contentPanel);
                    var dropped = HitTestGroupHeader(pos);
                    if (dropped != null)
                    {
                        _draggedTask.Group = dropped;
                        SaveTasks();
                        BuildTaskList();
                    }
                }
                _dragging = false;
                _draggedTask = null;
                _dragSourceContainer = null;
                HighlightGroupDropZones(false);
                e.Handled = true;
            };

            // Checkbox click marks finished (only on the circle area)
            circle.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                MarkTaskFinished(task, container);
            };

            // Double-click on text = inline edit
            tb.MouseLeftButtonDown += (_, e) =>
            {
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    StartInlineEdit(task, tb, container, row, theme);
                }
            };

            // right-click context menu on task
            container.MouseRightButtonUp += (_, _) =>
            {
                var menu = new ContextMenu();

                var rename = new MenuItem { Header = "Rename" };
                rename.Click += (_, _) => StartInlineEdit(task, tb, container, row, theme);

                var addGroup = new MenuItem { Header = "Set group" };
                addGroup.Click += (_, _) =>
                {
                    var input = PromptForText("Set group", "Enter group name (leave empty to remove from group):");
                    if (input != null)
                    {
                        task.Group = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
                        SaveTasks();
                        BuildTaskList();
                    }
                };

                var star = new MenuItem { Header = task.Starred ? "Unstar" : "⭐ Star" };
                star.Click += (_, _) =>
                {
                    if (task.Starred) { task.Starred = false; task.StarOrder = 0; }
                    else { task.Starred = true; starCounter++; task.StarOrder = starCounter; }
                    SaveTasks();
                    BuildTaskList();
                };

                var del = new MenuItem { Header = "Delete" };
                del.Click += (_, _) =>
                {
                    if (MessageBox.Show("Delete this task?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        tasks.Remove(task);
                        SaveTasks();
                        BuildTaskList();
                    }
                };

                menu.Items.Add(rename);
                menu.Items.Add(addGroup);
                menu.Items.Add(star);
                menu.Items.Add(del);
                menu.IsOpen = true;
            };

            return container;
        }

        void StartInlineEdit(TaskEntry task, TextBlock tb, Border container, DockPanel row, ThemeColors theme)
        {
            var editBox = new TextBox
            {
                Text = task.Text,
                Background = (Brush)new BrushConverter().ConvertFromString(theme.SearchBg)!,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                CaretBrush = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2),
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = false,
                MinWidth = 100,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Replace TextBlock with TextBox in the row
            var idx = row.Children.IndexOf(tb);
            row.Children.RemoveAt(idx);
            row.Children.Insert(idx, editBox);
            editBox.Focus();
            editBox.SelectAll();

            Action commit = () =>
            {
                var newText = editBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(newText))
                {
                    task.Text = newText;
                    SaveTasks();
                }
                BuildTaskList();
            };

            editBox.LostFocus += (_, _) => commit();
            editBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; commit(); }
                else if (e.Key == Key.Escape) { BuildTaskList(); }
            };
        }

        void StartInlineGroupEdit(string groupName, Border? header, DockPanel? headerPanel, TextBlock? nameText, ThemeColors theme)
        {
            if (header == null || headerPanel == null || nameText == null) return;

            var editBox = new TextBox
            {
                Text = groupName,
                Background = (Brush)new BrushConverter().ConvertFromString(theme.SearchBg)!,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                CaretBrush = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(2),
                MinWidth = 100,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var idx = headerPanel.Children.IndexOf(nameText);
            headerPanel.Children.RemoveAt(idx);
            headerPanel.Children.Insert(idx, editBox);
            editBox.Focus();
            editBox.SelectAll();

            Action commit = () =>
            {
                var newName = editBox.Text.Trim();
                if (!string.IsNullOrWhiteSpace(newName) && newName != groupName)
                {
                    foreach (var t in tasks.Where(t => t.Group == groupName))
                        t.Group = newName;
                    // Update collapsed and starred group keys
                    if (settings.CollapsedGroups.ContainsKey(groupName))
                    {
                        var val = settings.CollapsedGroups[groupName];
                        settings.CollapsedGroups.Remove(groupName);
                        settings.CollapsedGroups[newName] = val;
                    }
                    settings.StarredGroups ??= new HashSet<string>();
                    if (settings.StarredGroups.Contains(groupName))
                    {
                        settings.StarredGroups.Remove(groupName);
                        settings.StarredGroups.Add(newName);
                    }
                    SaveSettings();
                    SaveTasks();
                }
                BuildTaskList();
            };

            editBox.LostFocus += (_, _) => commit();
            editBox.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter) { e.Handled = true; commit(); }
                else if (e.Key == Key.Escape) { BuildTaskList(); }
            };
        }

        // Overload for task's group edit (from task context menu)
        void StartInlineGroupEdit(TaskEntry task, Border? unused)
        {
            // Find the group header in contentPanel and start inline edit
            var groupName = task.Group ?? "";
            foreach (var child in contentPanel.Children)
            {
                if (child is Border hdr && hdr.Child is DockPanel dp2)
                {
                    TextBlock? nt = null;
                    foreach (var c in dp2.Children) if (c is TextBlock tb2 && tb2.FontWeight == FontWeights.SemiBold) { nt = tb2; break; }
                    if (nt?.Text == groupName)
                    {
                        var theme = GetTheme();
                        StartInlineGroupEdit(groupName, hdr, dp2, nt, theme);
                        return;
                    }
                }
            }
            // Fallback: just prompt for new group assignment inline via a temp approach
            var input = PromptForText("Group name", "Enter group name:");
            if (!string.IsNullOrWhiteSpace(input))
            {
                task.Group = input.Trim();
                SaveTasks();
                BuildTaskList();
            }
        }
        void MarkTaskFinished(TaskEntry task, Border visual)
        {
            var theme = GetTheme();

            // Already in pending state?
            if (_pendingFinish.ContainsKey(task)) return;

            // Fill the circle with a checkmark
            DockPanel? dp = null;
            Border? circle = null;
            TextBlock? t = null;
            if (visual.Child is DockPanel dockP)
            {
                dp = dockP;
                foreach (var child in dockP.Children)
                {
                    if (child is Border b && b.CornerRadius.TopLeft == 10)
                    {
                        circle = b;
                        t = b.Child as TextBlock;
                        break;
                    }
                }
            }

            if (circle != null && t != null)
            {
                t.Text = "✓";
                t.Foreground = Brushes.Black;
                circle.Background = (Brush)new BrushConverter().ConvertFromString(theme.AccentColor)!;
            }

            // Add undo overlay
            var undoOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 30, 30, 30)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Cursor = Cursors.Hand,
                IsHitTestVisible = true
            };
            var undoText = new TextBlock
            {
                Text = "Undo (5s)",
                Foreground = Brushes.White,
                FontSize = 11
            };
            undoOverlay.Child = undoText;

            // Add undo button to row
            if (dp != null)
            {
                DockPanel.SetDock(undoOverlay, Dock.Right);
                dp.Children.Add(undoOverlay);
            }

            bool undone = false;

            // Countdown timer
            int remaining = 5;
            var countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            countdown.Tick += (_, _) =>
            {
                remaining--;
                if (remaining > 0)
                    undoText.Text = $"Undo ({remaining}s)";
                else
                {
                    countdown.Stop();
                    if (!undone)
                        DoFadeOut(task, visual, theme);
                }
            };
            countdown.Start();
            _pendingFinish[task] = countdown;

            undoOverlay.MouseLeftButtonUp += (_, e) =>
            {
                e.Handled = true;
                undone = true;
                countdown.Stop();
                _pendingFinish.Remove(task);
                // Restore visual
                if (circle != null && t != null)
                {
                    t.Text = "";
                    t.Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!;
                    circle.Background = Brushes.Transparent;
                }
                if (dp != null && dp.Children.Contains(undoOverlay))
                    dp.Children.Remove(undoOverlay);
            };
        }

        Dictionary<TaskEntry, DispatcherTimer> _pendingFinish = new();
        TaskEntry? _draggedTask = null;
        Border? _dragSourceContainer = null;

        void HighlightGroupDropZones(bool on)
        {
            var theme = GetTheme();
            var accentBrush = (Brush)new BrushConverter().ConvertFromString(theme.AccentColor)!;
            var normalBrush = (Brush)new BrushConverter().ConvertFromString(theme.HighlightBg)!;
            foreach (var child in contentPanel.Children)
            {
                if (child is Border b && b.Tag is string)
                {
                    b.BorderBrush = on ? accentBrush : (Brush)new BrushConverter().ConvertFromString(theme.Border)!;
                    b.BorderThickness = on ? new Thickness(2) : new Thickness(0);
                    b.Background = on ? new SolidColorBrush(Color.FromArgb(40,
                        ((SolidColorBrush)accentBrush).Color.R,
                        ((SolidColorBrush)accentBrush).Color.G,
                        ((SolidColorBrush)accentBrush).Color.B)) : normalBrush;
                }
            }
        }

        string? HitTestGroupHeader(Point posInContentPanel)
        {
            foreach (var child in contentPanel.Children)
            {
                if (child is Border b && b.Tag is string groupName)
                {
                    var topLeft = b.TranslatePoint(new Point(0, 0), contentPanel);
                    var rect = new Rect(topLeft, new Size(b.ActualWidth, b.ActualHeight));
                    if (rect.Contains(posInContentPanel))
                        return groupName;
                }
            }
            return null;
        }

        void DoFadeOut(TaskEntry task, Border visual, ThemeColors theme)
        {
            _pendingFinish.Remove(task);
            visual.IsHitTestVisible = false;

            var fadeOut = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromSeconds(1.0),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) =>
            {
                try
                {
                    Directory.CreateDirectory(dataDir);
                    File.AppendAllLines(
                        finishedTasksFile,
                        new[] { $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {task.Text}" },
                        Encoding.UTF8);

                    tasks.Remove(task);
                    SaveTasks();
                    BuildTaskList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving finished task: {ex.Message}");
                }
            };

            visual.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        // ---------------------------------------------------------------
        // Prompt helper (themed, no default styling)
        // ---------------------------------------------------------------
        string? PromptForText(string title, string message)
        {
            childWindowOpen = true;
            var theme = GetTheme();

            var win = new Window
            {
                Title = title,
                Width = 380,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                Owner = this,
                AllowsTransparency = true,
                Background = Brushes.Transparent
            };

            var outerBorder = new Border { Background = Brushes.Transparent, Padding = new Thickness(0) };
            var mainBorder = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(theme.Background)!,
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(theme.Border)!,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8)
            };

            var panel = new StackPanel { Margin = new Thickness(4) };

            // drag handle
            var drag = BuildDragHandle(win);
            panel.Children.Add(drag);

            panel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            });

            var tb = new TextBox
            {
                MinWidth = 320,
                Padding = new Thickness(4),
                Background = (Brush)new BrushConverter().ConvertFromString(theme.SearchBg)!,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                CaretBrush = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                BorderThickness = new Thickness(0)
            };
            panel.Children.Add(tb);

            // OK button — circular accent border like QuickSnips
            var btnGrid = new Grid { Height = 32, Margin = new Thickness(0, 8, 0, 0) };
            var okBtn = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = (Brush)new BrushConverter().ConvertFromString(theme.AccentColor)!,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            var okText = new TextBlock
            {
                Text = "✓",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            };
            okBtn.Child = okText;
            okBtn.MouseLeftButtonUp += (_, _) => win.DialogResult = true;
            btnGrid.Children.Add(okBtn);
            panel.Children.Add(btnGrid);

            mainBorder.Child = panel;
            outerBorder.Child = mainBorder;
            win.Content = outerBorder;

            win.KeyDown += (_, k) =>
            {
                if (k.Key == Key.Escape) { win.DialogResult = false; win.Close(); }
                else if (k.Key == Key.Enter) { win.DialogResult = true; }
            };

            var result = win.ShowDialog();
            childWindowOpen = false;
            return result == true ? tb.Text.Trim() : null;
        }

        // Shared helper: builds the 2-line drag icon for sub-windows
        static Border BuildDragHandle(Window owner)
        {
            var dragHandle = new Border
            {
                Height = 20,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Margin = new Thickness(0, 0, 0, 8)
            };
            dragHandle.MouseLeftButtonDown += (_, ev) =>
            {
                if (ev.ChangedButton == MouseButton.Left)
                    owner.DragMove();
            };

            var dragIcon = new Border
            {
                Width = 30,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dragLines = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            dragLines.Children.Add(new Border { Height = 2, Width = 20, Background = Brushes.DarkGray, Margin = new Thickness(0, 3, 0, 1) });
            dragLines.Children.Add(new Border { Height = 2, Width = 20, Background = Brushes.DarkGray, Margin = new Thickness(0, 1, 0, 3) });

            dragIcon.Child = dragLines;
            dragHandle.Child = dragIcon;
            return dragHandle;
        }

        // ---------------------------------------------------------------
        // Open editor (new or existing)
        // ---------------------------------------------------------------
        void OpenEditor(TaskEntry? editing)
        {
            childWindowOpen = true;
            var win = new EditorWindow(this, settings.Theme, editing);
            win.Owner = this;

            // Restore last position
            if (!double.IsNaN(settings.EditorLeft) && !double.IsNaN(settings.EditorTop))
            {
                win.WindowStartupLocation = WindowStartupLocation.Manual;
                win.Left = settings.EditorLeft;
                win.Top = settings.EditorTop;
            }

            win.LocationChanged += (_, _) =>
            {
                settings.EditorLeft = win.Left;
                settings.EditorTop = win.Top;
                SaveSettings();
            };

            win.Closed += (_, _) => { childWindowOpen = false; };
            if (win.ShowDialog() == true)
            {
                var text = win.ResultText?.Trim();
                if (string.IsNullOrWhiteSpace(text)) return;

                if (editing == null)
                {
                    var newTask = new TaskEntry { Text = text, Order = tasks.Count };
                    tasks.Add(newTask);
                }
                else
                {
                    editing.Text = text;
                }
                SaveTasks();
                BuildTaskList();
            }
        }

        // ---------------------------------------------------------------
        // Storage
        // ---------------------------------------------------------------
        void LoadSettings()
        {
            try
            {
                Directory.CreateDirectory(dataDir);
                var opts = new JsonSerializerOptions
                {
                    NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
                };
                settings = File.Exists(settingsFile)
                    ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsFile), opts) ?? new AppSettings()
                    : new AppSettings();
            }
            catch { settings = new AppSettings(); }
        }

        public void SaveSettings()
        {
            Directory.CreateDirectory(dataDir);
            var opts = new JsonSerializerOptions
            {
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals
            };
            File.WriteAllText(settingsFile, JsonSerializer.Serialize(settings, opts));
            ApplyTheme();
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            RegisterGlobalHotkey(hwnd);
        }

        void LoadTasks()
        {
            try
            {
                Directory.CreateDirectory(dataDir);
                tasks = File.Exists(openTasksFile)
                    ? JsonSerializer.Deserialize<List<TaskEntry>>(File.ReadAllText(openTasksFile)) ?? new List<TaskEntry>()
                    : new List<TaskEntry>();
                starCounter = tasks.Any() ? tasks.Max(t => t.StarOrder) : 0;
            }
            catch { tasks = new List<TaskEntry>(); }
        }

        void SaveTasks()
        {
            Directory.CreateDirectory(dataDir);
            var sb = new StringBuilder();
            sb.AppendLine("# QuickTasks — Open Tasks");
            sb.AppendLine($"# Saved: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            var grouped = tasks
                .Where(t => !string.IsNullOrWhiteSpace(t.Group))
                .GroupBy(t => t.Group!)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var g in grouped)
            {
                sb.AppendLine($"[{g.Key}]");
                var sorted = g.OrderByDescending(t => t.Starred).ThenBy(t => t.Order);
                foreach (var t in sorted)
                {
                    var star = t.Starred ? " ★" : "";
                    sb.AppendLine($"  - {t.Text}{star}");
                }
                sb.AppendLine();
            }

            var ungrouped = tasks
                .Where(t => string.IsNullOrWhiteSpace(t.Group))
                .OrderByDescending(t => t.Starred)
                .ThenBy(t => t.Order);

            bool hasUngrouped = ungrouped.Any();
            if (hasUngrouped)
            {
                sb.AppendLine("[Ungrouped]");
                foreach (var t in ungrouped)
                {
                    var star = t.Starred ? " ★" : "";
                    sb.AppendLine($"  - {t.Text}{star}");
                }
                sb.AppendLine();
            }

            // Also write JSON for internal use (machine-readable)
            File.WriteAllText(openTasksFile, JsonSerializer.Serialize(tasks, new JsonSerializerOptions { WriteIndented = true }));
            // Write human-readable version alongside
            File.WriteAllText(Path.ChangeExtension(openTasksFile, ".txt"), sb.ToString(), Encoding.UTF8);
        }

        public void ResetAllTasks()
        {
            var res = MessageBox.Show(
                "Are you sure? This will clear both open and finished tasks saved locally.",
                "Reset all tasks", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (res == MessageBoxResult.OK)
            {
                try
                {
                    tasks.Clear();
                    SaveTasks();
                    if (File.Exists(finishedTasksFile))
                        File.WriteAllText(finishedTasksFile, "");
                    BuildTaskList();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not reset tasks: {ex.Message}");
                }
            }
        }

        void OpenSettings()
        {
            childWindowOpen = true;
            var win = new SettingsWindow(this, settings);
            win.Owner = this;
            win.Closed += (_, _) => { childWindowOpen = false; };
            win.ShowDialog();
        }
    }

    // ===========================================================
    // Editor window — mirrors QuickSnips EntryEditor exactly:
    //   drag handle, themed textbox, resize grip, circular ✓ save
    // ===========================================================
    public class EditorWindow : Window
    {
        TextBox input = null!;
        public string? ResultText { get; private set; }
        bool hasBeenManuallyResized = false;
        Grid textBoxGrid = null!;
        Grid buttonContainer = null!;

        public EditorWindow(Window owner, string themeName, TaskEntry? editing)
        {
            Owner = owner;
            Title = editing == null ? "New Task" : "Edit Task";
            MinWidth = 300;
            MinHeight = 150;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            SizeToContent = SizeToContent.Manual;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            var themes = Themes.GetThemes();
            var theme = themes.ContainsKey(themeName) ? themes[themeName] : themes["Dark"];

            KeyDown += (_, k) =>
            {
                if (k.Key == Key.Escape) { DialogResult = false; Close(); }
            };

            var outerBorder = new Border { Background = Brushes.Transparent, Padding = new Thickness(0) };

            var mainBorder = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(theme.Background)!,
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(theme.Border)!,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8)
            };

            var mainPanel = new DockPanel();

            // Drag handle
            var dragHandle = new Border
            {
                Height = 20,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Margin = new Thickness(0, 0, 0, 8)
            };
            dragHandle.MouseLeftButtonDown += (_, ev) =>
            {
                if (ev.ChangedButton == MouseButton.Left) DragMove();
            };

            var dragIcon = new Border
            {
                Width = 30,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                Background = (Brush)new BrushConverter().ConvertFromString(theme.Background)!,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dragLines = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            dragLines.Children.Add(new Border { Height = 2, Width = 20, Background = Brushes.DarkGray, Margin = new Thickness(0, 3, 0, 1) });
            dragLines.Children.Add(new Border { Height = 2, Width = 20, Background = Brushes.DarkGray, Margin = new Thickness(0, 1, 0, 3) });
            dragIcon.Child = dragLines;
            dragHandle.Child = dragIcon;
            DockPanel.SetDock(dragHandle, Dock.Top);
            mainPanel.Children.Add(dragHandle);

            // Text box in a grid (for resize grip overlay)
            textBoxGrid = new Grid();

            var placeholderText = "ENTER: Save | SHIFT+ENTER: New line";

            input = new TextBox
            {
                Background = (Brush)new BrushConverter().ConvertFromString(theme.SearchBg)!,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 4, 20, 20),
                CaretBrush = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinWidth = 284,
                MinHeight = 50,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            if (editing != null)
            {
                input.Text = editing.Text;
            }
            else
            {
                input.Text = placeholderText;
                input.Foreground = Brushes.Gray;
            }

            input.GotFocus += (_, _) =>
            {
                if (input.Text == placeholderText)
                {
                    input.Text = "";
                    input.Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!;
                }
            };

            input.LostFocus += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(input.Text))
                {
                    input.Text = placeholderText;
                    input.Foreground = Brushes.Gray;
                }
            };

            input.TextChanged += (_, _) =>
            {
                if (!hasBeenManuallyResized && input.Text != placeholderText)
                    AutoResizeTextBox();
            };

            input.PreviewKeyDown += (_, ev) =>
            {
                if (ev.Key == Key.Enter)
                {
                    if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                        return; // allow newline
                    ev.Handled = true;
                    SaveAndClose();
                }
            };

            textBoxGrid.Children.Add(input);

            // Resize grip (bottom-right of textbox)
            var resizeGrip = new Border
            {
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Background = Brushes.Transparent,
                Margin = new Thickness(0, 0, 2, 2),
                Cursor = Cursors.SizeNWSE,
                IsHitTestVisible = true
            };

            var gripLines = new Grid();
            for (int i = 0; i < 3; i++)
            {
                gripLines.Children.Add(new Border
                {
                    Width = 10 - (i * 3),
                    Height = 2,
                    Background = Brushes.DarkGray,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, i * 4)
                });
            }
            resizeGrip.Child = gripLines;

            bool isResizing = false;
            Point startPoint = new Point();
            double startWidth = 0, startHeight = 0;

            resizeGrip.MouseLeftButtonDown += (_, ev) =>
            {
                isResizing = true;
                hasBeenManuallyResized = true;
                startPoint = ev.GetPosition(this);
                startWidth  = input.Width.Equals(double.NaN)  ? input.ActualWidth  : input.Width;
                startHeight = input.Height.Equals(double.NaN) ? input.ActualHeight : input.Height;
                resizeGrip.CaptureMouse();
                ev.Handled = true;
            };

            resizeGrip.MouseMove += (_, ev) =>
            {
                if (!isResizing) return;
                var current = ev.GetPosition(this);
                var dX = current.X - startPoint.X;
                var dY = current.Y - startPoint.Y;
                input.Width  = Math.Max(input.MinWidth,  startWidth  + dX);
                input.Height = Math.Max(input.MinHeight, startHeight + dY);
                UpdateWindowSize();
            };

            resizeGrip.MouseLeftButtonUp += (_, ev) =>
            {
                if (isResizing) { isResizing = false; resizeGrip.ReleaseMouseCapture(); }
            };

            textBoxGrid.Children.Add(resizeGrip);
            DockPanel.SetDock(textBoxGrid, Dock.Top);
            mainPanel.Children.Add(textBoxGrid);

            // Save button — circular accent border ✓ (mirrors QuickSnips)
            buttonContainer = new Grid { Height = 32, Margin = new Thickness(0, 4, 0, 0) };
            DockPanel.SetDock(buttonContainer, Dock.Bottom);

            var checkButton = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = (Brush)new BrushConverter().ConvertFromString(theme.AccentColor)!,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            var checkText = new TextBlock
            {
                Text = "✓",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            };
            checkButton.Child = checkText;
            checkButton.MouseLeftButtonUp += (_, _) => SaveAndClose();
            buttonContainer.Children.Add(checkButton);
            mainPanel.Children.Add(buttonContainer);

            mainBorder.Child = mainPanel;
            outerBorder.Child = mainBorder;
            AddChild(outerBorder);

            Loaded += (_, _) =>
            {
                input.Focus();
                if (editing != null && !hasBeenManuallyResized)
                    AutoResizeTextBox();
                UpdateWindowSize();
            };
        }

        void AutoResizeTextBox()
        {
            var ft = new System.Windows.Media.FormattedText(
                input.Text,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(input.FontFamily, input.FontStyle, input.FontWeight, input.FontStretch),
                input.FontSize,
                input.Foreground,
                new System.Windows.Media.NumberSubstitution(),
                System.Windows.Media.TextFormattingMode.Display,
                96);

            var reqW = Math.Max(input.MinWidth, ft.Width + input.Padding.Left + input.Padding.Right + 20);
            ft.MaxTextWidth = reqW - input.Padding.Left - input.Padding.Right;
            var reqH = Math.Max(input.MinHeight, ft.Height + input.Padding.Top + input.Padding.Bottom + 10);

            input.Width  = Math.Min(reqW, 600);
            input.Height = Math.Min(reqH, 400);
            UpdateWindowSize();
        }

        void UpdateWindowSize()
        {
            textBoxGrid.UpdateLayout();
            buttonContainer.UpdateLayout();

            var dragH   = 20 + 8;
            var tbH     = input.ActualHeight > 0 ? input.ActualHeight : input.Height;
            var btnH    = 32 + 4;
            var padding = 16;
            var border  = 4;

            var totalH = dragH + tbH + btnH + padding + border;
            var tbW    = input.ActualWidth  > 0 ? input.ActualWidth  : input.Width;
            var totalW = tbW + padding + border;

            Width  = Math.Max(MinWidth,  totalW);
            Height = Math.Max(MinHeight, totalH);
        }

        void SaveAndClose()
        {
            var placeholderText = "ENTER: Save | SHIFT+ENTER: New line";
            var text = input.Text;
            if (text == placeholderText || string.IsNullOrWhiteSpace(text)) { Close(); return; }
            ResultText = text;
            DialogResult = true;
            Close();
        }
    }

    // ===========================================================
    // Settings window — mirrors QuickSnips SettingsWindow:
    //   drag handle, themed ComboBox, hotkey badges, circular ✓
    // ===========================================================
    public class SettingsWindow : Window
    {
        ComboBox themeBox = null!;
        TextBlock globalHotkeyDisplay = null!;
        TextBlock newHotkeyDisplay = null!;
        AppSettings settings;
        MainWindow parent;

        public SettingsWindow(MainWindow p, AppSettings s)
        {
            parent = p;
            settings = s;
            Title = "Settings";
            Width = 320;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            var themes = Themes.GetThemes();
            var theme = themes.ContainsKey(s.Theme) ? themes[s.Theme] : themes["Dark"];

            KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };

            var outerBorder = new Border { Background = Brushes.Transparent, Padding = new Thickness(0) };

            var mainBorder = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(theme.Background)!,
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(theme.Border)!,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8)
            };

            var mainPanel = new StackPanel { Margin = new Thickness(8) };

            // Drag handle
            var dragHandle = new Border
            {
                Height = 20,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Margin = new Thickness(0, 0, 0, 8)
            };
            dragHandle.MouseLeftButtonDown += (_, ev) =>
            {
                if (ev.ChangedButton == MouseButton.Left) DragMove();
            };

            var dragIcon = new Border
            {
                Width = 30,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                Background = (Brush)new BrushConverter().ConvertFromString(theme.Background)!,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var dragLines = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            dragLines.Children.Add(new Border { Height = 2, Width = 20, Background = Brushes.DarkGray, Margin = new Thickness(0, 3, 0, 1) });
            dragLines.Children.Add(new Border { Height = 2, Width = 20, Background = Brushes.DarkGray, Margin = new Thickness(0, 1, 0, 3) });
            dragIcon.Child = dragLines;
            dragHandle.Child = dragIcon;
            mainPanel.Children.Add(dragHandle);

            // Global hotkey
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Global hotkey (show/hide):",
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                Margin = new Thickness(0, 0, 0, 4)
            });
            globalHotkeyDisplay = new TextBlock
            {
                Text = s.GlobalHotkey,
                Padding = new Thickness(6),
                Background = Brushes.Gray,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                Cursor = Cursors.Hand
            };
            globalHotkeyDisplay.MouseLeftButtonUp += (_, _) => CaptureHotkey(isGlobal: true);
            mainPanel.Children.Add(globalHotkeyDisplay);

            var resetGlobal = new Border
            {
                Background = Brushes.DimGray,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 4, 0, 10),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            resetGlobal.Child = new TextBlock { Text = "Reset Global Hotkey", Foreground = Brushes.White };
            resetGlobal.MouseLeftButtonUp += (_, _) =>
            {
                settings.GlobalHotkey = "Ctrl+Alt+,";
                globalHotkeyDisplay.Text = settings.GlobalHotkey;
                parent.SaveSettings();
            };
            mainPanel.Children.Add(resetGlobal);

            // New task hotkey
            mainPanel.Children.Add(new TextBlock
            {
                Text = "New task hotkey (when app active):",
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                Margin = new Thickness(0, 0, 0, 4)
            });
            newHotkeyDisplay = new TextBlock
            {
                Text = s.NewTaskHotkey,
                Padding = new Thickness(6),
                Background = Brushes.Gray,
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!,
                Cursor = Cursors.Hand
            };
            newHotkeyDisplay.MouseLeftButtonUp += (_, _) => CaptureHotkey(isGlobal: false);
            mainPanel.Children.Add(newHotkeyDisplay);

            var resetNew = new Border
            {
                Background = Brushes.DimGray,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 4, 0, 10),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            resetNew.Child = new TextBlock { Text = "Reset New Task Hotkey", Foreground = Brushes.White };
            resetNew.MouseLeftButtonUp += (_, _) =>
            {
                settings.NewTaskHotkey = "Ctrl+N";
                newHotkeyDisplay.Text = settings.NewTaskHotkey;
                parent.SaveSettings();
            };
            mainPanel.Children.Add(resetNew);

            // Theme picker
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Theme",
                Foreground = (Brush)new BrushConverter().ConvertFromString(theme.Foreground)!
            });
            themeBox = new ComboBox
            {
                ItemsSource = themes.Keys.OrderBy(k => k).ToList(),
                SelectedItem = s.Theme,
                Margin = new Thickness(0, 4, 0, 10)
            };
            mainPanel.Children.Add(themeBox);

            // Reset all tasks
            var resetAll = new Border
            {
                Background = Brushes.DimGray,
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 0, 12),
                CornerRadius = new CornerRadius(4),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            resetAll.Child = new TextBlock { Text = "Reset all tasks (clear open + finished)", Foreground = Brushes.White };
            resetAll.MouseLeftButtonUp += (_, _) => parent.ResetAllTasks();
            mainPanel.Children.Add(resetAll);

            // Save (✓) button — circular accent like QuickSnips
            var buttonContainer = new Grid { Height = 32, Margin = new Thickness(0, 4, 0, 4) };
            var checkButton = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = (Brush)new BrushConverter().ConvertFromString(theme.AccentColor)!,
                BorderThickness = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = Cursors.Hand
            };
            var checkText = new TextBlock
            {
                Text = "✓",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -2, 0, 0)
            };
            checkButton.Child = checkText;
            checkButton.MouseLeftButtonUp += (_, _) => SaveAndClose();
            buttonContainer.Children.Add(checkButton);
            mainPanel.Children.Add(buttonContainer);

            mainBorder.Child = mainPanel;
            outerBorder.Child = mainBorder;
            AddChild(outerBorder);
        }

        void CaptureHotkey(bool isGlobal)
        {
            var cap = new HotkeyCaptureWindow(settings.Theme);
            cap.Owner = this;
            if (cap.ShowDialog() == true && cap.CapturedHotkey != null)
            {
                if (isGlobal)
                {
                    settings.GlobalHotkey = cap.CapturedHotkey;
                    globalHotkeyDisplay.Text = settings.GlobalHotkey;
                }
                else
                {
                    settings.NewTaskHotkey = cap.CapturedHotkey;
                    newHotkeyDisplay.Text = settings.NewTaskHotkey;
                }
                parent.SaveSettings();
            }
        }

        void SaveAndClose()
        {
            settings.Theme = themeBox.SelectedValue?.ToString() ?? "Dark";
            parent.SaveSettings();
            Close();
        }
    }

    // ===========================================================
    // Hotkey capture window — mirrors QuickSnips exactly:
    //   drag handle with 2-line icon, themed text, no default chrome
    // ===========================================================
    public class HotkeyCaptureWindow : Window
    {
        public string? CapturedHotkey { get; private set; }
        TextBlock preview = null!;
        HashSet<Key> pressed = new();
        TextBlock instruction = null!;
        DispatcherTimer? cancelTimer;

        public HotkeyCaptureWindow(string themeName)
        {
            Title = "Capture Hotkey";
            Width = 280;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            var themes = Themes.GetThemes();
            var themeColors = themes.ContainsKey(themeName) ? themes[themeName] : themes["Dark"];

            var outerBorder = new Border { Background = Brushes.Transparent };

            var mainBorder = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(themeColors.Background)!,
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(themeColors.Border)!,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12)
            };

            var panel = new StackPanel();

            // Drag handle
            var dragHandle = new Border
            {
                Height = 20,
                Background = Brushes.Transparent,
                Cursor = Cursors.SizeAll,
                Margin = new Thickness(0, 0, 0, 8)
            };
            dragHandle.MouseLeftButtonDown += (_, ev) =>
            {
                if (ev.ChangedButton == MouseButton.Left) DragMove();
            };

            var dragIcon = new Border
            {
                Width = 30,
                Height = 12,
                CornerRadius = new CornerRadius(3),
                Background = (Brush)new BrushConverter().ConvertFromString(themeColors.Background)!,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            var dragLines = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            dragLines.Children.Add(new Border { Height = 2, Width = 20, Background = Brushes.DarkGray, Margin = new Thickness(0, 3, 0, 1) });
            dragLines.Children.Add(new Border { Height = 2, Width = 20, Background = Brushes.DarkGray, Margin = new Thickness(0, 1, 0, 3) });
            dragIcon.Child = dragLines;
            dragHandle.Child = dragIcon;
            panel.Children.Add(dragHandle);

            instruction = new TextBlock
            {
                Text = "Press key combination to set as hotkey.\nRelease to confirm.\nPress ESC to cancel.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)new BrushConverter().ConvertFromString(themeColors.Foreground)!,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
            panel.Children.Add(instruction);

            preview = new TextBlock
            {
                Text = "",
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)new BrushConverter().ConvertFromString(themeColors.Foreground)!,
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                TextAlignment = TextAlignment.Center,
                MinHeight = 30
            };
            panel.Children.Add(preview);

            mainBorder.Child = panel;
            outerBorder.Child = mainBorder;
            Content = outerBorder;

            PreviewKeyDown += HotkeyCaptureWindow_PreviewKeyDown;
            PreviewKeyUp += HotkeyCaptureWindow_PreviewKeyUp;
            Loaded += (_, _) => Focus();
        }

        void HotkeyCaptureWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.Key == Key.Escape) { CancelCapture(); return; }
            if (e.Key == Key.System) return;

            pressed.Add(e.Key);
            if (pressed.Count > 5) { CancelCapture(); return; }
            UpdatePreview();
        }

        void HotkeyCaptureWindow_PreviewKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (pressed.Count > 0 && pressed.Count <= 5)
            {
                CapturedHotkey = FormatHotkey();
                DialogResult = true;
                Close();
            }
        }

        void UpdatePreview() => preview.Text = FormatHotkey();

        string FormatHotkey()
        {
            var parts = new List<string>();
            if (pressed.Contains(Key.LeftCtrl)  || pressed.Contains(Key.RightCtrl))  parts.Add("Ctrl");
            if (pressed.Contains(Key.LeftAlt)   || pressed.Contains(Key.RightAlt))   parts.Add("Alt");
            if (pressed.Contains(Key.LeftShift) || pressed.Contains(Key.RightShift)) parts.Add("Shift");

            foreach (var k in pressed)
            {
                if (k == Key.LeftCtrl  || k == Key.RightCtrl  ||
                    k == Key.LeftAlt   || k == Key.RightAlt   ||
                    k == Key.LeftShift || k == Key.RightShift) continue;

                var ks = k.ToString();
                if (ks.StartsWith("D") && ks.Length == 2 && char.IsDigit(ks[1])) ks = ks.Substring(1);
                if (k == Key.OemComma)  ks = ",";
                if (k == Key.OemPeriod) ks = ".";
                parts.Add(ks);
            }
            return string.Join("+", parts);
        }

        void CancelCapture()
        {
            instruction.Text = "Cancelling...";
            preview.Text = "";
            pressed.Clear();
            cancelTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            cancelTimer.Tick += (_, _) =>
            {
                cancelTimer.Stop();
                DialogResult = false;
                Close();
            };
            cancelTimer.Start();
        }
    }
}