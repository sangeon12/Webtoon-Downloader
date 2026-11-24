using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Webtoon_Downloader.Models;

namespace Webtoon_Downloader.Services;

public static class WebtoonPathService
{
    private static readonly object Sync = new();

    private static string? _webtoonDirectory;

    private static string ConfigDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData
                    ),
                    "Webtoon-Downloader"
                );
            }

            if (OperatingSystem.IsMacOS())
            {
                return Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile
                    ),
                    "Library",
                    "Application Support",
                    "Webtoon-Downloader"
                );
            }

            throw new PlatformNotSupportedException(
                "지원하지 않는 운영체제입니다."
            );
        }
    }

    private static string ConfigPath =>
        Path.Combine(ConfigDirectory, "webtoon-path.json");

    public static string WebtoonsDirectory => Initialize();

    public static string Initialize()
    {
        lock (Sync)
        {
            if (_webtoonDirectory != null)
                return _webtoonDirectory;

            Directory.CreateDirectory(ConfigDirectory);

            WebtoonPathConfig config;

            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);

                config = JsonSerializer.Deserialize<WebtoonPathConfig>(
                    json
                ) ?? new WebtoonPathConfig();
            }
            else
            {
                config = new WebtoonPathConfig();
            }

            if (string.IsNullOrWhiteSpace(config.WebtoonDirectory))
            {
                config.WebtoonDirectory = Path.Combine(
                    ConfigDirectory,
                    "Webtoons"
                );

                string json = JsonSerializer.Serialize(
                    config,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                );

                File.WriteAllText(ConfigPath, json);
            }

            string directory = config.WebtoonDirectory.Trim();

            if (!Path.IsPathFullyQualified(directory))
            {
                throw new InvalidDataException(
                    "webtoon-path.json에 올바른 절대 경로가 없습니다."
                );
            }

            Directory.CreateDirectory(directory);

            _webtoonDirectory = Path.GetFullPath(directory);

            return _webtoonDirectory;
        }
    }

    public static string[] GetWebtoonDirectories()
    {
        string root = WebtoonsDirectory;

        return Directory.GetDirectories(root)
            .OrderBy(
                path => Path.GetFileName(path),
                StringComparer.CurrentCulture
            )
            .ToArray();
    }
}