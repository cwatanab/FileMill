using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FileMill.Helpers;

public static class FileMoveHelper
{
    public static void MoveWithRetries(string sourcePath, string destinationPath, int retryCount = 5, int delayMilliseconds = 100)
    {
        while (retryCount > 0)
        {
            try
            {
                File.Move(sourcePath, destinationPath);
                return;
            }
            catch (IOException)
            {
                retryCount--;
                if (retryCount == 0)
                    throw;

                CollectPendingFinalizers();
                Thread.Sleep(delayMilliseconds);
            }
        }
    }

    public static async Task MoveWithRetriesAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken,
        int retryCount = 5,
        int delayMilliseconds = 100)
    {
        while (retryCount > 0)
        {
            try
            {
                File.Move(sourcePath, destinationPath);
                return;
            }
            catch (IOException)
            {
                retryCount--;
                if (retryCount == 0)
                    throw;

                CollectPendingFinalizers();
                await Task.Delay(delayMilliseconds, cancellationToken);
            }
        }
    }

    public static void DeleteIfExists(string? path)
    {
        if (path == null || !File.Exists(path))
            return;

        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static void CollectPendingFinalizers()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}
