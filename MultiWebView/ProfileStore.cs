using System.Text.Json;

namespace MultiWebView;

public sealed class ProfileStore
{
    public const string DefaultStartUrl = "https://www.google.com/";

    public static readonly string DefaultProfilesPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MultiWebView",
        "Profiles");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string settingsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MultiWebView");

    private string SettingsPath => Path.Combine(settingsFolder, "settings.json");

    public string AppDataPath { get; private set; }

    public string ProfilesPath => Path.Combine(AppDataPath, "profiles.json");

    public ProfileStore()
    {
        AppDataPath = LoadSettings().ProfilesPath;
    }

    public IReadOnlyList<Profile> LoadProfiles()
    {
        Directory.CreateDirectory(AppDataPath);

        if (!File.Exists(ProfilesPath))
        {
            SaveProfiles([]);
            return [];
        }

        var json = File.ReadAllText(ProfilesPath);
        var profiles = JsonSerializer.Deserialize<List<Profile>>(json, JsonOptions) ?? [];
        if (NormalizeProfileUrls(profiles))
        {
            SaveProfiles(profiles);
        }

        return profiles.OrderByDescending(profile => profile.LastUsedAt).ToList();
    }

    public Profile CreateProfile(string name, string startUrl)
    {
        var profiles = LoadProfiles().ToList();
        var profile = new Profile
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"Person {profiles.Count + 1}" : name.Trim(),
            StartUrl = NormalizeStartUrl(startUrl)
        };

        profiles.Add(profile);
        SaveProfiles(profiles);
        return profile;
    }

    public void MarkUsed(Profile selectedProfile)
    {
        var profiles = LoadProfiles().ToList();
        var profile = profiles.FirstOrDefault(item => item.Id == selectedProfile.Id);

        if (profile is null)
        {
            profiles.Add(selectedProfile);
            profile = selectedProfile;
        }

        profile.LastUsedAt = DateTimeOffset.UtcNow;
        SaveProfiles(profiles);
    }

    public void UpdateProfile(Profile selectedProfile, string name, string startUrl)
    {
        var trimmedName = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            return;
        }

        var profiles = LoadProfiles().ToList();
        var profile = profiles.FirstOrDefault(item => item.Id == selectedProfile.Id);

        if (profile is null)
        {
            return;
        }

        profile.Name = trimmedName;
        selectedProfile.Name = trimmedName;
        profile.StartUrl = NormalizeStartUrl(startUrl);
        selectedProfile.StartUrl = profile.StartUrl;
        SaveProfiles(profiles);
    }

    public void DeleteProfile(Profile selectedProfile)
    {
        var profiles = LoadProfiles()
            .Where(item => item.Id != selectedProfile.Id)
            .ToList();

        SaveProfiles(profiles);

        var profileFolder = GetProfileFolder(selectedProfile);
        if (Directory.Exists(profileFolder))
        {
            Directory.Delete(profileFolder, true);
        }
    }

    public void ChangeProfileFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return;
        }

        AppDataPath = folder.Trim();
        Directory.CreateDirectory(AppDataPath);

        if (!File.Exists(ProfilesPath))
        {
            SaveProfiles([]);
        }

        SaveSettings(new ProfileStoreSettings { ProfilesPath = AppDataPath });
    }

    public static string NormalizeStartUrl(string startUrl)
    {
        var trimmedUrl = startUrl.Trim();
        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return DefaultStartUrl;
        }

        return uri.ToString();
    }

    public string GetWebViewUserDataFolder(Profile profile)
    {
        var folder = Path.Combine(GetProfileFolder(profile), "webview2");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public string GetProfileFolder(Profile profile)
    {
        return Path.Combine(AppDataPath, profile.Id);
    }

    private void SaveProfiles(IReadOnlyCollection<Profile> profiles)
    {
        Directory.CreateDirectory(AppDataPath);
        var json = JsonSerializer.Serialize(profiles, JsonOptions);
        File.WriteAllText(ProfilesPath, json);

        foreach (var profile in profiles)
        {
            var profileFolder = GetProfileFolder(profile);
            Directory.CreateDirectory(profileFolder);
            File.WriteAllText(Path.Combine(profileFolder, "profile.json"), JsonSerializer.Serialize(profile, JsonOptions));
        }
    }

    private static bool NormalizeProfileUrls(IEnumerable<Profile> profiles)
    {
        var changed = false;

        foreach (var profile in profiles)
        {
            var normalizedUrl = NormalizeStartUrl(profile.StartUrl);
            if (profile.StartUrl != normalizedUrl)
            {
                profile.StartUrl = normalizedUrl;
                changed = true;
            }
        }

        return changed;
    }

    private ProfileStoreSettings LoadSettings()
    {
        Directory.CreateDirectory(settingsFolder);

        if (!File.Exists(SettingsPath))
        {
            var settings = new ProfileStoreSettings { ProfilesPath = DefaultProfilesPath };
            SaveSettings(settings);
            return settings;
        }

        var json = File.ReadAllText(SettingsPath);
        var loaded = JsonSerializer.Deserialize<ProfileStoreSettings>(json, JsonOptions);

        if (loaded is null || string.IsNullOrWhiteSpace(loaded.ProfilesPath))
        {
            return new ProfileStoreSettings { ProfilesPath = DefaultProfilesPath };
        }

        return loaded;
    }

    private void SaveSettings(ProfileStoreSettings settings)
    {
        Directory.CreateDirectory(settingsFolder);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private sealed class ProfileStoreSettings
    {
        public string ProfilesPath { get; set; } = ProfileStore.DefaultProfilesPath;
    }
}
