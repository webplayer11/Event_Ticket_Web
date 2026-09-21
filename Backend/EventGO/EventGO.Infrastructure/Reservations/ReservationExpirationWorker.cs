using EventGO.Application.Reservations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EventGO.Infrastructure.Reservations;

public sealed class ReservationExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationOptions _options;
    private readonly ILogger<ReservationExpirationWorker> _logger;

    public ReservationExpirationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ReservationOptions> options,
        ILogger<ReservationExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_options.SweepInterval <= TimeSpan.Zero)
        {
            _logger.LogWarning(
                "Reservation expiration worker is disabled because Reservation:SweepInterval is not configured.");
            return;
        }

        using var timer = new PeriodicTimer(_options.SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var expirationService = scope.ServiceProvider
                    .GetRequiredService<IReservationExpirationService>();
                var count = await expirationService.ExpireDueAsync(stoppingToken);

                if (count > 0)
                {
                    _logger.LogInformation(
                        "Expired {ReservationCount} ticket reservations.", count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Reservation expiration sweep failed.");
            }
        }
    }
}
