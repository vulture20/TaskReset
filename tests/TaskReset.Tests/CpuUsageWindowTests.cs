// TaskReset - überwacht die CPU-Last eines Prozesses und startet den zugehörigen Dienst neu.
// Copyright © 2026 Thorsten Schröpel
//
// This program is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation, either version 3
// of the License, or (at your option) any later version. See the LICENSE file for details.
namespace TaskReset.Tests;

public class CpuUsageWindowTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static CpuSample At(double secondsFromT0, double percent, double durationSeconds = 5, int processes = 1) =>
        new(T0.AddSeconds(secondsFromT0), TimeSpan.FromSeconds(durationSeconds), percent, processes);

    [Fact]
    public void Empty_window_has_no_average_and_is_not_full()
    {
        var window = new CpuUsageWindow(TimeSpan.FromMinutes(5));
        Assert.Null(window.Average);
        Assert.False(window.IsFull);
    }

    [Fact]
    public void Window_is_full_only_after_full_duration()
    {
        var window = new CpuUsageWindow(TimeSpan.FromMinutes(5));
        for (var t = 5; t < 300; t += 5)
            window.Add(At(t, 100));
        Assert.False(window.IsFull);

        window.Add(At(300, 100));
        Assert.True(window.IsFull);
    }

    [Fact]
    public void Average_is_time_weighted()
    {
        var window = new CpuUsageWindow(TimeSpan.FromSeconds(30));
        window.Add(At(10, 90, durationSeconds: 10));
        window.Add(At(30, 30, durationSeconds: 20));
        // (90*10 + 30*20) / 30 = 50
        Assert.Equal(50, window.Average!.Value, 6);
    }

    [Fact]
    public void Old_samples_fall_out_of_the_window()
    {
        var window = new CpuUsageWindow(TimeSpan.FromSeconds(10));
        window.Add(At(5, 100));
        window.Add(At(10, 100));
        window.Add(At(15, 0));
        window.Add(At(20, 0));
        Assert.Equal(0, window.Average!.Value, 6);
    }

    [Fact]
    public void Partially_overlapping_sample_is_clipped()
    {
        var window = new CpuUsageWindow(TimeSpan.FromSeconds(10));
        window.Add(At(8, 100, durationSeconds: 8)); // 0..8
        window.Add(At(14, 0, durationSeconds: 6));  // 8..14, Fenster 4..14
        // 100 % für 4 s (4..8), 0 % für 6 s -> 40 %
        Assert.Equal(40, window.Average!.Value, 6);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var window = new CpuUsageWindow(TimeSpan.FromSeconds(10));
        window.Add(At(5, 100));
        window.Add(At(10, 100));
        Assert.True(window.IsFull);

        window.Reset();
        Assert.False(window.IsFull);
        Assert.Null(window.Average);
    }

    [Fact]
    public void Samples_going_back_in_time_are_ignored()
    {
        var window = new CpuUsageWindow(TimeSpan.FromSeconds(10));
        window.Add(At(10, 50));
        window.Add(At(5, 100));
        Assert.Equal(50, window.Average!.Value, 6);
    }
}
