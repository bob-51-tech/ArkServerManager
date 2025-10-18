using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization; // Added for consistent float formatting

namespace ArkServerManager
{
    /// <summary>
    /// Represents settings typically found in the Game.ini file under the
    /// [/Script/ShooterGame.ShooterGameMode] section.
    /// </summary>
    public class GameSettings
    {
        // IMPORTANT: The order and number of elements in the PerLevelStatsMultiplier arrays
        // MUST match the game's expected indices:
        // Player (12 stats): 0:Health, 1:Stamina, 2:Torpidity, 3:Oxygen, 4:Food, 5:Water, 6:Temperature, 7:Weight, 8:MeleeDamage, 9:Speed, 10:Fortitude, 11:CraftingSpeed
        // Dinos (11 stats):  0:Health, 1:Stamina, 2:Torpidity, 3:Oxygen, 4:Food, 5:Water, 6:Temperature, 7:Weight, 8:MeleeDamage, 9:Speed, 10:Fortitude (No CraftingSpeed)

        #region Per-Level Stat Multipliers
        // Note: These arrays control the *gain* per level point spent (or assigned for wild).

        /// <summary>Player Per-Level Stat Multipliers (Index corresponds to game stat order).</summary>
        public float[] PerLevelStatsMultiplierPlayer { get; set; } = new float[12]
        {
            1.0f, // Health
            1.0f, // Stamina
            1.0f, // Torpidity
            1.0f, // Oxygen
            1.0f, // Food
            1.0f, // Water
            1.0f, // Temperature (Usually not levelable)
            1.0f, // Weight
            1.0f, // MeleeDamage
            1.0f, // Speed
            1.0f, // Fortitude
            1.0f  // CraftingSpeed
        };

        /// <summary>Wild Dino Per-Level Stat Multipliers (Index corresponds to game stat order).</summary>
        public float[] PerLevelStatsMultiplierDinoWild { get; set; } = new float[11]
        {
            1.0f, // Health
            1.0f, // Stamina
            1.0f, // Torpidity
            1.0f, // Oxygen
            1.0f, // Food
            1.0f, // Water
            1.0f, // Temperature (Usually not levelable)
            1.0f, // Weight
            1.0f, // MeleeDamage
            1.0f, // Speed
            1.0f  // Fortitude
        };

        /// <summary>Tamed Dino Per-Level Stat Multipliers (Base Gain) (Index corresponds to game stat order).</summary>
        public float[] PerLevelStatsMultiplierDinoTamed { get; set; } = new float[11]
        {
            0.2f,   // Health       (Example: Vanilla default)
            1.0f,   // Stamina
            1.0f,   // Torpidity
            1.0f,   // Oxygen
            1.0f,   // Food
            1.0f,   // Water
            1.0f,   // Temperature
            1.0f,   // Weight
            0.17f,  // MeleeDamage  (Example: Vanilla default for Damage stat index 8)
            1.0f,   // Speed
            1.0f    // Fortitude
         };

        /// <summary>Tamed Dino Per-Level Stat Multipliers (Additive Bonus) (Index corresponds to game stat order).</summary>
        public float[] PerLevelStatsMultiplierDinoTamed_Add { get; set; } = new float[11]
        {
            0.147f, // Health       (Example: Value from your original code, likely post-tame bonus)
            1.0f,   // Stamina
            1.0f,   // Torpidity
            1.0f,   // Oxygen
            1.0f,   // Food
            1.0f,   // Water
            1.0f,   // Temperature
            1.0f,   // Weight
            0.147f, // MeleeDamage  (Example: Value from your original code)
            1.0f,   // Speed
            1.0f    // Fortitude
        };

        /// <summary>Tamed Dino Per-Level Stat Multipliers (Imprint Bonus Multiplier) (Index corresponds to game stat order).</summary>
        public float[] PerLevelStatsMultiplierDinoTamed_Affinity { get; set; } = new float[11]
        {
             0.44f, // Health       (Example: Multiplier applied by imprinting)
             1.0f,  // Stamina
             1.0f,  // Torpidity
             1.0f,  // Oxygen
             1.0f,  // Food
             1.0f,  // Water
             1.0f,  // Temperature
             1.0f,  // Weight
             0.44f, // MeleeDamage
             1.0f,  // Speed
             1.0f   // Fortitude
        };
        #endregion

