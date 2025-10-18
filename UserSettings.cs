using System.Text;
using System.Globalization; // Added for consistent float formatting

namespace ArkServerManager
{
    /// <summary>
    /// Represents server-relevant settings typically found under the
    /// [/Script/ShooterGame.ShooterGameUserSettings] section in GameUserSettings.ini.
    /// Updated based on provided GameUserSettings.ini example and research.
    /// Note: Many settings here are client-side, but some influence server/player interaction.
    /// </summary>
    public class UserSettings
    {
        // --- Settings relevant to server admins/behavior, defaults from example INI ---
        public bool AllowThirdPersonPlayer { get; set; } = true; // Often set server-side too, default True
        public bool ShowMapPlayerLocation { get; set; } = true; // Default from example INI
        public bool ShowFloatingDamageText { get; set; } = false; // Default from example INI
        public bool bFloatingNames { get; set; } = true; // Default from example INI
        public bool bJoinNotifications { get; set; } = true; // Default from example INI
        public bool bHideFloatingPlayerNames { get; set; } = true; // Default from example INI
        public bool bChatBubbles { get; set; } = true; // Default from example INI
        public bool bPreventHitMarkers { get; set; } = false; // Default from example INI
        public bool bNoBloodEffects { get; set; } = true; // Default from example INI
        public bool bEnableInventoryItemTooltips { get; set; } = true; // Default from example INI

        // --- Settings moved from GameSettings to match example INI location ---
        public bool bDisableStructurePlacementCollision { get; set; } = true; // Default from example INI
        public bool bAllowUnlimitedRespecs { get; set; } = true; // Default from example INI
        public bool bAllowPlatformSaddleMultiFloors { get; set; } = true; // Default from example INI (Also works in Game.ini)
      //  public bool bAllowSpeedLeveling { get; set; } = true;
       // public bool bAllowFlyerSpeedLeveling { get; set; } = true;

        /// <summary>
        /// Generates the INI string segment for the [/Script/ShooterGame.ShooterGameUserSettings] section.
        /// </summary>
        /// <returns>A string containing settings in INI format for this section.</returns>
        public string ToIniString()
        {
            StringBuilder ini = new StringBuilder();
            ini.AppendLine("[/Script/ShooterGame.ShooterGameUserSettings]");

            foreach (var prop in GetType().GetProperties())
            {
                if (!prop.CanRead || prop.GetMethod == null || !prop.GetMethod.IsPublic) continue;
                var value = prop.GetValue(this);

                if (value is bool boolValue) { ini.AppendLine($"{prop.Name}={boolValue}"); }
                else if (value is float floatValue) { ini.AppendLine($"{prop.Name}={floatValue.ToString("0.###############", CultureInfo.InvariantCulture)}"); }
                else if (value is int intValue) { ini.AppendLine($"{prop.Name}={intValue}"); }
                else if (value is string stringValue) { ini.AppendLine($"{prop.Name}={stringValue ?? ""}"); }
            }
            return ini.ToString();
        }
    }
}