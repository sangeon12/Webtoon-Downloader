using System;
using System.Threading.Tasks;
using Webtoon_Downloader.Models;
using Microsoft.Playwright;

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
}