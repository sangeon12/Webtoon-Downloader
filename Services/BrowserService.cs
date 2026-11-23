using System;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Webtoon_Downloader.Services;

public sealed class BrowserService : IAsyncDisposable
{
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IBrowserContext? _context;

    public IPage? Page { get; private set; }

    public async Task<IPage> OpenAsync(Uri url)
    {
        await CloseAsync();

        try
        {
            _playwright = await Playwright.CreateAsync();

            _browser = await _playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = false
                }
            );

            _context = await _browser.NewContextAsync();

            Page = await _context.NewPageAsync();

            await Page.GotoAsync(
                url.ToString(),
                new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded
                }
            );

            return Page;
        }
        catch
        {
            await CloseAsync();
            throw;
        }
    }

    public async Task CloseAsync()
    {
        Page = null;

        if (_context != null)
        {
            await _context.CloseAsync();
            _context = null;
        }

        if (_browser != null)
        {
            await _browser.CloseAsync();
            _browser = null;
        }

        _playwright?.Dispose();
        _playwright = null;
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
    }
}