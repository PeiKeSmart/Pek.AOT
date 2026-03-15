using System.Collections.ObjectModel;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Pek.Log;
using Pek.Log.Avalonia;
using Pek.Threading;

namespace AvaloniaLogSample;

public class MainWindow : Window
{
    private readonly ObservableCollection<String> _items = [];
    private readonly AvaloniaCollectionLog _avaloniaLog;
    private readonly Button _toggleTimerButton;
    private TimerX? _timer;
    private Int32 _messageCount;

    public MainWindow()
    {
        Title = "Pek.Log Avalonia Sample";
        Width = 960;
        Height = 640;
        MinWidth = 720;
        MinHeight = 480;

        _avaloniaLog = new AvaloniaCollectionLog(_items, new AvaloniaLogOptions { MaxItems = 300 });
        _toggleTimerButton = new Button
        {
            Content = "启动后台日志",
            MinWidth = 132,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        _toggleTimerButton.Click += ToggleTimer;
        Closed += OnClosed;

        XTrace.UseConsole();
        XTraceAvaloniaExtensions.UseAvalonia(_avaloniaLog);
        Content = BuildContent();

        XTrace.WriteLine("Avalonia 日志示例已启动");
    }

    private Control BuildContent()
    {
        var title = new TextBlock
        {
            Text = "Pek.Log Avalonia",
            FontSize = 28,
            FontWeight = FontWeight.SemiBold
        };

        var description = new TextBlock
        {
            Text = "按钮会写入普通日志、异常日志以及后台定时日志，列表通过 AvaloniaCollectionLog 自动刷新。",
            TextWrapping = TextWrapping.Wrap,
            Foreground = Brushes.DimGray
        };

        var info = new Border
        {
            Padding = new Thickness(16),
            CornerRadius = new CornerRadius(12),
            Background = new SolidColorBrush(Color.Parse("#FFF5F1E8")),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    title,
                    description
                }
            }
        };

        var writeButton = CreateButton("写入日志", (_, _) => WriteMessage());
        var exceptionButton = CreateButton("写入异常", (_, _) => WriteException());
        var clearButton = CreateButton("清空列表", (_, _) => _avaloniaLog.Buffer.Clear());

        var buttons = new WrapPanel
        {
            Margin = new Thickness(0, 16, 0, 16),
            Orientation = Orientation.Horizontal,
            ItemHeight = Double.NaN,
            ItemWidth = Double.NaN
        };
        buttons.Children.Add(writeButton);
        buttons.Children.Add(exceptionButton);
        buttons.Children.Add(_toggleTimerButton);
        buttons.Children.Add(clearButton);

        var listBox = new ListBox
        {
            ItemsSource = _items
        };

        return new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(24),
            Children =
            {
                new StackPanel
                {
                    Spacing = 0,
                    Children =
                    {
                        info,
                        buttons
                    }
                },
                new Border
                {
                    [Grid.RowProperty] = 1,
                    Padding = new Thickness(12),
                    CornerRadius = new CornerRadius(12),
                    Background = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#FFE5DED1")),
                    BorderThickness = new Thickness(1),
                    Child = listBox
                }
            }
        };
    }

    private Button CreateButton(String text, EventHandler<Avalonia.Interactivity.RoutedEventArgs> handler)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0, 0, 12, 12),
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        button.Click += handler;
        return button;
    }

    private void WriteMessage()
    {
        var count = Interlocked.Increment(ref _messageCount);
        XTrace.WriteLine("界面日志消息 #{0}，Time={1:HH:mm:ss.fff}", count, DateTime.Now);
    }

    private void WriteException()
    {
        try
        {
            throw new InvalidOperationException("Avalonia 示例故意抛出的测试异常");
        }
        catch (Exception ex)
        {
            XTrace.WriteException(ex);
        }
    }

    private void ToggleTimer(Object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_timer != null)
        {
            _timer.Dispose();
            _timer = null;
            _toggleTimerButton.Content = "启动后台日志";
            XTrace.WriteLine("后台日志已停止");
            return;
        }

        _timer = new TimerX(_ =>
        {
            var count = Interlocked.Increment(ref _messageCount);
            XTrace.WriteScope("Samples.Avalonia", "Timer", "后台日志 Tick={0} Thread={1}", count, Environment.CurrentManagedThreadId);
        }, "Avalonia", 500, 1500, "AvaloniaSample");

        _toggleTimerButton.Content = "停止后台日志";
        XTrace.WriteLine("后台日志已启动");
    }

    private void OnClosed(Object? sender, EventArgs e)
    {
        _timer?.Dispose();
        _timer = null;
        XTrace.Shutdown();
    }
}