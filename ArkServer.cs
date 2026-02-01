using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization; // For JsonIgnore

namespace ArkServerManager
{
    /// <summary>
    /// Represents a single ARK: Survival Ascended server profile,
    /// including its configuration settings and runtime state.
    /// </summary>
    public class ArkServer : INotifyPropertyChanged
    {
        // --- Fields backing the properties ---
        // Use common defaults where applicable
        private string _name = "My ARK Server";
        private string _map = "TheIsland_WP"; // Default map identifier
        private int _playerLimit = 70;
        private string _installPath = ""; // Must be set per server
        private List<string> _mods = new List<string>();
        private GameSettings _gameSettings = new GameSettings();
        private UserSettings _userSettings = new UserSettings();
        private ServerSettings _serverSettings = new ServerSettings();
        private int _queryPort = 27015; // Default Steam query port
        private int _gamePort = 7777;   // Default UE game port
        private int _rconPort = 27020;  // Default RCON port
        private string _ipAddress = "0.0.0.0"; // Default: Bind to all available network interfaces
        private string _clusterId = "Cluster"; // Default Cluster ID
        private string _customArgs = ""; // Default empty
        // --- Public Properties ---

        /// <summary>
        /// The display name of the server profile. Also used for SessionName in INI.
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        /// <summary>
        /// The map identifier used for launching the server (e.g., "TheIsland_WP", "ScorchedEarth_WP").
        /// </summary>
        public string Map
        {
            get => _map;
            set => SetField(ref _map, value);
        }

        /// <summary>
        /// The maximum number of players allowed on the server.
        /// </summary>
        public int PlayerLimit
        {
            get => _playerLimit;
            set => SetField(ref _playerLimit, value);
        }

        /// <summary>
        /// The full path to the server's installation directory (containing ShooterGame folder).
        /// </summary>
        public string InstallPath
        {
            get => _installPath;
            set => SetField(ref _installPath, value);
        }

        /// <summary>
        /// A list of Mod IDs (as strings) to be loaded by the server.
        /// </summary>
        public List<string> Mods
        {
            get => _mods;
            set => SetField(ref _mods, value);
        }

        /// <summary>
        /// Settings typically found in Game.ini under [/Script/ShooterGame.ShooterGameMode].
        /// </summary>
        public GameSettings GameSettings
        {
            get => _gameSettings;
            set => SetField(ref _gameSettings, value);
        }

        /// <summary>
        /// Settings typically found in GameUserSettings.ini under [/Script/ShooterGame.ShooterGameUserSettings].
        /// </summary>
        public UserSettings UserSettings
        {
            get => _userSettings;
            set => SetField(ref _userSettings, value);
        }

        /// <summary>
        /// Settings typically found in GameUserSettings.ini under [ServerSettings].
        /// Also includes manager-specific settings like passwords and RCON toggle.
        /// </summary>
        public ServerSettings ServerSettings
        {
            get => _serverSettings;
            set => SetField(ref _serverSettings, value);
        }

        /// <summary>
        /// The UDP port used for Steam server browser queries.
        /// </summary>
        public int QueryPort
        {
            get => _queryPort;
            set => SetField(ref _queryPort, value);
        }

        /// <summary>
        /// The UDP port used for game client connections.
        /// </summary>
        public int GamePort
        {
            get => _gamePort;
            set => SetField(ref _gamePort, value);
        }

        /// <summary>
        /// The TCP port used for RCON connections.
        /// </summary>
        public int RconPort
        {
            get => _rconPort;
            set => SetField(ref _rconPort, value);
        }

        /// <summary>
        /// The specific IP address the server should bind to (-MultiHome parameter).
        /// "0.0.0.0" means bind to all available interfaces.
        /// </summary>
        public string IpAddress
        {
            get => _ipAddress;
            set
            {
                string validatedIp = "0.0.0.0"; // Default fallback
                if (!string.IsNullOrWhiteSpace(value))
                {
                    string trimmedValue = value.Trim();
                    // Allow "0.0.0.0" or valid IP formats
                    if (trimmedValue == "0.0.0.0" || System.Net.IPAddress.TryParse(trimmedValue, out _))
                    {
                        validatedIp = trimmedValue;
                    }
                    else
                    {
                        // Log invalid format attempt, keep existing value or default
                        Debug.WriteLine($"Invalid IP address format entered: '{value}'. Using previous/default value ('{_ipAddress ?? "0.0.0.0"}').");
                        validatedIp = _ipAddress ?? "0.0.0.0"; // Revert to current if invalid input
                    }
                }
                // Only update if the validated IP is different
                SetField(ref _ipAddress, validatedIp);
            }
        }

        /// <summary>
        /// The ID used to link servers together for cross-travel.
        /// </summary>
        public string ClusterId
        {
            get => _clusterId;
            set => SetField(ref _clusterId, value);
        }

        /// <summary>
        /// Custom launch arguments added manually by the user.
        /// </summary>
        public string CustomArgs
        {
            get => _customArgs;
            set => SetField(ref _customArgs, value);
        }

        /// <summary>
        /// A reference to the running server process. Null if not running.
        /// Marked with [JsonIgnore] so it's not saved in the profile JSON.
        /// </summary>
        [JsonIgnore]
        public Process RunningProcess { get; set; } = null; // Should be managed by ServerManager, not directly set here usually

        // --- INotifyPropertyChanged Implementation ---

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Helper method to set a field and raise the PropertyChanged event if the value changed.
        /// </summary>
        /// <typeparam name="T">The type of the property.</typeparam>
        /// <param name="field">A reference to the backing field.</param>
        /// <param name="value">The new value.</param>
        /// <param name="propertyName">The name of the property (automatically determined).</param>
        /// <returns>True if the value changed, false otherwise.</returns>
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            // Check if the value actually changed
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false; // No change, do nothing
            }

            field = value; // Update the backing field
            OnPropertyChanged(propertyName); // Raise the event
            return true; // Indicate that the value changed
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The name of the property that changed.</param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // Use ?. operator for thread safety (in case handler is removed concurrently)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}