        #region Character Stat Drain/Recovery Multipliers (Defaults from previous code - Not in example Game.ini)
        // Keep defaults as 1.0 unless specific values are desired
        public float DinoCharacterFoodDrainMultiplier { get; set; } = 1.0f;
        public float DinoCharacterStaminaDrainMultiplier { get; set; } = 1.0f;
        public float DinoCharacterHealthRecoveryMultiplier { get; set; } = 1.0f;
        public float PlayerCharacterWaterDrainMultiplier { get; set; } = 1.0f;
        public float PlayerCharacterFoodDrainMultiplier { get; set; } = 1.0f;
        public float PlayerCharacterStaminaDrainMultiplier { get; set; } = 1.0f;
        public float PlayerCharacterHealthRecoveryMultiplier { get; set; } = 1.0f;
        #endregion

        // Removed Non-Standard Drain/Damage Multipliers - Clean up

        #region Breeding and Maturation Multipliers (Defaults from example Game.ini)
        public float MatingIntervalMultiplier { get; set; } = 0.999989986f;
        public float EggHatchSpeedMultiplier { get; set; } = 0.999989986f;
        public float BabyMatureSpeedMultiplier { get; set; } = 0.999989986f;
        public float BabyFoodConsumptionSpeedMultiplier { get; set; } = 0.999989986f;
        public float LayEggIntervalMultiplier { get; set; } = 0.999989986f;
        public float PoopIntervalMultiplier { get; set; } = 0.999989986f;
        #endregion

        #region Imprinting Multipliers (Defaults from example Game.ini)
        public float BabyImprintAmountMultiplier { get; set; } = 1.0f; // Default if not overridden
        public float BabyImprintingStatScaleMultiplier { get; set; } = 0.999989986f;
        public float BabyCuddleIntervalMultiplier { get; set; } = 0.999989986f;
        public float BabyCuddleGracePeriodMultiplier { get; set; } = 0.999989986f;
        public float BabyCuddleLoseImprintQualitySpeedMultiplier { get; set; } = 0.999989986f;
        #endregion

        #region Resource, Spoilage, and Decay Multipliers (Defaults from example Game.ini)
        public float GlobalSpoilingTimeMultiplier { get; set; } = 0f;
        public float GlobalItemDecompositionTimeMultiplier { get; set; } = 0f;
        public float GlobalCorpseDecompositionTimeMultiplier { get; set; } = 6f;
        public float CropGrowthSpeedMultiplier { get; set; } = 0.999989986f;
        public float CropDecaySpeedMultiplier { get; set; } = 0.999989986f;
        public float FuelConsumptionIntervalMultiplier { get; set; } = 1f;
        public float ResourceNoReplenishRadiusPlayers { get; set; } = 0.999989986f;
        public float ResourceNoReplenishRadiusStructures { get; set; } = 0.999989986f;
        // ResourcesRespawnPeriodMultiplier moved to ServerSettings
        #endregion

        #region Harvesting Multipliers (Defaults from example Game.ini)
        // HarvestAmountMultiplier & HarvestHealthMultiplier moved to ServerSettings
        public float PlayerHarvestingDamageMultiplier { get; set; } = 0.999989986f;
        public float DinoHarvestingDamageMultiplier { get; set; } = 3.19999003f;
        // SupplyCrateLootQualityMultiplier & FishingLootQualityMultiplier moved to ServerSettings
        #endregion

        #region XP Multipliers (Defaults from example Game.ini)
        // XPMultiplier (global) moved to ServerSettings
        public float KillXPMultiplier { get; set; } = 0.999989986f;
        public float HarvestXPMultiplier { get; set; } = 0.999989986f;
        public float CraftXPMultiplier { get; set; } = 0.999989986f;
        public float GenericXPMultiplier { get; set; } = 0.999989986f;
        public float SpecialXPMultiplier { get; set; } = 0.999989986f;
        public float ExplorerNoteXPMultiplier { get; set; } = 0.999989986f;
        public float BossKillXPMultiplier { get; set; } = 0.999989986f;
        public float AlphaKillXPMultiplier { get; set; } = 0.999989986f;
        public float WildKillXPMultiplier { get; set; } = 0.999989986f;
        public float CaveKillXPMultiplier { get; set; } = 0.999989986f;
        public float TamedKillXPMultiplier { get; set; } = 0.999989986f;
        public float UnclaimedKillXPMultiplier { get; set; } = 0.999989986f;
        #endregion

