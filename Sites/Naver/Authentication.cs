using Microsoft.Playwright;

namespace Webtoon_Downloader.Sites.Naver;

public static class NaverAuthentication
{
    public static async Task<string> RunAsync(
        IWebtoonSite site,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );

        timeout.CancelAfter(TimeSpan.FromMinutes(2));

        try
        {
            using var playwright = await Playwright.CreateAsync()
                .WaitAsync(timeout.Token);

            await using var browser = await playwright.Chromium.LaunchAsync(
                new BrowserTypeLaunchOptions
                {
                    Headless = false
                }
            ).WaitAsync(timeout.Token);

            await using var context = await browser.NewContextAsync()
                .WaitAsync(timeout.Token);

            var page = await context.NewPageAsync()
                .WaitAsync(timeout.Token);

            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            page.FrameNavigated += (_, frame) =>
            {
                if (frame != page.MainFrame)
                    return;

                if (!Uri.TryCreate(
                    frame.Url,
                    UriKind.Absolute,
                    out var uri))
                    return;

                if (uri.Host.Equals(
                    "www.naver.com",
                    StringComparison.OrdinalIgnoreCase
                ) && uri.AbsolutePath == "/")
                {
                    completion.TrySetResult(true);
                }
            };

            page.Close += (_, _) =>
            {
                completion.TrySetException(
                    new InvalidOperationException(
                        "인증 브라우저가 종료되었습니다."
                    )
                );
            };

            browser.Disconnected += (_, _) =>
            {
                completion.TrySetException(
                    new InvalidOperationException(
                        "인증 브라우저가 종료되었습니다."
                    )
                );
            };

            try
            {
                await page.GotoAsync(
                    "https://nid.naver.com/nidlogin.login",
                    new PageGotoOptions
                    {
                        WaitUntil = WaitUntilState.Commit
                    }
                ).WaitAsync(timeout.Token);
            }
            catch (PlaywrightException)
                when (!page.IsClosed &&
                      browser.IsConnected &&
                      !timeout.IsCancellationRequested)
            {
            }

            await completion.Task.WaitAsync(timeout.Token);

            string authState = await context.StorageStateAsync(
                new BrowserContextStorageStateOptions
                {
                    IndexedDB = true
                }
            ).WaitAsync(timeout.Token);

            return authState;
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested &&
                  timeout.IsCancellationRequested)
        {
            throw new TimeoutException(
                "로그인 인증 시간이 초과되었습니다."
            );
        }
    }
}