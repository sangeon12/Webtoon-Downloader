using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Webtoon_Downloader.Services;

public static class AccountConfigService
{
    public static string ConfigPath
    {
        get
        {
            string directory;

            if (OperatingSystem.IsMacOS())
            {
                directory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.UserProfile),
                    "Library",
                    "Application Support",
                    "Webtoon-Downloader"
                );
            }
            else if (OperatingSystem.IsWindows())
            {
                directory = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.LocalApplicationData),
                    "Webtoon-Downloader"
                );
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            return Path.Combine(directory, "webtoon-account.json");
        }
    }

    public static void Initialize()
    {
        string path = ConfigPath;

        Directory.CreateDirectory(
            Path.GetDirectoryName(path)!
        );

        if (File.Exists(path))
            return;

        var config = new
        {
            sites = new[]
            {
                "Naver"
            },
            accounts = new
            {
            }
        };

        string json = JsonSerializer.Serialize(
            config,
            new JsonSerializerOptions
            {
                WriteIndented = true
            }
        );

        File.WriteAllText(path, json);
    }

    public static string[] GetSites()
    {
        using JsonDocument document =
            JsonDocument.Parse(File.ReadAllText(ConfigPath));

        return document.RootElement
            .GetProperty("sites")
            .EnumerateArray()
            .Select(x => x.GetString() ?? "")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    public static void SaveAccountId(
        string site,
        string id)
    {
        Initialize();

        if (!GetSites().Contains(site, StringComparer.Ordinal))
            throw new ArgumentException("지원하지 않는 사이트입니다.");

        var root = JsonNode.Parse(
            File.ReadAllText(ConfigPath)
        ) as JsonObject
            ?? throw new InvalidDataException(
                "계정 설정 JSON이 올바르지 않습니다."
            );

        if (root["accounts"] is not JsonObject accounts)
        {
            accounts = new JsonObject();
            root["accounts"] = accounts;
        }

        accounts[site] = new JsonObject
        {
            ["id"] = id
        };

        string temporaryPath = ConfigPath + ".tmp";

        try
        {
            File.WriteAllText(
                temporaryPath,
                root.ToJsonString(
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }
                )
            );

            File.Move(
                temporaryPath,
                ConfigPath,
                true
            );
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static string? GetAccountId(string site)
    {
        Initialize();
    
        using var document = JsonDocument.Parse(
            File.ReadAllText(ConfigPath)
        );
    
        if (!document.RootElement.TryGetProperty(
            "accounts", out var accounts))
            return null;
    
        if (!accounts.TryGetProperty(site, out var account))
            return null;
    
        if (!account.TryGetProperty("id", out var id))
            return null;
    
        return id.GetString();
    }
}