        #region PvP and Structure Settings (Defaults from example Game.ini)
        public float PvPZoneStructureDamageMultiplier { get; set; } = 6f;
        public int StructureDamageRepairCooldown { get; set; } = 180; // Int is fine
        public bool bIncreasePvPRespawnInterval { get; set; } = true;
        public int IncreasePvPRespawnIntervalCheckPeriod { get; set; } = 300; // Int is fine
        public float IncreasePvPRespawnIntervalMultiplier { get; set; } = 1.99998999f;
        public int IncreasePvPRespawnIntervalBaseAmount { get; set; } = 60; // Int is fine
        public bool bForceAllStructureLocking { get; set; } = false;
        // bDisableStructurePlacementCollision moved to UserSettings
        // bAllowPlatformSaddleMultiFloors moved to UserSettings (but default here was false from INI)
        public bool bHardLimitTurretsInRange { get; set; } = true;
        public float LimitTurretsRange { get; set; } = 10000.0f; // Keeping C# default
        public int LimitTurretsNum { get; set; } = 100; // Keeping C# default
        public float DinoTurretDamageMultiplier { get; set; } = 0.999989986f;
        #endregion

        #region Miscellaneous Gameplay Settings (Defaults from example Game.ini where available)
        public bool MaxDifficulty { get; set; } = false;
        // !! IMPORTANT: bUseSingleplayerSettings=True heavily modifies other multipliers in-game !!
        // !! Set to False for predictable dedicated server behavior based purely on INI values !!
        public bool bUseSingleplayerSettings { get; set; } = true; // Default from INI example
        public bool bUseCorpseLocator { get; set; } = true;
        public bool bDisableFriendlyFire { get; set; } = false;
        public bool bAllowCustomRecipes { get; set; } = true;
        public float CustomRecipeEffectivenessMultiplier { get; set; } = 0.999989986f;
        public float CustomRecipeSkillMultiplier { get; set; } = 0.999989986f;
        // CraftingSkillBonusMultiplier moved to ServerSettings
        public bool bDisableLootCrates { get; set; } = false;
        public bool bFlyerPlatformAllowUnalignedDinoBasing { get; set; } = false;
        public bool bPassiveDefensesDamageRiderlessDinos { get; set; } = false;
        public bool bPvEAllowTribeWar { get; set; } = true;
        public bool bPvEAllowTribeWarCancel { get; set; } = false;
        public bool bAutoPvETimer { get; set; } = false;
        public bool bAutoPvEUseSystemTime { get; set; } = false;
        public int AutoPvEStartTimeSeconds { get; set; } = 0;
        public int AutoPvEStopTimeSeconds { get; set; } = 0;

        // bAllowUnlimitedRespecs moved to UserSettings (default here was false from INI)
        public bool bAllowSpeedLeveling { get; set; } = true;
        public bool bAllowFlyerSpeedLeveling { get; set; } = true;
        public bool bDisableDinoRiding { get; set; } = false;
        public bool bDisableDinoTaming { get; set; } = false;
        public bool bAllowTekSuitPowersInCaves { get; set; } = true; // Keep C# default
        public bool bAutoUnlockAllEngrams { get; set; } = false; // Keep C# default
        public float OverrideMaxExperiencePointsPlayer { get; set; } = 0f;
        public float OverrideMaxExperiencePointsDino { get; set; } = 0f;
        public int MaxNumberOfPlayersInTribe { get; set; } = 0;
        public bool bShowCreativeMode { get; set; } = false;
        public float PhotoModeRangeLimit { get; set; } = 3000f;
        public bool bDisablePhotoMode { get; set; } = false;

