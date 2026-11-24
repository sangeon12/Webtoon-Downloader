using System;
using System.Threading.Tasks;
using Webtoon_Downloader.Models;
using Microsoft.Playwright;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Webtoon_Downloader.Models;

namespace Webtoon_Downloader.Sites.Naver;

public class NaverWebtoon : IWebtoonSite
{
    public bool CanHandle(Uri url)
    {
        if(url.AbsoluteUri.Contains("comic.naver.com")) return true;
        else return false;
    }

    public Uri NormalizeUrl(Uri url)
    {
        var parameters = url.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries);

        string? titleId = null;

        foreach (var parameter in parameters)
        {
            var pair = parameter.Split('=', 2);

            if (pair.Length == 2 && pair[0] == "titleId")
            {
                titleId = pair[1];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(titleId) ||
            !long.TryParse(titleId, out _))
        {
            throw new ArgumentException("웹툰 ID를 찾을 수 없습니다.");
        }

        return new Uri(
            $"https://comic.naver.com/webtoon/list?titleId={titleId}"
        );
    }

   public async Task<WebtoonInfo> ExtractAsync(
    IPage page,
    Uri url)
    {
        throw new NotImplementedException();
    }

    public async Task<WebtoonListInfo> ExtractListAsync(
        IPage page,
        Uri url)
    {
        string title = (await page.Locator(
            ".EpisodeListInfo__title--mYLjC"
        ).InnerTextAsync()).Trim();

        string? href = await page.Locator(
            ".EpisodeListList__episode_list--_N3ks > " +
            ".EpisodeListList__item--M8zq4:first-child > " +
            ".EpisodeListList__link--DdClU"
        ).GetAttributeAsync("href");

        if (string.IsNullOrWhiteSpace(href))
            throw new Exception("마지막 회차 링크를 찾을 수 없습니다.");

        Uri episodeUri = new Uri(url, href);

        var parameters = episodeUri.Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .ToDictionary(x => x[0], x => x[1]);

        if (!parameters.TryGetValue("titleId", out string? titleId) ||
            !long.TryParse(titleId, out _))
        {
            throw new Exception("웹툰 ID를 찾을 수 없습니다.");
        }

        if (!parameters.TryGetValue("no", out string? no) ||
            !int.TryParse(no, out int lastEpisode) ||
            lastEpisode < 1)
        {
            throw new Exception("마지막 회차 번호를 찾을 수 없습니다.");
        }

        var episodes = new List<WebtoonEpisodeLink>();

        for (int number = 1; number <= lastEpisode; number++)
        {
            episodes.Add(new WebtoonEpisodeLink(
                number,
                $"https://comic.naver.com/webtoon/detail?titleId={titleId}&no={number}"
            ));
        }

        return new WebtoonListInfo(
            titleId,
            title,
            episodes
        );
    }

    public async Task<string> ExtractFirstImageUrlAsync(IPage page)
    {
        var handle = await page.WaitForFunctionAsync(
            """
            () => {
                const image = document.querySelector("#content_image_0");
    
                if (!image)
                    return false;
    
                const source = image.getAttribute("src");
    
                if (!source)
                    return false;
    
                const url = new URL(source, location.href);
    
                if (url.protocol !== "http:" && url.protocol !== "https:")
                    return false;
    
                return url.href;
            }
            """,
            null,
            new PageWaitForFunctionOptions
            {
                Timeout = 30000
            }
        );
    
        string? imageUrl = await handle.JsonValueAsync<string>();

        await handle.DisposeAsync();
    
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new Exception("첫 이미지 URL을 찾을 수 없습니다.");
    
        return imageUrl;
    }
}