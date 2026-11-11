using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Marites.Worker;

internal sealed class CaptureWorker(ILogger<CaptureWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Marites worker host started; capture behavior is not implemented yet.");
        await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
    }
}
