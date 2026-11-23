using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Webtoon_Downloader.Sites;

namespace Webtoon_Downloader.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string webtoonUrl = "";

    [ObservableProperty]
    private string statusText = "";

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private bool isNotBusy = true;
}