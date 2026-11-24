namespace Webtoon_Downloader.Models;

public record WebtoonEpisode(
    int Number,
    string Url,
    string FirstImageUrl
);

public record WebtoonInfo(
    string TitleId,
    string Title,
    int EpisodeCount,
    IReadOnlyList<WebtoonEpisode> Episodes
);

public record WebtoonListInfo(
    string TitleId,
    string Title,
    IReadOnlyList<WebtoonEpisodeLink> Episodes
);

public record WebtoonEpisodeLink(
    int Number,
    string Url
);