using System.Text;
using System.Globalization; // Added for consistent float formatting

namespace ArkServerManager
{
    /// <summary>
    /// Represents settings typically found in the GameUserSettings.ini file
    /// under the [ServerSettings] section.
    /// Defaults updated based on provided GameUserSettings.ini example and research.
    /// </summary>
    public class ServerSettings
    {
        #region Difficulty Settings
        public float DifficultyOffset { get; set; } = 0.200000003f; // From INI
        public float OverrideOfficialDifficulty { get; set; } = 5.0f; // Default (Not in INI example's [ServerSettings], but standard)
        #endregion

        #region Time Settings
        public float DayCycleSpeedScale { get; set; } = 0.999989986f; // From INI
        public float DayTimeSpeedScale { get; set; } = 0.999989986f; // From INI
        public float NightTimeSpeedScale { get; set; } = 0.999989986f; // From INI
        public bool StartTimeOverride { get; set; } = false; // From INI
        public float StartTimeHour { get; set; } = 10.0000095f; // From INI
        public bool OverrideStartTime { get; set; } = false; // From INI
        #endregion

        #region Multipliers
        public float DinoDamageMultiplier { get; set; } = 0.999989986f; // From INI
        public float PlayerDamageMultiplier { get; set; } = 0.999989986f; // From INI
        public float StructureDamageMultiplier { get; set; } = 0.999989986f; // From INI
        public float PlayerResistanceMultiplier { get; set; } = 0.999989986f; // From INI
        public float DinoResistanceMultiplier { get; set; } = 0.999989986f; // From INI
        public float StructureResistanceMultiplier { get; set; } = 0.999989986f; // From INI
        public float XPMultiplier { get; set; } = 0.999989986f; // From INI
        public float TamingSpeedMultiplier { get; set; } = 0.999989986f; // From INI
        public float HarvestAmountMultiplier { get; set; } = 0.999989986f; // From INI
        public float HarvestHealthMultiplier { get; set; } = 0.999989986f; // From INI
        public float ResourcesRespawnPeriodMultiplier { get; set; } = 1.0f; // Added - Default 1.0 (Not in example INI)
        public float PvEDinoDecayPeriodMultiplier { get; set; } = 0.999989986f; // From INI
        public float PvEStructureDecayPeriodMultiplier { get; set; } = 0.999989986f; // From INI
        public float CraftingSkillBonusMultiplier { get; set; } = 1f; // From INI
        public float FishingLootQualityMultiplier { get; set; } = 1f; // Added from Game.ini example (belongs here)
        public float SupplyCrateLootQualityMultiplier { get; set; } = 1f; // From INI
        public float ItemStackSizeMultiplier { get; set; } = 1f; // From INI
        public float OxygenSwimSpeedStatMultiplier { get; set; } = 1f; // From INI
        public float PlatformSaddleBuildAreaBoundsMultiplier { get; set; } = 1f; // From INI
        public float PerPlatformMaxStructuresMultiplier { get; set; } = 1f; // From INI
        public float ListenServerTetherDistanceMultiplier { get; set; } = 1f; // From INI
        public float RaidDinoCharacterFoodDrainMultiplier { get; set; } = 0.999989986f; // From INI
        public float StructurePreventResourceRadiusMultiplier { get; set; } = 0.999989986f; // From INI
        #endregion

        #region Dino and Structure Settings
        public bool AllowRaidDinoFeeding { get; set; } = false; // From INI
        public float TheMaxStructuresInRange { get; set; } = 10500f; // From INI
        public bool ForceAllowCaveFlyers { get; set; } = false; // From INI
        public bool DisableDinoDecayPvE { get; set; } = false; // From INI
        public bool AllowCaveBuildingPvE { get; set; } = false; // From INI
        public bool OverrideStructurePlatformPrevention { get; set; } = false; // From INI
        public bool PvPDinoDecay { get; set; } = false; // From INI
        public bool DisableStructureDecayPvE { get; set; } = false; // From INI
        public bool AllowFlyerCarryPvE { get; set; } = false; // From INI
        #endregion

