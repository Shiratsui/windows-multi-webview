using System.Diagnostics;

namespace MultiWebView;

internal static class GpuStatsSampler
{
    private const string GpuEngineCategory = "GPU Engine";
    private const string GpuProcessMemoryCategory = "GPU Process Memory";
    private const string GpuUtilizationCounter = "Utilization Percentage";
    private const string GpuLocalMemoryCounter = "Local Usage";
    private const string GpuDedicatedMemoryCounter = "Dedicated Usage";
    private static readonly object CounterLock = new();
    private static readonly Dictionary<string, PerformanceCounter> Counters = [];

    public static GpuStatsSnapshot Sample(IReadOnlySet<int> processIds)
    {
        if (processIds.Count == 0)
        {
            return default;
        }

        return new GpuStatsSnapshot(
            SampleUtilization(processIds, out var hasGpuUtilization),
            SampleMemory(processIds, out var hasGpuMemory),
            hasGpuUtilization,
            hasGpuMemory);
    }

    private static double SampleUtilization(IReadOnlySet<int> processIds, out bool hasValue)
    {
        hasValue = false;
        var total = 0d;

        try
        {
            var category = new PerformanceCounterCategory(GpuEngineCategory);
            var instanceNames = category.GetInstanceNames();
            PruneCounters(GpuEngineCategory, GpuUtilizationCounter, instanceNames);

            foreach (var instanceName in instanceNames)
            {
                if (!BelongsToProcessTree(instanceName, processIds))
                {
                    continue;
                }

                total += Math.Max(0, ReadCounterValue(
                    GpuEngineCategory,
                    GpuUtilizationCounter,
                    instanceName));
                hasValue = true;
            }
        }
        catch
        {
            hasValue = false;
            return 0;
        }

        return total;
    }

    private static double SampleMemory(IReadOnlySet<int> processIds, out bool hasValue)
    {
        hasValue = false;

        try
        {
            var category = new PerformanceCounterCategory(GpuProcessMemoryCategory);
            var counterName = GetGpuMemoryCounterName(category);
            var instanceNames = category.GetInstanceNames();
            PruneCounters(GpuProcessMemoryCategory, counterName, instanceNames);
            var total = 0d;

            foreach (var instanceName in instanceNames)
            {
                if (!BelongsToProcessTree(instanceName, processIds))
                {
                    continue;
                }

                total += Math.Max(0, ReadCounterValue(
                    GpuProcessMemoryCategory,
                    counterName,
                    instanceName));
                hasValue = true;
            }

            return total;
        }
        catch
        {
            hasValue = false;
            return 0;
        }
    }

    private static string GetGpuMemoryCounterName(PerformanceCounterCategory category)
    {
        try
        {
            var counters = category.GetCounters(category.GetInstanceNames().FirstOrDefault() ?? string.Empty)
                .Select(counter => counter.CounterName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (counters.Contains(GpuLocalMemoryCounter))
            {
                return GpuLocalMemoryCounter;
            }

            if (counters.Contains(GpuDedicatedMemoryCounter))
            {
                return GpuDedicatedMemoryCounter;
            }
        }
        catch
        {
        }

        return GpuLocalMemoryCounter;
    }

    private static float ReadCounterValue(string categoryName, string counterName, string instanceName)
    {
        var key = CreateCounterKey(categoryName, counterName, instanceName);

        lock (CounterLock)
        {
            if (!Counters.TryGetValue(key, out var counter))
            {
                counter = new PerformanceCounter(
                    categoryName,
                    counterName,
                    instanceName,
                    readOnly: true);
                Counters[key] = counter;
            }

            return counter.NextValue();
        }
    }

    private static void PruneCounters(string categoryName, string counterName, IReadOnlyCollection<string> liveInstanceNames)
    {
        var liveKeys = liveInstanceNames
            .Select(instanceName => CreateCounterKey(categoryName, counterName, instanceName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (CounterLock)
        {
            foreach (var key in Counters.Keys.ToList())
            {
                if (!key.StartsWith($"{categoryName}|{counterName}|", StringComparison.OrdinalIgnoreCase) || liveKeys.Contains(key))
                {
                    continue;
                }

                Counters[key].Dispose();
                Counters.Remove(key);
            }
        }
    }

    private static string CreateCounterKey(string categoryName, string counterName, string instanceName)
    {
        return $"{categoryName}|{counterName}|{instanceName}";
    }

    private static bool BelongsToProcessTree(string instanceName, IReadOnlySet<int> processIds)
    {
        foreach (var processId in processIds)
        {
            if (instanceName.StartsWith($"pid_{processId}_", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

internal readonly record struct GpuStatsSnapshot(
    double GpuUtilizationPercent,
    double GpuMemoryBytes,
    bool HasGpuUtilization,
    bool HasGpuMemory);
