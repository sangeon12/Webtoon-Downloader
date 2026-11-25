using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Webtoon_Downloader.Services;

public static class WebtoonImportService
{
    public static async Task<(int Success, List<string> Errors)> ImportAsync(
        IEnumerable<string> sourcePaths,
        string destinationRoot)
    {
        return await Task.Run(() =>
        {
            Directory.CreateDirectory(destinationRoot);

            int success = 0;
            var errors = new List<string>();

            foreach (string sourcePath in sourcePaths.Distinct())
            {
                string title = Path.GetFileName(
                    Path.TrimEndingDirectorySeparator(sourcePath)
                );

                string destination = Path.Combine(
                    destinationRoot,
                    title
                );

                try
                {
                    MoveDirectory(sourcePath, destination);
                    success++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{title}: {ex.Message}");
                }
            }

            return (success, errors);
        });
    }

    private static void MoveDirectory(
        string source,
        string destination)
    {
        source = Path.GetFullPath(source);
        destination = Path.GetFullPath(destination);

        if (!Directory.Exists(source))
            throw new DirectoryNotFoundException("원본 폴더가 없습니다.");

        if (Directory.Exists(destination) || File.Exists(destination))
            throw new IOException("같은 이름의 웹툰이 이미 존재합니다.");

        string relative = Path.GetRelativePath(source, destination);

        if (relative == "." ||
            (!Path.IsPathRooted(relative) &&
             relative != ".." &&
             !relative.StartsWith(
                 ".." + Path.DirectorySeparatorChar,
                 StringComparison.Ordinal)))
        {
            throw new IOException("폴더를 자기 자신 또는 하위 폴더로 이동할 수 없습니다.");
        }

        try
        {
            Directory.Move(source, destination);
            return;
        }
        catch (IOException)
        {
            if (Directory.Exists(destination) || File.Exists(destination))
                throw;

            if (!Directory.Exists(source))
                throw;
        }

        string parent = Path.GetDirectoryName(destination)!;

        string temporary = Path.Combine(
            parent,
            ".importing-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            CopyDirectory(source, temporary);
            VerifyDirectory(source, temporary);

            Directory.Move(temporary, destination);

            Directory.Delete(source, true);
        }
        catch
        {
            if (Directory.Exists(temporary))
                Directory.Delete(temporary, true);

            throw;
        }
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("심볼릭 링크 폴더는 가져올 수 없습니다.");

        Directory.CreateDirectory(destination);

        foreach (string directory in Directory.GetDirectories(source))
        {
            CopyDirectory(
                directory,
                Path.Combine(destination, Path.GetFileName(directory))
            );
        }

        foreach (string file in Directory.GetFiles(source))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("심볼릭 링크 파일은 가져올 수 없습니다.");

            File.Copy(
                file,
                Path.Combine(destination, Path.GetFileName(file))
            );
        }
    }

    private static void VerifyDirectory(
        string source,
        string destination)
    {
        string[] sourceFiles = Directory.GetFiles(
            source, "*", SearchOption.AllDirectories
        );

        foreach (string file in sourceFiles)
        {
            string relative = Path.GetRelativePath(source, file);
            string copied = Path.Combine(destination, relative);

            if (!File.Exists(copied))
                throw new IOException($"복사된 파일이 없습니다: {relative}");

            if (new FileInfo(file).Length != new FileInfo(copied).Length)
                throw new IOException($"파일 크기가 일치하지 않습니다: {relative}");
        }

        int sourceDirectoryCount = Directory.GetDirectories(
            source, "*", SearchOption.AllDirectories
        ).Length;

        int copiedDirectoryCount = Directory.GetDirectories(
            destination, "*", SearchOption.AllDirectories
        ).Length;

        if (sourceDirectoryCount != copiedDirectoryCount)
            throw new IOException("복사된 폴더 개수가 일치하지 않습니다.");

        int copiedFileCount = Directory.GetFiles(
            destination, "*", SearchOption.AllDirectories
        ).Length;

        if (sourceFiles.Length != copiedFileCount)
            throw new IOException("복사된 파일 개수가 일치하지 않습니다.");
    }
}