        #region Gameplay Toggles
        public bool ServerHardcore { get; set; } = false; // From INI
        public bool ServerPVE { get; set; } = false; // From INI
        public bool ServerCrosshair { get; set; } = true; // From INI
        public bool ServerForceNoHUD { get; set; } = false; // From INI
        public bool EnablePvPGamma { get; set; } = true; // From INI
        public bool DisablePvEGamma { get; set; } = false; // From INI
        public bool AllowHitMarkers { get; set; } = true; // From INI
        public bool AllowThirdPersonPlayer { get; set; } = true; // Belongs in UserSettings, but often mirrored here. Default True.
        public bool ShowFloatingDamageText { get; set; } = false; // Belongs in UserSettings, but often mirrored here. Default False.
        public bool ShowMapPlayerLocation { get; set; } = true; // Belongs in UserSettings, but often mirrored here. Default True.
        public bool OnlyAllowSpecifiedEngrams { get; set; } = false; // From INI
        public bool DisableWeatherFog { get; set; } = false; // From INI
        public bool RandomSupplyCratePoints { get; set; } = false; // From INI
        public bool PreventDiseases { get; set; } = false; // From INI
        public bool NonPermanentDiseases { get; set; } = false; // From INI
        public bool bAllowSpeedLeveling { get; set; } = true; // From INI
        public bool bAllowFlyerSpeedLeveling { get; set; } = true; // From INI
        public bool AllowAnyoneBabyImprintCuddle { get; set; } = false; // From INI
        public bool DisableImprintDinoBuff { get; set; } = false; // From INI
        public bool EnableCryoSicknessPVE { get; set; } = false; // Added - Default False.
        #endregion

        #region Chat and Notifications
        public bool GlobalVoiceChat { get; set; } = false; // From INI
        public bool ProximityChat { get; set; } = false; // From INI
        public bool AlwaysNotifyPlayerLeft { get; set; } = false; // From INI
        public bool DontAlwaysNotifyPlayerJoined { get; set; } = false; // From INI
        public bool AdminLogging { get; set; } = false; // From INI
        public bool AllowHideDamageSourceFromLogs { get; set; } = true; // From INI
        #endregion

        #region Downloads and Transfers
        public bool NoTributeDownloads { get; set; } = false; // From INI
        public bool PreventDownloadSurvivors { get; set; } = false; // From INI
        public bool PreventDownloadItems { get; set; } = false; // From INI
        public bool PreventDownloadDinos { get; set; } = false; // From INI
        #endregion

        #region PvP Settings
        public bool PreventOfflinePvP { get; set; } = false; // From INI
        public float PreventOfflinePvPInterval { get; set; } = 0.0f; // From INI (-0 -> 0.0f)
        #endregion

        #region Tribe Settings
        public float TribeNameChangeCooldown { get; set; } = 15f; // From INI
        public bool PreventTribeAlliances { get; set; } = false; // From INI
        #endregion

        #region Structure Pickup
        public bool AlwaysAllowStructurePickup { get; set; } = true; // From INI
        public float StructurePickupTimeAfterPlacement { get; set; } = 30f; // From INI
        public float StructurePickupHoldDuration { get; set; } = 0.5f; // From INI
        #endregion

        #region Server Management
        public float KickIdlePlayersPeriod { get; set; } = 3600f; // From INI
        public float AutoSavePeriodMinutes { get; set; } = 15f; // From INI
        public int MaxTamedDinos { get; set; } = 5000; // From INI
        public int MaxTamedDinos_SoftTameLimit { get; set; } = 5000; // From INI
        public int MaxTamedDinos_SoftTameLimit_CountdownForDeletionDuration { get; set; } = 604800; // From INI
        public int OverrideSecondsUntilBuriedTreasureAutoReveals { get; set; } = 1209600; // From INI
        public int RCONServerGameLogBuffer { get; set; } = 600; // From INI
        public int ImplantSuicideCD { get; set; } = 28800; // From INI
        // MaxPersonalTamedDinos = 0; // Potential useful addition
        #endregion

