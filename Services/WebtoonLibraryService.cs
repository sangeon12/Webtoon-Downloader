using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Webtoon_Downloader.Models;

namespace Webtoon_Downloader.Services;

public static class WebtoonLibraryService
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp"
        };

    public static IReadOnlyList<StoredWebtoon> GetWebtoons()
    {
        string root = WebtoonPathService.WebtoonsDirectory;

        return System.IO.Directory.GetDirectories(root)
            .Select(path => new StoredWebtoon(
                Path.GetFileName(path),
                path
            ))
            .OrderBy(x => x.Title, StringComparer.CurrentCulture)
            .ToArray();
    }

    public static IReadOnlyList<string> GetEpisodes(
        StoredWebtoon webtoon)
    {
        return System.IO.Directory.GetDirectories(webtoon.Directory)
            .Select(path => new
            {
                Path = path,
                Valid = int.TryParse(
                    System.IO.Path.GetFileName(path),
                    out int number
                ) && number > 0,
                Number = int.TryParse(
                    System.IO.Path.GetFileName(path),
                    out int value
                ) ? value : 0
            })
            .Where(x => x.Valid)
            .OrderBy(x => x.Number)
            .Select(x => x.Path)
            .ToArray();
    }

    public static IReadOnlyList<string> GetImages(string episodeDirectory)
    {
        return System.IO.Directory.GetFiles(episodeDirectory)
            .Where(path => ImageExtensions.Contains(
                Path.GetExtension(path)
            ))
            .OrderBy(path =>
            {
                return int.TryParse(
                    Path.GetFileNameWithoutExtension(path),
                    out int number
                ) ? number : int.MaxValue;
            })
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static void DeleteWebtoon(StoredWebtoon webtoon)
    {
        string root = Path.GetFullPath(
            WebtoonPathService.WebtoonsDirectory
        );

        string parent = Path.GetFullPath(
            Path.GetDirectoryName(webtoon.Directory)!
        );

        if (!string.Equals(
            root.TrimEnd(Path.DirectorySeparatorChar),
            parent.TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "웹툰 저장 경로 밖의 폴더는 삭제할 수 없습니다."
            );
        }

        if (System.IO.Directory.Exists(webtoon.Directory))
            System.IO.Directory.Delete(webtoon.Directory, true);
    }
}