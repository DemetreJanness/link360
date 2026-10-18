namespace Link360Desktop;

// Both maintained branches use the same source; the build selects private data.
internal static class CommunityProfile {
#if COMMUNITY_REVIEW
 public const string Title = "LINK360 · Community review build";
 public const string Subtitle = "COMMUNITY REVIEW";
 public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LINK360-Community");
 public static string SettingsFile => Path.Combine(DataDirectory, "settings.local.json");
#else
 public const string Title = "LINK360 · Link Studio";
 public const string Subtitle = "LINK STUDIO";
 public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LINK360");
 public static string SettingsFile => Path.Combine(AppContext.BaseDirectory, "settings.local.json");
#endif
 public static string DiagnosticsDirectory => Path.Combine(DataDirectory, "diagnostics");
}