        #region Miscellaneous
        public string ActiveEvent { get; set; } = ""; // From INI
        public string ActiveMods { get; set; } = ""; // From INI (Handled by launch args)
        public float ActiveMapMod { get; set; } = 0f; // From INI
        public bool ForceGachaUnhappyInCaves { get; set; } = false; // From INI
        public bool EnableExtraStructurePreventionVolumes { get; set; } = false; // From INI
        // AllowSharedConnections=False // Potential useful addition
        // UseOptimizedHarvestingHealth=True // Potential useful addition
        // AllowCrateSpawnsOnTopOfStructures=False // Potential useful addition
        // PreventSpawnAnimations=False // Potential useful addition
        // FastDecayInterval=1.0 // Potential useful addition
        // bAllowPlatformSaddleMovingFoundation=False // Potential useful addition

        #endregion

        #region Manager Specific / Not typically in [ServerSettings]
        public bool RCONEnabled { get; set; } = true; // Defaulting to true for management convenience
        public string ServerAdminPassword { get; set; } = "changeme"; // Default insecure password - user should change
        public string ServerPassword { get; set; } = ""; // Default no password
        #endregion

        /// <summary>
        /// Generates the INI string segment for the [ServerSettings] section.
        /// Does NOT include other sections like [SessionSettings] or passwords.
        /// </summary>
        /// <returns>A string containing settings in INI format for the [ServerSettings] section.</returns>
        public string ToIniString()
        {
            StringBuilder ini = new StringBuilder();
            ini.AppendLine("[ServerSettings]");

            foreach (var prop in GetType().GetProperties())
            {
                // Skip properties not meant for the [ServerSettings] section directly
                if (prop.Name == nameof(RCONEnabled) ||
                    prop.Name == nameof(ServerAdminPassword) ||
                    prop.Name == nameof(ServerPassword) ||
                    prop.Name == nameof(ActiveMods)) // ActiveMods is handled via launch args
                {
                    continue;
                }

                // Skip properties that primarily belong in UserSettings, even if sometimes mirrored here
                if (prop.Name == nameof(AllowThirdPersonPlayer) ||
                    prop.Name == nameof(ShowMapPlayerLocation) ||
                    prop.Name == nameof(ShowFloatingDamageText))
                {
                    continue;
                }

                // Skip properties confirmed to belong ONLY in Game.ini
                // (List based on previous analysis - adjust if needed)
                if (prop.Name == "DinoCharacterFoodDrainMultiplier" || // Check name carefully
                    prop.Name == "DinoCharacterStaminaDrainMultiplier" ||
                    prop.Name == "DinoCharacterHealthRecoveryMultiplier" ||
                    prop.Name == "PlayerCharacterWaterDrainMultiplier" ||
                    prop.Name == "PlayerCharacterFoodDrainMultiplier" ||
                    prop.Name == "PlayerCharacterStaminaDrainMultiplier" ||
                    prop.Name == "PlayerCharacterHealthRecoveryMultiplier"
                    /* Add other Game.ini only settings here if they accidentally got added to this class */
                    )
                {
                    continue;
                }


                var value = prop.GetValue(this);
                if (value is bool boolValue)
                {
                    ini.AppendLine($"{prop.Name}={boolValue}");
                }
                else if (value is float floatValue)
                {
                    ini.AppendLine($"{prop.Name}={floatValue.ToString("0.###############", CultureInfo.InvariantCulture)}");
                }
                else if (value is int intValue)
                {
                    ini.AppendLine($"{prop.Name}={intValue}");
                }
                else if (value is string stringValue)
                {
                    ini.AppendLine($"{prop.Name}={stringValue ?? ""}");
                }
            }
            return ini.ToString();
        }
    }
}