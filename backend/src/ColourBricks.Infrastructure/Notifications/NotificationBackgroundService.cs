using ColourBricks.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ColourBricks.Infrastructure.Notifications;

/// <summary>
/// P9-T01 — runs the notification evaluator on a schedule. Disabled unless
/// <c>Notifications:BackgroundEnabled</c> is true, so tests and local runs stay
/// quiet and drive the evaluator explicitly through the admin endpoint.
/// </summary>
public sealed class NotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<NotificationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Notifications:BackgroundEnabled", false))
        {
            return;
        }

        int minutes = Math.Max(1, configuration.GetValue("Notifications:IntervalMinutes", 5));
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));

        do
        {
            try
            {
                await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
                INotificationEvaluator evaluator = scope.ServiceProvider.GetRequiredService<INotificationEvaluator>();
                NotificationRunResult result = await evaluator.RunAsync(stoppingToken);
                if (result.Created > 0 || result.EmailsFailed > 0)
                {
                    logger.LogInformation(
                        "Notification run: {Created} new, {Sent} emailed, {Failed} email failures.",
                        result.Created, result.EmailsSent, result.EmailsFailed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification evaluation run failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
