using System;

namespace Webtoon_Downloader.Services;

public sealed class ChromiumScrollEngine
{
    public double CurrentY { get; private set; }
    public double TargetY { get; private set; }
    public double MaxScrollY { get; set; }
    public double EaseFactor { get; set; } = 0.12;
    public bool IsAnimating { get; private set; }

    public event Action<double>? OnScrollUpdated;

    public ChromiumScrollEngine(
        double maxScrollY,
        double initialY = 0)
    {
        MaxScrollY = Math.Max(0, maxScrollY);
        CurrentY = Math.Clamp(initialY, 0, MaxScrollY);
        TargetY = CurrentY;
    }

    public void OnWheelInput(double deltaY)
    {
        TargetY = Math.Clamp(
            TargetY + deltaY,
            0,
            MaxScrollY
        );

        IsAnimating = Math.Abs(TargetY - CurrentY) >= 0.5;
    }

    public void UpdateFrame(double deltaTime)
    {
        if (!IsAnimating)
            return;

        CurrentY = Math.Clamp(CurrentY, 0, MaxScrollY);
        TargetY = Math.Clamp(TargetY, 0, MaxScrollY);

        double diff = TargetY - CurrentY;

        if (Math.Abs(diff) < 0.5)
        {
            CurrentY = TargetY;
            IsAnimating = false;
            OnScrollUpdated?.Invoke(CurrentY);
            return;
        }

        double frameAdjustedEase =
            1.0 - Math.Pow(
                1.0 - EaseFactor,
                deltaTime * 60.0
            );

        CurrentY += diff * frameAdjustedEase;

        OnScrollUpdated?.Invoke(CurrentY);
    }

    public void Reset(double position, double maxScrollY)
    {
        MaxScrollY = Math.Max(0, maxScrollY);

        CurrentY = Math.Clamp(
            position,
            0,
            MaxScrollY
        );

        TargetY = CurrentY;
        IsAnimating = false;
    }
}