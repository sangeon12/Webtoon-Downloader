using System.Net;

namespace Webtoon_Downloader.Sites;

public static class LoginResolver
{
    private static readonly HttpClient Client = new(
        new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10
        }
    )
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public static async Task<bool> RequiresLoginAsync(
        IWebtoonSite site,
        Uri url,
        CancellationToken cancellationToken = default)
    {
        using var response = await Client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();

        string finalUrl = response.RequestMessage?
            .RequestUri?
            .ToString() ?? url.ToString();

        return site.IsLoginPage(finalUrl);
    }
}