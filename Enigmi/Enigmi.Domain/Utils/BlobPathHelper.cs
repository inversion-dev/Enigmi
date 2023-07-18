using Enigmi.Common;
using static System.FormattableString;

namespace Enigmi.Domain.Utils;

public static class BlobPathHelper
{
    public static string PrependBlobPathIfRequired(Settings settings, string uploadPath)
    {
        settings.ThrowIfNull();
        
        if (string.IsNullOrEmpty(settings.EnvironmentPrefix))
        {
            return uploadPath;
        }

        return Invariant($"/{settings.EnvironmentPrefix}{uploadPath}");
    }
}