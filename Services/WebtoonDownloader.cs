using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Webtoon_Downloader.Models;

namespace Webtoon_Downloader.Services;

public sealed class WebtoonDownloader : IDisposable
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private static readonly Regex ImagePattern = new(
        @"^(?<prefix>.+_)(?<number>\d+)(?<extension>\.(?:jpg|jpeg|png|webp|gif))$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public WebtoonDownloader()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0"
        );
    }

    public async Task<int> DownloadEpisodeAsync(
        string title,
        WebtoonEpisode episode,
        Action<int>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        string root = Path.Combine(
            WebtoonPathService.WebtoonsDirectory,
            SanitizeFileName(title),
            episode.Number.ToString()
        );

        Directory.CreateDirectory(root);

        Uri firstUrl = new Uri(episode.FirstImageUrl);

        Match match = ImagePattern.Match(firstUrl.AbsolutePath);

        if (!match.Success)
        {
            throw new Exception(
                $"{episode.Number}화 이미지 URL 형식을 인식하지 못했습니다."
            );
        }

        string prefix = match.Groups["prefix"].Value;
        string extension = match.Groups["extension"].Value;

        int number = int.Parse(
            match.Groups["number"].Value
        );

        int downloaded = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var builder = new UriBuilder(firstUrl)
            {
                Path = $"{prefix}{number}{extension}"
            };

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                builder.Uri
            );

            request.Headers.Referrer = new Uri(episode.Url);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                if (downloaded == 0)
                {
                    throw new Exception(
                        $"{episode.Number}화 첫 이미지가 존재하지 않습니다."
                    );
                }

                break;
            }

            response.EnsureSuccessStatusCode();

            string? mediaType = response.Content.Headers
                .ContentType?.MediaType;

            if (mediaType == null ||
                !mediaType.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase
                ))
            {
                throw new Exception(
                    $"{episode.Number}화 {number}번 이미지 응답이 올바르지 않습니다."
                );
            }

            string filePath = Path.Combine(
                root,
                $"{downloaded + 1}{extension}"
            );

            string temporaryPath = filePath + ".tmp";

            try
            {
                await using (var file = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None
                ))
                {
                    await response.Content.CopyToAsync(
                        file,
                        cancellationToken
                    );
                }

                cancellationToken.ThrowIfCancellationRequested();

                File.Move(
                    temporaryPath,
                    filePath,
                    true
                );
            }
            catch
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);

                throw;
            }

            downloaded++;

            cancellationToken.ThrowIfCancellationRequested();
            
            onProgress?.Invoke(downloaded);
            number++;
        }

        return downloaded;
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalidChars =
        [
            '<', '>', ':', '"', '/', '\\',
            '|', '?', '*'
        ];

        string result = name.Trim();

        foreach (char character in invalidChars)
            result = result.Replace(character, '_');

        result = result.TrimEnd('.', ' ');

        if (string.IsNullOrWhiteSpace(result))
            result = "Untitled";

        return result;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}