namespace Link360Desktop;

// Local review-build infrastructure. Keep this commit out of selective imports
// into the normal development build.
internal static class CommunityProfile {
 public const string Title = "LINK360 · Community review build";
 public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LINK360-Community");
 public static string SettingsFile => Path.Combine(DataDirectory, "settings.local.json");
 public static string DiagnosticsDirectory => Path.Combine(DataDirectory, "diagnostics");
}
