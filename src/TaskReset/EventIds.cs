// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.
namespace TaskReset;

/// <summary>Ereignis-IDs, unter denen TaskReset in die Windows-Ereignisanzeige schreibt.</summary>
public static class EventIds
{
    public static readonly EventId Started = new(1000, nameof(Started));
    public static readonly EventId Stopped = new(1001, nameof(Stopped));
    public static readonly EventId ThresholdExceeded = new(2000, nameof(ThresholdExceeded));
    public static readonly EventId Restarted = new(2001, nameof(Restarted));
    public static readonly EventId CooldownEnded = new(2002, nameof(CooldownEnded));
    public static readonly EventId ProcessNotFound = new(3000, nameof(ProcessNotFound));
    public static readonly EventId HardKill = new(3001, nameof(HardKill));
    public static readonly EventId DependentStartFailed = new(3002, nameof(DependentStartFailed));
    public static readonly EventId RestartFailed = new(4000, nameof(RestartFailed));
    public static readonly EventId InvalidConfiguration = new(4001, nameof(InvalidConfiguration));
    public static readonly EventId Status = new(5000, nameof(Status));
}
