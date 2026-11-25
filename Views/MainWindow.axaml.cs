using System;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using Avalonia.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Webtoon_Downloader.Sites;
using Microsoft.Playwright;
using Webtoon_Downloader.Models;
using Webtoon_Downloader.Services;
using Webtoon_Downloader.ViewModels;

namespace Webtoon_Downloader.Views;

public partial class MainWindow : Window
{
    private readonly BrowserService _browserService = new(browserCount: 8);
    private readonly WebtoonExtractionService _extractionService;
    private bool _isOpening;
    private ChromiumScrollController? _webtoonListScroll;

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        await _browserService.DisposeAsync();
    }

    public MainWindow()
    {
        InitializeComponent();

        var webtoonList = this.FindControl<ListBox>("WebtoonList");

        if (webtoonList != null)
        {
            _webtoonListScroll = new ChromiumScrollController(webtoonList);
        }
        
        Closed += (_, _) =>
        {
            _webtoonListScroll?.Dispose();
        };

        _extractionService = new WebtoonExtractionService(
            _browserService
        );

        Opened += MainWindow_Opened;
    }

    private bool _isImporting;

    private async void ImportWebtoons_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_isImporting)
            return;

        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "가져올 웹툰 폴더 선택",
                AllowMultiple = true
            }
        );

        if (folders.Count == 0)
            return;

        string[] paths = folders
            .Select(folder => folder.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray();

        if (paths.Length == 0)
        {
            await ShowAlertAsync("로컬 폴더를 선택해야 합니다.");
            return;
        }

        _isImporting = true;

        try
        {
            var result = await WebtoonImportService.ImportAsync(
                paths,
                WebtoonPathService.WebtoonsDirectory
            );

            RefreshWebtoonList();

            string message = $"{result.Success}개 웹툰 가져오기 완료";

            if (result.Errors.Count > 0)
            {
                message +=
                    $"\n\n실패 {result.Errors.Count}개\n" +
                    string.Join("\n", result.Errors);
            }

            await ShowAlertAsync(message);
        }
        catch (Exception ex)
        {
            await ShowAlertAsync($"가져오기 실패: {ex.Message}");
        }
        finally
        {
            _isImporting = false;
        }
    }

    private async void Webtoon_DoubleTapped(
        object? sender,
        TappedEventArgs e)
    {
        if (sender is not Border border ||
            border.DataContext is not StoredWebtoon webtoon)
            return;

        if (!Directory.Exists(webtoon.Directory))
        {
            await ShowAlertAsync("웹툰 폴더를 찾을 수 없습니다.");
            RefreshWebtoonList();
            return;
        }

        bool viewerOpened = false;

        try
        {
            Hide();

            var selection = new RegionSelectionWindow();

            var completion = new TaskCompletionSource<PixelRect?>();
            
            selection.Closed += (_, _) =>
            {
                completion.TrySetResult(
                    selection.SelectedRegion
                );
            };
            
            selection.Show();
            selection.Activate();
            
            PixelRect? region = await completion.Task;

            if (region == null)
                return;

            var viewer = new StealthViewerWindow(
                webtoon,
                region.Value
            );

            viewer.ReselectRequested += async (_, _) =>
            {
                await ReselectViewerAsync(viewer);
            };

            viewer.Closed += (_, _) =>
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
                RefreshWebtoonList();
            };

            viewer.Show();
            viewer.Activate();

            viewerOpened = true;
        }
        catch (Exception ex)
        {
            await ShowAlertAsync(
                $"뷰어 실행 실패: {ex.Message}"
            );
        }
        finally
        {
            if (!viewerOpened)
            {
                Show();
                WindowState = WindowState.Normal;
                Activate();
            }
        }
    }

    private async Task ReselectViewerAsync(
        StealthViewerWindow viewer)
    {
        viewer.Opacity = 0;
    
        try
        {
            var selection = new RegionSelectionWindow();

            var completion = new TaskCompletionSource<PixelRect?>();

            selection.Closed += (_, _) =>
            {
                completion.TrySetResult(
                    selection.SelectedRegion
                );
            };

            selection.Show();
            selection.Activate();

            PixelRect? region = await completion.Task;
    
            if (region != null)
                viewer.SetRegion(region.Value);
        }
        finally
        {
            viewer.Opacity = 1;
            viewer.Activate();
        }
    }

    private async void MainWindow_Opened(
        object? sender,
        EventArgs e)
    {
        try
        {
            WebtoonPathService.Initialize();

            RefreshWebtoonList();
        }
        catch (Exception ex)
        {
            await ShowAlertAsync(
                $"웹툰 경로 초기화 실패: {ex.Message}"
            );
        }
    }

    private void RefreshWebtoonList()
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.WebtoonTitles.Clear();

        foreach (var webtoon in WebtoonLibraryService.GetWebtoons())
            vm.WebtoonTitles.Add(webtoon);
    }

    private async void DeleteWebtoon_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button ||
            button.DataContext is not StoredWebtoon webtoon)
            return;
    
        var dialog = new Window
        {
            Title = "웹툰 삭제",
            Width = 350,
            Height = 160,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
    
        var yes = new Button { Content = "Yes", MinWidth = 70 };
        var no = new Button { Content = "No", MinWidth = 70, IsDefault = true };
    
        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
    
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = $"{webtoon.Title}을(를) 삭제하시겠습니까?",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { no, yes }
                }
            }
        };
    
        bool confirmed = await dialog.ShowDialog<bool>(this);
    
        if (!confirmed)
            return;
    
        try
        {
            WebtoonLibraryService.DeleteWebtoon(webtoon);
            RefreshWebtoonList();
        }
        catch (Exception ex)
        {
            await ShowAlertAsync($"웹툰 삭제 실패: {ex.Message}");
        }
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

        if (!Uri.TryCreate(
            input,
            UriKind.Absolute,
            out var url
        ) || (url.Scheme != Uri.UriSchemeHttps
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
        bool showProgress = true;

        try
        {
            if (DataContext is MainWindowViewModel startVm)
            {
                startVm.IsBusy = true;
                startVm.IsNotBusy = false;
                startVm.Progress = 0;
                startVm.StatusText = "웹툰 정보 수집 중...";
            }

            using var downloader = new WebtoonDownloader();

            WebtoonBatchResult result =
                await _extractionService.ExtractAndDownloadAsync(
                    site,
                    url,
                    downloader,
                    progress =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            if (!showProgress)
                                return;

                            if (DataContext is MainWindowViewModel vm)
                            {
                                vm.Progress =
                                    (progress.Completed + progress.Failed)
                                    * 100.0 / progress.Total;

                                vm.StatusText =
                                    $"전체 {progress.Total}화 | " +
                                    $"다운로드 완료 {progress.Completed}화 | " +
                                    $"실패 {progress.Failed}화";
                            }
                        });
                    }
                );

            showProgress = false;

            if (DataContext is MainWindowViewModel resultVm)
            {
                resultVm.Progress = 100;

                resultVm.StatusText =
                    $"작업 종료 | 성공 {result.Completed}화 | " +
                    $"실패 {result.Failed}화";
            }

            if (result.Failed == 0)
            {
                RefreshWebtoonList();

                if (DataContext is MainWindowViewModel vm)
                {
                    vm.StatusText =
                        $"{result.Info.Title} - 전체 다운로드 완료";

                    vm.WebtoonUrl = "";
                }
            }
            else
            {
                string errors = string.Join(
                    "\n",
                    result.Failures
                        .Take(5)
                        .Select(x => $"{x.Number}화: {x.Error}")
                );

                await ShowAlertAsync(
                    $"다운로드 실패: {result.Failed}개 회차\n\n{errors}"
                );
            }
        }
        catch (Exception ex)
        {
            showProgress = false;

            if (DataContext is MainWindowViewModel errorVm)
                errorVm.StatusText = "다운로드 실패";

            await ShowAlertAsync(
                $"작업 중 오류가 발생했습니다.\n{ex.Message}"
            );
        }
        finally
        {
            showProgress = false;
            _isOpening = false;

            if (DataContext is MainWindowViewModel vm)
            {
                vm.IsBusy = false;
                vm.IsNotBusy = true;
            }
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