// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace TaskReset;

public interface IServiceRestarter
{
    /// <summary>Startet den Dienst neu. Wirft eine Ausnahme, wenn das nicht gelingt.</summary>
    Task RestartAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Startet einen Windows-Dienst neu. Beendet er sich nicht innerhalb des Timeouts,
/// werden der Dienstprozess und der überwachte Prozess samt Unterprozessen hart beendet.
/// Abhängige Dienste, die vorher liefen, werden danach wieder gestartet.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsServiceRestarter : IServiceRestarter
{
    private static readonly TimeSpan KillWaitTimeout = TimeSpan.FromSeconds(30);

    private readonly WatchdogOptions _options;
    private readonly ILogger<WindowsServiceRestarter> _logger;

    public WindowsServiceRestarter(WatchdogOptions options, ILogger<WindowsServiceRestarter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task RestartAsync(CancellationToken cancellationToken) =>
        Task.Run(() => Restart(cancellationToken), cancellationToken);

    private void Restart(CancellationToken cancellationToken)
    {
        using var service = new ServiceController(_options.ServiceName);
        service.Refresh();

        var runningDependents = service.DependentServices
            .Where(d => d.Status is ServiceControllerStatus.Running or ServiceControllerStatus.StartPending)
            .Select(d => d.ServiceName)
            .ToList();
        foreach (var d in service.DependentServices)
            d.Dispose();

        var servicePid = TryGetServiceProcessId(service);

        if (service.Status is not ServiceControllerStatus.Stopped)
        {
            if (!TryStopGracefully(service))
            {
                _logger.LogWarning(EventIds.HardKill,
                    "Dienst {ServiceName} wurde nicht innerhalb von {Timeout} s beendet. Prozesse werden hart beendet.",
                    _options.ServiceName, _options.StopTimeoutSeconds);

                if (servicePid is > 0)
                    Kill(servicePid.Value, "Dienstprozess");
                KillMonitoredProcesses();

                service.Refresh();
                if (service.Status is not ServiceControllerStatus.Stopped)
                    service.WaitForStatus(ServiceControllerStatus.Stopped, KillWaitTimeout);
            }
        }

        // Falls der überwachte Prozess das Beenden des Dienstes überlebt hat, räumen wir ihn ab.
        KillMonitoredProcesses();

        cancellationToken.ThrowIfCancellationRequested();

        service.Start();
        service.WaitForStatus(ServiceControllerStatus.Running, _options.StartTimeout);

        foreach (var name in runningDependents)
        {
            try
            {
                using var dependent = new ServiceController(name);
                dependent.Refresh();
                if (dependent.Status is ServiceControllerStatus.Stopped)
                {
                    dependent.Start();
                    dependent.WaitForStatus(ServiceControllerStatus.Running, _options.StartTimeout);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ServiceProcess.TimeoutException or Win32Exception)
            {
                _logger.LogWarning(EventIds.DependentStartFailed, ex,
                    "Abhängiger Dienst {DependentService} konnte nicht wieder gestartet werden.", name);
            }
        }
    }

    private bool TryStopGracefully(ServiceController service)
    {
        try
        {
            if (service.Status is not ServiceControllerStatus.StopPending)
                service.Stop();
            service.WaitForStatus(ServiceControllerStatus.Stopped, _options.StopTimeout);
            return true;
        }
        catch (System.ServiceProcess.TimeoutException)
        {
            return false;
        }
        catch (InvalidOperationException ex)
        {
            // Zum Beispiel: Dienst nimmt gerade keine Steuerbefehle an.
            _logger.LogWarning(EventIds.HardKill, ex, "Dienst {ServiceName} ließ sich nicht sauber beenden.", _options.ServiceName);
            return false;
        }
    }

    private void KillMonitoredProcesses()
    {
        foreach (var process in Process.GetProcessesByName(_options.ProcessName))
        {
            using (process)
                Kill(process, "überwachter Prozess");
        }
    }

    private void Kill(int processId, string description)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            Kill(process, description);
        }
        catch (ArgumentException)
        {
            // Prozess läuft schon nicht mehr.
        }
    }

    private void Kill(Process process, string description)
    {
        try
        {
            var id = process.Id;
            process.Kill(entireProcessTree: true);
            process.WaitForExit(KillWaitTimeout);
            _logger.LogWarning(EventIds.HardKill, "{Description} {ProcessName} (PID {ProcessId}) wurde hart beendet.",
                description, process.ProcessName, id);
        }
        catch (InvalidOperationException)
        {
            // Prozess hat sich inzwischen selbst beendet.
        }
        catch (Win32Exception ex)
        {
            _logger.LogError(EventIds.RestartFailed, ex, "{Description} (PID {ProcessId}) konnte nicht beendet werden.",
                description, process.Id);
        }
    }

    private int? TryGetServiceProcessId(ServiceController service)
    {
        try
        {
            using var handle = service.ServiceHandle;
            var buffer = new byte[Marshal.SizeOf<ServiceStatusProcess>()];
            if (!QueryServiceStatusEx(handle, ScStatusProcessInfo, buffer, buffer.Length, out _))
                return null;
            var status = MemoryMarshal.Read<ServiceStatusProcess>(buffer);
            return status.ProcessId == 0 ? null : (int)status.ProcessId;
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            _logger.LogDebug(ex, "PID des Dienstes {ServiceName} konnte nicht ermittelt werden.", _options.ServiceName);
            return null;
        }
    }

    private const int ScStatusProcessInfo = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(SafeHandle hService, int infoLevel, byte[] buffer, int bufferSize, out int bytesNeeded);
}
