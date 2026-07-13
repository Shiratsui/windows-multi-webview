namespace MultiWebView;

public readonly record struct ProfileUsageSnapshot(
    string ProfileName,
    string State,
    string Cpu,
    string Memory,
    string Gpu,
    string GpuMemory);
