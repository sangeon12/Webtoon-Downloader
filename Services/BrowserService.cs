using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Webtoon_Downloader.Services;

public sealed class BrowserService : IAsyncDisposable
{
    private readonly int _browserCount;
    private readonly List<IBrowser> _browsers = new();
    private readonly List<SemaphoreSlim> _browserLocks = new();
    private readonly SemaphoreSlim _initializeLock = new(1, 1);

    private IPlaywright? _playwright;
    private int _nextBrowser = -1;

    public BrowserService(int browserCount = 8)
    {
        if (browserCount < 1)
            throw new ArgumentOutOfRangeException(nameof(browserCount));

        _browserCount = browserCount;
    }

    public async Task PrepareAsync(int count)
    {
        if (count < 1 || count > _browserCount)
            throw new ArgumentOutOfRangeException(nameof(count));

        await _initializeLock.WaitAsync();

        try
        {
            _playwright ??= await Playwright.CreateAsync();

            while (_browsers.Count < count)
            {
                var browser = await _playwright.Chromium.LaunchAsync(
                    new BrowserTypeLaunchOptions
                    {
                        Headless = true
                    }
                );

                _browsers.Add(browser);
                _browserLocks.Add(new SemaphoreSlim(1, 1));
            }
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    public async Task<T> RunPageAsync<T>(
        Uri url,
        Func<IPage, Task<T>> operation)
    {
        if (_browsers.Count == 0)
            await PrepareAsync(1);

        int index = (int)(
            (uint)Interlocked.Increment(ref _nextBrowser)
            % (uint)_browsers.Count
        );

        var browserLock = _browserLocks[index];

        await browserLock.WaitAsync();

        try
        {
            var context = await _browsers[index].NewContextAsync();

            try
            {
                var page = await context.NewPageAsync();

                await page.GotoAsync(
                    url.ToString(),
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.Commit
                    }
                );

                return await operation(page);
            }
            finally
            {
                await context.CloseAsync();
            }
        }
        finally
        {
            browserLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _initializeLock.WaitAsync();

        try
        {
            foreach (var browser in _browsers)
                await browser.CloseAsync();

            _browsers.Clear();

            _playwright?.Dispose();
            _playwright = null;
        }
        finally
        {
            _initializeLock.Release();
        }
    }
}