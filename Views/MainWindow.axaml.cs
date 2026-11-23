using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Webtoon_Downloader.Sites;
using Microsoft.Playwright;
using Webtoon_Downloader.Services;

namespace Webtoon_Downloader.Views;

public partial class MainWindow : Window
{
    private readonly BrowserService _browserService = new();
    private bool _isOpening;

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        await _browserService.DisposeAsync();
    }

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void ConfirmButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await SelectAlgorithmAsync();
    }

    private async void UrlInput_KeyDown(
        object? sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;

        await SelectAlgorithmAsync();
    }

    private async Task SelectAlgorithmAsync()
    {
        if (_isOpening)
            return;

        string input = UrlInput.Text?.Trim() ?? "";

        if (!Uri.TryCreate(input, UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttps
                && url.Scheme != Uri.UriSchemeHttp))
        {
            await ShowAlertAsync("올바른 URL을 입력해주세요.");
            return;
        }

        bool selected = SiteResolver.TryResolve(url, out var site);

        if (!selected || site == null)
        {
            await ShowAlertAsync("알고리즘 선택에 실패했습니다.");
            return;
        }

        _isOpening = true;

        try
        {
            Uri targetUrl = site.NormalizeUrl(url);

            var page = await _browserService.OpenAsync(targetUrl);
            //var info = await site.ExtractAsync(page, url);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync($"브라우저 실행 오류: {ex.Message}");
        }
        finally
        {
            _isOpening = false;
        }
    }

    private async Task ShowAlertAsync(string message)
    {
        var dialog = new Window
        {
            Title = "알림",
            Width = 350,
            Height = 150,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var button = new Button
        {
            Content = "확인",
            HorizontalAlignment = HorizontalAlignment.Right,
            IsDefault = true
        };

        button.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 15,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                button
            }
        };

        await dialog.ShowDialog(this);
    }
}