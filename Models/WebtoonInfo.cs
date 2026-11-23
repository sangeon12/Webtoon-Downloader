namespace Webtoon_Downloader.Models;

public record WebtoonEpisode(
    int Number,
    string Url
);

public record WebtoonInfo(
    string TitleId,
    string Title,
    int EpisodeCount,
    string ImageSelector,
    IReadOnlyList<WebtoonEpisode> Episodes
);