        // --- Added Extra Useful Settings ---
        public bool bDisableCryopodFridgeRequirement { get; set; } = false;
        public bool ClampResourceHarvestDamage { get; set; } = false;
        public int MaxTribeLogs { get; set; } = 100;
        public bool bAllowUnclaimDinos { get; set; } = false;
        public bool bDisableGenericNameChecking { get; set; } = false;
        #endregion


        /// <summary>
        /// Generates the INI string for the [/Script/ShooterGame.ShooterGameMode] section.
        /// Filters out properties that belong in GameUserSettings.ini.
        /// </summary>
        /// <returns>A string containing settings in INI format for this section.</returns>
        public string ToIniString()
        {
            var ini = new StringBuilder();
            ini.AppendLine("[/Script/ShooterGame.ShooterGameMode]");

            Action<string, float[]> addFloatArray = (propName, arr) =>
            {
                if (arr == null) return;
                for (int i = 0; i < arr.Length; i++)
                {
                    ini.AppendLine($"{propName}[{i}]={arr[i].ToString("0.###############", CultureInfo.InvariantCulture)}");
                }
            };

            addFloatArray("PerLevelStatsMultiplier_Player", PerLevelStatsMultiplierPlayer);
            addFloatArray("PerLevelStatsMultiplier_DinoWild", PerLevelStatsMultiplierDinoWild);
            addFloatArray("PerLevelStatsMultiplier_DinoTamed", PerLevelStatsMultiplierDinoTamed);
            addFloatArray("PerLevelStatsMultiplier_DinoTamed_Add", PerLevelStatsMultiplierDinoTamed_Add);
            addFloatArray("PerLevelStatsMultiplier_DinoTamed_Affinity", PerLevelStatsMultiplierDinoTamed_Affinity);

            foreach (var prop in GetType().GetProperties())
            {
                if (prop.PropertyType.IsArray) continue;

                string propName = prop.Name;

                // Filter out properties confirmed to belong in GameUserSettings.ini
                // Using string literals for robustness.
                if (propName == "TamingSpeedMultiplier" ||
                    propName == "HarvestAmountMultiplier" ||
                    propName == "HarvestHealthMultiplier" ||
                    propName == "XPMultiplier" ||
                    propName == "CraftingSkillBonusMultiplier" ||
                    propName == "SupplyCrateLootQualityMultiplier" ||
                    propName == "FishingLootQualityMultiplier" || // Moved
                    propName == "ResourcesRespawnPeriodMultiplier" || // Moved
                   // propName == "bAllowSpeedLeveling" ||
                    //propName == "bAllowFlyerSpeedLeveling" ||
                    propName == "bOnlyAllowSpecifiedEngrams" ||
                    propName == "bDisableDinoDecayPvE" ||
                    propName == "PvEDinoDecayPeriodMultiplier" ||
                    propName == "bAllowRaidDinoFeeding" ||
                    propName == "RaidDinoFoodDrainMultiplier" || // Check ServerSettings name
                    propName == "bDisableWeatherFog" ||
                    propName == "EnableCryoSicknessPVE" || // Moved
                    propName == "bDisableStructurePlacementCollision" || // Moved to UserSettings
                    propName == "bAllowUnlimitedRespecs" || // Moved to UserSettings
                    propName == "bAllowPlatformSaddleMultiFloors") // Moved to UserSettings (although works here too)
                {
                    continue;
                }

                // Filter non-standard properties if desired
                // if (propName == nameof(TamedDinoCharacterFoodDrainMultiplier) || ...) { continue; }

                var value = prop.GetValue(this);
                if (value is bool boolValue)
                {
                    ini.AppendLine($"{propName}={boolValue}");
                }
                else if (value is float floatValue)
                {
                    ini.AppendLine($"{propName}={floatValue.ToString("0.###############", CultureInfo.InvariantCulture)}");
                }
                else if (value is int intValue)
                {
                    ini.AppendLine($"{propName}={intValue}");
                }
                else if (value is string stringValue)
                {
                    ini.AppendLine($"{propName}={stringValue ?? ""}");
                }
            }
            return ini.ToString();
        }
    }
}