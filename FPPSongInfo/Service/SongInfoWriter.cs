using System.Text;
using FPPSongInfo.Configuration;
using Microsoft.Extensions.Options;

namespace FPPSongInfo.Service;

internal sealed class SongInfoWriter(
    IOptions<OutputOptions> outputOptions,
    ILogger<SongInfoWriter> logger)
    : ISongInfoWriter, IDisposable
{
    private const int ReplaceRetryCount = 3;
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);
    private readonly ILogger<SongInfoWriter> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly OutputOptions _outputOptions = outputOptions?.Value ?? throw new ArgumentNullException(nameof(outputOptions));
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task UpdateSongInfoAsync(SongInfo songInfo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(songInfo);

        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            var destinationPath = Path.Combine(_outputOptions.FilePath, _outputOptions.FileName);
            var temporaryPath = Path.Combine(
                _outputOptions.FilePath,
                $".{_outputOptions.FileName}.{Guid.NewGuid():N}.tmp");
            var content = CreateSongInfoContent(songInfo);

            Directory.CreateDirectory(_outputOptions.FilePath);

            try
            {
                await WriteTextAsync(temporaryPath, FileMode.CreateNew, FileShare.None, content, cancellationToken);

                if (await TryReplaceAsync(temporaryPath, destinationPath, cancellationToken))
                {
                    _logger.LogDebug("Atomically wrote song info to {Path}", destinationPath);
                    return;
                }

                _logger.LogWarning(
                    "Falling back to a compatible in-place song info write for {Path}",
                    destinationPath);
                await WriteTextAsync(destinationPath, FileMode.Create, FileShare.ReadWrite, content, cancellationToken);
            }
            finally
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<bool> TryReplaceAsync(
        string temporaryPath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= ReplaceRetryCount; attempt++)
        {
            try
            {
                File.Move(temporaryPath, destinationPath, true);
                return true;
            }
            catch (IOException exception) when (attempt < ReplaceRetryCount)
            {
                var delay = TimeSpan.FromMilliseconds(50 * attempt);
                _logger.LogDebug(
                    exception,
                    "Could not atomically replace song info file {Path}; retrying in {Delay}",
                    destinationPath,
                    delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (IOException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Could not atomically replace song info file {Path} after {Attempts} attempts",
                    destinationPath,
                    ReplaceRetryCount);
                return false;
            }
        }

        return false;
    }

    private static string CreateSongInfoContent(SongInfo songInfo) =>
        $"{songInfo.Artist}{(string.IsNullOrEmpty(songInfo.Artist) ? string.Empty : " - ")}{songInfo.Title}{Environment.NewLine}";

    private static async Task WriteTextAsync(
        string path,
        FileMode mode,
        FileShare share,
        string content,
        CancellationToken cancellationToken)
    {
        await using var fileStream = new FileStream(
            path,
            mode,
            FileAccess.Write,
            share,
            bufferSize: 4096,
            FileOptions.Asynchronous);
        await using var writer = new StreamWriter(fileStream, Utf8WithoutBom, leaveOpen: true);

        await writer.WriteAsync(content.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        await fileStream.FlushAsync(cancellationToken);
    }

    private void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "Could not remove temporary song info file {Path}", temporaryPath);
        }
    }

    public void Dispose() => _writeLock.Dispose();
}
