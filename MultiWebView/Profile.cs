namespace MultiWebView;

public sealed class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Person 1";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
    public string StartUrl { get; set; } = ProfileStore.DefaultStartUrl;
    public int VolumePercent { get; set; } = 100;
    public bool IsMuted { get; set; }
    public bool ShowStatsFps { get; set; }
    public bool ShowStatsCpu { get; set; }
    public bool ShowStatsMemory { get; set; }
    public bool ShowStatsHorizontal { get; set; }
}
