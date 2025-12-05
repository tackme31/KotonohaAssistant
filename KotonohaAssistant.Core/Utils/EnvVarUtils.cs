using System.IO;

namespace KotonohaAssistant.Core.Utils;

public static class EnvVarUtils
{
    public static string? TraverseEnvFileFolder(string startPath)
    {
        var currentDir = new DirectoryInfo(startPath);

        while (currentDir != null)
        {
            string envFilePath = Path.Combine(currentDir.FullName, ".env");
            if (File.Exists(envFilePath))
            {
                return currentDir.FullName;
            }

            currentDir = currentDir.Parent;
        }

        return null;
    }
}
