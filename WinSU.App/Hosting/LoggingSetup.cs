using WinSU.Core.Storage;
using Serilog;
using Serilog.Events;

namespace WinSU.App.Hosting;

internal static class LoggingSetup
{
    private const string OutputTemplate =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}";

    /// <summary>Logs dans <c>logs\winsu-AAAAMMJJ.log</c>, un fichier par jour, 7 jours conservés.</summary>
    public static Serilog.ILogger CreateLogger(IDataPaths paths) =>
        new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(paths.LogsDirectory, "winsu-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: OutputTemplate)
            .WriteTo.Debug(outputTemplate: OutputTemplate)
            .CreateLogger();
}
