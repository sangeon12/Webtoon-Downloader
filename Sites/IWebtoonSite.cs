using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Webtoon_Downloader.Models;

namespace Webtoon_Downloader.Sites;

public interface IWebtoonSite
{
    bool CanHandle(Uri url);

    Uri NormalizeUrl(Uri url);

    Task<WebtoonInfo> ExtractAsync(IPage page, Uri url);
}