using System;

namespace McpNetDll.Helpers;

public static class PathHelper
{
    public static string ConvertWslPath(string path)
    {
        if (OperatingSystem.IsWindows() && path.StartsWith("/mnt/") && path.Length > 6)
        {
            return $"{char.ToUpper(path[5])}:{path[6..].Replace('/', '\\')}";
        }
        return path;
    }
}

