using System;

namespace Webtoon_Downloader.Services;

public sealed class AuthenticationFailedException : Exception
{
    public AuthenticationFailedException(string message)
        : base(message)
    {
    }
}