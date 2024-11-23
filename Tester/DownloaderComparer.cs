namespace Tester;

using EmbeddedDownloader;
using TaskfulDownloader;
using AsyncDownloader;

internal static class DownloaderComparer {
    private static readonly string[] ResultDirs = ["asyncResults", "taskResults", "embeddedResults"];

    private static async Task Main() {
        CleanDirectories();

        RunAndCaptureResults(EmbeddedDownloader.Main, "embeddedResults");
        RunAndCaptureResults(TaskfulDownloader.Main, "taskResults");
        await RunAndCaptureResultsAsync(AsyncDownloader.Main, "asyncResults");
        VerifyResults();
    }

    private static void CleanDirectories() {
        foreach (var dir in ResultDirs) {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
            Directory.CreateDirectory(dir);
        }
    }

    private static void RunAndCaptureResults(Action downloaderMain, string outputDir) {
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(outputDir);

        Console.WriteLine($"Running downloader: {downloaderMain.Method.DeclaringType?.Name}");
        downloaderMain.Invoke();
        Directory.SetCurrentDirectory(originalDir);
    }

    private static async Task RunAndCaptureResultsAsync(Func<Task> downloaderMain, string outputDir) {
        var originalDir = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(outputDir);

        Console.WriteLine($"Running downloader: {downloaderMain.Method.DeclaringType?.Name}");
        await downloaderMain();

        Directory.SetCurrentDirectory(originalDir);
    }

    private static void VerifyResults() {
        var checkingDir = ResultDirs[0];

        foreach (var dir in ResultDirs.Skip(1)) {
            Console.WriteLine($"\nComparing {checkingDir} with {dir}...");
            var refFiles = Directory.GetFiles(checkingDir).OrderBy(f => f).ToArray();
            var compFiles = Directory.GetFiles(dir).OrderBy(f => f).ToArray();

            if (refFiles.Length != compFiles.Length) {
                Console.WriteLine($"Mismatch in file count between {checkingDir} and {dir}.");
                continue;
            }

            for (var i = 0; i < refFiles.Length; i++) {
                var refContent = File.ReadAllText(refFiles[i]);
                var compContent = File.ReadAllText(compFiles[i]);

                Console.WriteLine(!string.Equals(refContent, compContent)
                                      ? $"File {Path.GetFileName(refFiles[i])} differs between {checkingDir} and {dir}."
                                      : $"File {Path.GetFileName(refFiles[i])} matches.");
            }
        }

        Console.WriteLine("\nComparison complete.");
    }
}