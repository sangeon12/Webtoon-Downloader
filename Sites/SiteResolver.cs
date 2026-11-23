using System;
using Webtoon_Downloader.Sites.Naver;

namespace Webtoon_Downloader.Sites;

public static class SiteResolver
{
    private static readonly IWebtoonSite[] Sites =
    [
        new NaverWebtoon()
    ];

    public static bool TryResolve(
        Uri url,
        out IWebtoonSite? selectedSite)
    {
        foreach (var site in Sites)
        {
            if (site.CanHandle(url))
            {
                selectedSite = site;
                return true;
            }
        }

        selectedSite = null;
        return false;
    }
}