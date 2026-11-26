using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Webtoon_Downloader.Models;
using Webtoon_Downloader.Sites;

namespace Webtoon_Downloader.Services;

public record WebtoonDownloadProgress(
    int Completed,
    int Failed,
    int Total,
    int Episode,
    int Images
);

public record WebtoonEpisodeFailure(
    int Number,
    string Error
);

public record WebtoonBatchResult(
    WebtoonInfo Info,
    int Total,
    IReadOnlyList<WebtoonEpisodeFailure> Failures
)
{
    public int Completed => Info.EpisodeCount;
    public int Failed => Failures.Count;
}

public sealed class WebtoonExtractionService
{
    private const int MaxWorkers = 8;

    private readonly BrowserService _browserService;

    public WebtoonExtractionService(BrowserService browserService)
    {
        _browserService = browserService;
    }

    public async Task<WebtoonBatchResult> ExtractAndDownloadAsync(
        IWebtoonSite site,
        Uri inputUrl,
        WebtoonDownloader downloader,
        Action<WebtoonDownloadProgress>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        Uri targetUrl = site.NormalizeUrl(inputUrl);

        await _browserService.PrepareAsync(1);

        WebtoonListInfo list = await _browserService.RunPageAsync(
            targetUrl,
            page => site.ExtractListAsync(page, targetUrl)
        );

        int total = list.Episodes.Count;

        if (total == 0)
            throw new Exception("다운로드할 회차가 없습니다.");

        int workerCount = Math.Min(MaxWorkers, total);

        await _browserService.PrepareAsync(workerCount);

        var episodes = new ConcurrentBag<WebtoonEpisode>();
        var failures = new ConcurrentBag<WebtoonEpisodeFailure>();

        int nextIndex = -1;
        int completed = 0;
        int failed = 0;

        async Task WorkerAsync(CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
        
                int index = Interlocked.Increment(ref nextIndex);
        
                if (index >= total)
                    return;
        
                token.ThrowIfCancellationRequested();
        
                var episodeLink = list.Episodes[index];
        
                try
                {
                    Uri episodeUrl = new Uri(episodeLink.Url);
        
                    string firstImageUrl =
                        await _browserService.RunPageAsync(
                            episodeUrl,
                            page => site.ExtractFirstImageUrlAsync(page)
                        );
        
                    token.ThrowIfCancellationRequested();
        
                    var episode = new WebtoonEpisode(
                        episodeLink.Number,
                        episodeLink.Url,
                        firstImageUrl
                    );
        
                    int imageCount =
                        await downloader.DownloadEpisodeAsync(
                            list.Title,
                            episode,
                            imageNumber =>
                            {
                                if (token.IsCancellationRequested)
                                    return;
                    
                                onProgress?.Invoke(
                                    new WebtoonDownloadProgress(
                                        Volatile.Read(ref completed),
                                        Volatile.Read(ref failed),
                                        total,
                                        episode.Number,
                                        imageNumber
                                    )
                                );
                            },
                            token
                        );
        
                    token.ThrowIfCancellationRequested();
        
                    episodes.Add(episode);
        
                    int completedCount =
                        Interlocked.Increment(ref completed);
        
                    if (!token.IsCancellationRequested)
                    {
                        onProgress?.Invoke(
                            new WebtoonDownloadProgress(
                                completedCount,
                                Volatile.Read(ref failed),
                                total,
                                episode.Number,
                                imageCount
                            )
                        );
                    }
                }
                catch (OperationCanceledException)
                    when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (token.IsCancellationRequested)
                        throw new OperationCanceledException(token);
        
                    failures.Add(new WebtoonEpisodeFailure(
                        episodeLink.Number,
                        ex.Message
                    ));
        
                    int failedCount =
                        Interlocked.Increment(ref failed);
        
                    onProgress?.Invoke(
                        new WebtoonDownloadProgress(
                            Volatile.Read(ref completed),
                            failedCount,
                            total,
                            episodeLink.Number,
                            0
                        )
                    );
                }
            }
        }

        Task[] workers = Enumerable.Range(0, workerCount)
            .Select(_ => WorkerAsync(cancellationToken))
            .ToArray();

        await Task.WhenAll(workers);

        var completedEpisodes = episodes
            .OrderBy(x => x.Number)
            .ToArray();

        var failedEpisodes = failures
            .OrderBy(x => x.Number)
            .ToArray();

        var info = new WebtoonInfo(
            list.TitleId,
            list.Title,
            completedEpisodes.Length,
            completedEpisodes
        );

        return new WebtoonBatchResult(
            info,
            total,
            failedEpisodes
        );
    }
}