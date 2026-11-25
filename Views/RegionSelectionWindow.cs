using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Webtoon_Downloader.Views;

public sealed class RegionSelectionWindow : Window
{
    private readonly Canvas _canvas = new();
    private readonly Border _selection = new();

    private Point? _start;
    private double _scaling = 1;

    public PixelRect? SelectedRegion { get; private set; }

    public RegionSelectionWindow()
    {
        Title = "영역 선택";
        WindowDecorations = WindowDecorations.None;
        ShowInTaskbar = false;
        Topmost = true;
        CanResize = false;
        Background = new SolidColorBrush(
            Color.FromArgb(45, 0, 0, 0)
        );

        TransparencyLevelHint =
        [
            WindowTransparencyLevel.Transparent
        ];

        _selection.BorderBrush = Brushes.White;
        _selection.BorderThickness = new Thickness(2);
        _selection.Background = new SolidColorBrush(
            Color.FromArgb(50, 100, 160, 255)
        );
        _selection.IsVisible = false;

        _canvas.Background = Brushes.Transparent;
        _canvas.Children.Add(_selection);

        Content = _canvas;

        Opened += (_, _) =>
        {
            var screen = Screens.Primary;

            if (screen == null)
            {
                Close();
                return;
            }

            _scaling = screen.Scaling;

            Position = screen.Bounds.Position;
            Width = screen.Bounds.Width / _scaling;
            Height = screen.Bounds.Height / _scaling;

            Activate();
        };

        _canvas.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(_canvas)
                .Properties.IsLeftButtonPressed)
                return;

            _start = e.GetPosition(_canvas);
            _selection.IsVisible = true;
            e.Pointer.Capture(_canvas);
        };

        _canvas.PointerMoved += (_, e) =>
        {
            if (_start == null)
                return;

            Point end = e.GetPosition(_canvas);
            UpdateSelection(_start.Value, end);
        };

        _canvas.PointerReleased += (_, e) =>
        {
            if (_start == null)
                return;

            Point end = e.GetPosition(_canvas);
            Point start = _start.Value;

            _start = null;
            e.Pointer.Capture(null);

            double left = Math.Min(start.X, end.X);
            double top = Math.Min(start.Y, end.Y);
            double width = Math.Abs(end.X - start.X);
            double height = Math.Abs(end.Y - start.Y);

            if (width < 50 || height < 50)
            {
                _selection.IsVisible = false;
                return;
            }

            var bounds = new PixelRect(
                Position.X + (int)Math.Round(left * _scaling),
                Position.Y + (int)Math.Round(top * _scaling),
                (int)Math.Round(width * _scaling),
                (int)Math.Round(height * _scaling)
            );

            SelectedRegion = bounds;
            Close();
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void UpdateSelection(Point start, Point end)
    {
        double left = Math.Min(start.X, end.X);
        double top = Math.Min(start.Y, end.Y);

        double width = Math.Abs(end.X - start.X);
        double height = Math.Abs(end.Y - start.Y);

        Canvas.SetLeft(_selection, left);
        Canvas.SetTop(_selection, top);

        _selection.Width = width;
        _selection.Height = height;
    }
}