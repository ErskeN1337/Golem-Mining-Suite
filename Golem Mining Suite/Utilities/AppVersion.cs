using System.Reflection;

namespace Golem_Mining_Suite.Utilities
{
    /// <summary>
    /// Single source of truth for the displayable app version.
    /// Reads <see cref="AssemblyInformationalVersionAttribute"/> (which honours the
    /// .csproj <c>&lt;Version&gt;</c> including pre-release tags like <c>1.4.0-beta</c>),
    /// strips the <c>+&lt;commit-sha&gt;</c> suffix MSBuild appends, and falls back to
    /// <c>AssemblyVersion</c> if the informational attribute is absent.
    /// </summary>
    public static class AppVersion
    {
        private static readonly string _display = ResolveDisplay();

        /// <summary>Returns e.g. <c>"v1.4.0-beta"</c>. Always non-empty.</summary>
        public static string Display => _display;

        private static string ResolveDisplay()
        {
            var assembly = Assembly.GetExecutingAssembly();

            var info = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                return "v" + (plus >= 0 ? info[..plus] : info);
            }

            var v = assembly.GetName().Version;
            return v == null ? "v1.0.0" : $"v{v.Major}.{v.Minor}.{v.Build}";
        }
    }
}
