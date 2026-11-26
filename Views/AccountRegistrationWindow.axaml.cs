using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Webtoon_Downloader.Services;

namespace Webtoon_Downloader.Views;

public partial class AccountRegistrationWindow : Window
{
    private bool _isSaving;

    public AccountRegistrationWindow()
    {
        InitializeComponent();

        AccountConfigService.Initialize();

        SiteComboBox.ItemsSource =
            AccountConfigService.GetSites();

        if (SiteComboBox.ItemCount > 0)
            SiteComboBox.SelectedIndex = 0;
    }

    private async void Register_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_isSaving)
            return;

        string? site = SiteComboBox.SelectedItem as string;
        string id = IdInput.Text?.Trim() ?? "";
        string password = PasswordInput.Text ?? "";

        if (string.IsNullOrWhiteSpace(site) ||
            string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrEmpty(password))
        {
            RegistrationStatus.Text = "모든 항목을 입력해주세요.";
            return;
        }

        _isSaving = true;
        RegisterButton.IsEnabled = false;
        RegistrationStatus.Text = "계정 저장 중";

        try
        {
            await Task.Run(() =>
            {
                SecurePasswordStorage.Save(site, id, password);

                AccountConfigService.SaveAccountId(site, id);
            });

            PasswordInput.Text = "";
            Close();
        }
        catch (Exception ex)
        {
            RegistrationStatus.Text =
                $"계정 저장 실패: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
            RegisterButton.IsEnabled = true;
        }
    }

    private void Close_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_isSaving)
            return;

        Close();
    }
}