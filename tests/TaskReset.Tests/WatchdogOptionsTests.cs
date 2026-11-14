// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.
namespace TaskReset.Tests;

public class WatchdogOptionsTests
{
    private static WatchdogOptions Valid() => new() { ServiceName = "Svc", ProcessName = "proc" };

    [Fact]
    public void Defaults_with_names_are_valid() => Assert.Empty(Valid().Validate());

    [Theory]
    [InlineData("", "proc")]
    [InlineData("Svc", "")]
    [InlineData("TaskReset", "proc")]
    [InlineData("Svc", "proc.exe")]
    public void Names_are_checked(string service, string process)
    {
        var o = Valid();
        o.ServiceName = service;
        o.ProcessName = process;
        Assert.Single(o.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100.1)]
    public void Threshold_must_be_a_percentage(double threshold)
    {
        var o = Valid();
        o.CpuThresholdPercent = threshold;
        Assert.Single(o.Validate());
    }

    [Fact]
    public void Interval_must_not_exceed_window()
    {
        var o = Valid();
        o.AverageWindowMinutes = 1;
        o.SampleIntervalSeconds = 61;
        Assert.Single(o.Validate());
    }

    [Fact]
    public void Negative_cooldown_is_invalid()
    {
        var o = Valid();
        o.CooldownMinutes = -1;
        Assert.Single(o.Validate());
    }
}
