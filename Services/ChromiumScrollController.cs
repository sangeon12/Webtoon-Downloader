using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Webtoon_Downloader.Services;

public sealed class ChromiumScrollController : IDisposable
{
    private const double WheelScrollDistance = 120;

    private readonly Control _root;
    private readonly ChromiumScrollEngine _engine;
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private ScrollViewer? _activeViewer;
    private bool _disposed;

    public ChromiumScrollController(Control root)
    {
        _root = root;

        _engine = new ChromiumScrollEngine(0)
        {
            EaseFactor = 0.12
        };

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };

        _engine.OnScrollUpdated += UpdateScroll;
        _timer.Tick += UpdateFrame;

        _root.AddHandler(
            InputElement.PointerWheelChangedEvent,
            HandleWheel,
            RoutingStrategies.Tunnel
        );
    }

    private ScrollViewer? FindScrollViewer()
    {
        if (_root is ScrollViewer viewer)
            return viewer;

        return _root
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault();
    }

    private void HandleWheel(
        object? sender,
        PointerWheelEventArgs e)
    {
        if (_disposed)
            return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        if (e.Delta.Y == 0)
            return;

        ScrollViewer? viewer = FindScrollViewer();

        if (viewer == null)
            return;

        e.Handled = true;

        double maximum = Math.Max(
            0,
            viewer.Extent.Height - viewer.Viewport.Height
        );

        if (!ReferenceEquals(_activeViewer, viewer))
        {
            StopAnimation();

            _activeViewer = viewer;

            _engine.Reset(
                viewer.Offset.Y,
                maximum
            );
        }
        else
        {
            _engine.MaxScrollY = maximum;

            if (!_engine.IsAnimating)
            {
                _engine.Reset(
                    viewer.Offset.Y,
                    maximum
                );
            }
        }

        _engine.OnWheelInput(
            -e.Delta.Y * WheelScrollDistance
        );

        if (_engine.IsAnimating && !_timer.IsEnabled)
        {
            _clock.Restart();
            _timer.Start();
        }
    }

    private void UpdateFrame(object? sender, EventArgs e)
    {
        if (_disposed || _activeViewer == null)
        {
            StopAnimation();
            return;
        }

        double deltaTime = Math.Min(
            _clock.Elapsed.TotalSeconds,
            0.05
        );

        _clock.Restart();

        _engine.MaxScrollY = Math.Max(
            0,
            _activeViewer.Extent.Height -
            _activeViewer.Viewport.Height
        );

        _engine.UpdateFrame(deltaTime);

        if (!_engine.IsAnimating)
            StopAnimation();
    }

    private void UpdateScroll(double position)
    {
        if (_disposed || _activeViewer == null)
            return;

        _activeViewer.Offset = new Vector(
            _activeViewer.Offset.X,
            position
        );
    }

    public void ResetToCurrent()
    {
        StopAnimation();

        ScrollViewer? viewer = FindScrollViewer();

        _activeViewer = viewer;

        if (viewer == null)
        {
            _engine.Reset(0, 0);
            return;
        }

        double maximum = Math.Max(
            0,
            viewer.Extent.Height - viewer.Viewport.Height
        );

        _engine.Reset(viewer.Offset.Y, maximum);
    }

    public void Reset()
    {
        StopAnimation();

        _activeViewer = null;
        _engine.Reset(0, 0);
    }

    private void StopAnimation()
    {
        _timer.Stop();
        _clock.Stop();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        StopAnimation();

        _root.RemoveHandler(
            InputElement.PointerWheelChangedEvent,
            HandleWheel
        );

        _engine.OnScrollUpdated -= UpdateScroll;
        _timer.Tick -= UpdateFrame;

        _activeViewer = null;
    }
}