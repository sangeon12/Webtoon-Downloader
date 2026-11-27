using Microsoft.Playwright;
using Webtoon_Downloader.Models;

namespace Webtoon_Downloader.Sites;

public interface IWebtoonSite
{

    bool CanHandle(Uri url);

    bool IsLoginPage(string url);

    Uri NormalizeUrl(Uri url);

    Task<WebtoonListInfo> ExtractListAsync(IPage page, Uri url);

    Task<string> ExtractFirstImageUrlAsync(IPage page);

    Task<string> AuthenticateAsync(CancellationToken cancellationToken = default);
}