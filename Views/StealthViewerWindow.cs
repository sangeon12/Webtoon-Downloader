using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Styling;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Webtoon_Downloader.Models;
using Webtoon_Downloader.Services;

namespace Webtoon_Downloader.Views;

public sealed class StealthViewerWindow : Window
{
    private readonly IReadOnlyList<string> _episodes;
    private readonly ContentControl _content = new();
    private readonly Border _panel;
    private readonly List<Bitmap> _bitmaps = new();

    private ScrollViewer? _scrollViewer;
    private StackPanel? _imageStack;

    private int _currentEpisode = -1;
    private bool _showList = true;
    private double _opacity = 1;

    public event EventHandler? ReselectRequested;

    private ChromiumScrollController _viewerScroll = null!;

    private bool _isEpisodeLoading;
    private bool _viewerClosed;

    public StealthViewerWindow(
        StoredWebtoon webtoon,
        PixelRect region)
    {
        _episodes = WebtoonLibraryService.GetEpisodes(webtoon);

        Title = webtoon.Title;
        WindowDecorations = WindowDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Background = Brushes.Transparent;

        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Transparent
        ];

        _panel = new Border
        {
            Background = Brushes.Black,
            Child = _content
        };

        var root = new Grid
        {
            Background = Brushes.Transparent
        };

        root.Children.Add(_panel);
        Content = root;

        SetRegion(region);

        AddHandler(
            InputElement.PointerPressedEvent,
            HandlePointer,
            RoutingStrategies.Tunnel
        );

        AddHandler(
            InputElement.PointerWheelChangedEvent,
            HandleWheel,
            RoutingStrategies.Tunnel
        );

        KeyDown += HandleKeyDown;

        ShowEpisodeList();

        Opened += (_, _) => Activate();
        Closed += (_, _) =>
        {
            _viewerClosed = true;

            _viewerScroll.Dispose();

            ClearImages();
        };

        _viewerScroll = new ChromiumScrollController(_content);
    }

    public void SetRegion(PixelRect region)
    {
        double scaling = Screens.Primary?.Scaling ?? 1;

        Position = region.Position;
        Width = Math.Max(100, region.Width / scaling);
        Height = Math.Max(100, region.Height / scaling);

        if (_imageStack != null)
        {
            foreach (var image in _imageStack.Children.OfType<Image>())
                image.Width = Width;
        }
    }

    private void ShowEpisodeList()
    {
        if (_isEpisodeLoading)
            return;

        ClearImages();

        _showList = true;
        _scrollViewer = null;
        _imageStack = null;

        var list = new ListBox
        {
            ItemsSource = Enumerable.Range(1, _episodes.Count)
                .Select(i => $"{i}화")
                .ToArray(),
            SelectedIndex = _currentEpisode,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        list.Styles.Add(new Style(x =>
            x.OfType<ListBoxItem>()
                .Class(":selected")
                .Template()
                .OfType<ContentPresenter>())
        {
            Setters =
            {
                new Setter(
                    ContentPresenter.BackgroundProperty,
                    Brush.Parse("#707070")
                )
            }
        });

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedIndex >= 0)
                OpenEpisode(list.SelectedIndex);
        };

        _content.Content = list;
        _panel.IsVisible = true;
    }

    private async void OpenEpisode(int index)
    {
        if (_isEpisodeLoading)
            return;

        if (index < 0 || index >= _episodes.Count)
            return;

        _isEpisodeLoading = true;

        try
        {
            _viewerScroll.Reset();

            _content.Content = null;
            ClearImages();

            _currentEpisode = index;
            _showList = false;

            string directory = _episodes[index];

            var stack = new StackPanel
            {
                Spacing = 0,
                Background = Brushes.Black
            };

            _scrollViewer = new ScrollViewer
            {
                Content = stack
            };

            _content.Content = _scrollViewer;

            foreach (string path in WebtoonLibraryService.GetImages(directory))
            {
                if (_viewerClosed)
                    return;

                Bitmap bitmap = await Task.Run(
                    () => new Bitmap(path)
                );

                if (_viewerClosed)
                {
                    bitmap.Dispose();
                    return;
                }

                _bitmaps.Add(bitmap);

                stack.Children.Add(new Image
                {
                    Source = bitmap,
                    Width = Width,
                    Stretch = Stretch.Uniform,
                    UseLayoutRounding = true,
                    Margin = new Thickness(0, 0, 0, -1)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"이미지 로딩 실패: {ex}"
            );
        }
        finally
        {
            _isEpisodeLoading = false;
        }
    }

    private void ClearImages()
    {
        _content.Content = null;

        foreach (Bitmap bitmap in _bitmaps)
            bitmap.Dispose();

        _bitmaps.Clear();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.S)
        {
            ShowEpisodeList();
            e.Handled = true;
            return;
        }

        if (_showList)
            return;

        if (e.Key == Key.A)
        {
            OpenEpisode(_currentEpisode - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.D)
        {
            OpenEpisode(_currentEpisode + 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            ScrollImages(-250);
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            ScrollImages(250);
            e.Handled = true;
        }
        else if (e.Key == Key.Left)
        {
            _opacity = Math.Clamp(
                Math.Round(_opacity * 10) / 10 - 0.1,
                0.1,
                1.0
            );

            _panel.Opacity = _opacity;
            e.Handled = true;
        }
        else if (e.Key == Key.Right)
        {
            _opacity = Math.Clamp(
                Math.Round(_opacity * 10) / 10 + 0.1,
                0.1,
                1.0
            );

            _panel.Opacity = _opacity;
            e.Handled = true;
        }
        else if(e.Key == Key.Space)
        {
            _panel.IsVisible = !_panel.IsVisible;
            e.Handled = true;
        }
    }

    private void ScrollImages(double amount)
    {
        if (_scrollViewer == null)
            return;
    
        _viewerScroll.Reset();
    
        double maximum = Math.Max(
            0,
            _scrollViewer.Extent.Height -
            _scrollViewer.Viewport.Height
        );
    
        double next = Math.Clamp(
            _scrollViewer.Offset.Y + amount,
            0,
            maximum
        );
    
        _scrollViewer.Offset = new Vector(
            _scrollViewer.Offset.X,
            next
        );
    
        _viewerScroll.ResetToCurrent();
    }

    private void HandleWheel(
        object? sender,
        PointerWheelEventArgs e)
    {
        if (_showList)
            return;

        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        _opacity = Math.Clamp(
            _opacity + e.Delta.Y * 0.05,
            0.1,
            1.0
        );

        _panel.Opacity = _opacity;
        e.Handled = true;
    }

    private void HandlePointer(
        object? sender,
        PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(this).Properties;

        if (properties.IsRightButtonPressed)
        {
            ReselectRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        if (_showList)
            return;

        if (properties.IsLeftButtonPressed)
        {
            _panel.IsVisible = !_panel.IsVisible;
            e.Handled = true;
        }
    }
}