// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.
using Microsoft.Extensions.Logging.EventLog;
using Microsoft.Extensions.Options;
using TaskReset;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("TaskReset läuft nur unter Windows.");
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options => options.ServiceName = "TaskReset");
builder.Services.Configure<EventLogSettings>(settings =>
{
    if (OperatingSystem.IsWindows())
    {
        settings.SourceName = "TaskReset";
        settings.LogName = "Application";
    }
});
builder.Logging.AddEventLog();

builder.Services.AddOptions<WatchdogOptions>()
    .Bind(builder.Configuration.GetSection(WatchdogOptions.SectionName))
    .PostConfigure(o =>
    {
        o.ServiceName = o.ServiceName.Trim();
        o.ProcessName = o.ProcessName.Trim();
    });

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<WatchdogOptions>>().Value);
builder.Services.AddSingleton<ICpuSampler>(sp => new ProcessCpuSampler(
    sp.GetRequiredService<WatchdogOptions>().ProcessName,
    sp.GetRequiredService<ILogger<ProcessCpuSampler>>(),
    sp.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<IServiceRestarter, WindowsServiceRestarter>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

var configErrors = host.Services.GetRequiredService<IOptions<WatchdogOptions>>().Value.Validate();
if (configErrors.Count > 0)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(EventIds.InvalidConfiguration, "Ungültige Konfiguration:{NewLine}{Errors}",
        Environment.NewLine, string.Join(Environment.NewLine, configErrors));
    return 2;
}

await host.RunAsync();
return 0;
