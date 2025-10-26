using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net; // For IPAddress parsing
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Globalization;

namespace ArkServerManager
{
    /// <summary>
    /// Manages ARK server instances, including loading/saving profiles,
    /// launching, terminating, updating, and interacting via RCON (placeholder).
    /// </summary>
    public class ServerManager
    {
        private readonly string _appDirectory;
        private readonly string _steamCmdPath;
        private readonly string _profilesFilePath;
        private readonly string _serversBaseDirectory;
        private readonly TextBox _consoleOutput;
        private readonly Dispatcher _dispatcher; // UI thread dispatcher

        private List<ArkServer> _servers;
        private ArkServer _currentServer;
        private Process _serverProcess;
        private Process _steamCmdProcess; // Ensure only one SteamCMD runs at a time

        public ArkServer CurrentServer => _currentServer;
        public ServerManager(TextBox consoleOutput)
        {
            _consoleOutput = consoleOutput ?? throw new ArgumentNullException(nameof(consoleOutput));
            _dispatcher = consoleOutput.Dispatcher; // Capture dispatcher from the UI element

            // Determine application paths
            _appDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Environment.CurrentDirectory;
            string toolsDir = Path.Combine(_appDirectory, "Tools");
            Directory.CreateDirectory(toolsDir); // Ensure Tools directory exists
            _steamCmdPath = Path.Combine(toolsDir, "steamcmd.exe");

            string dataDir = Path.Combine(_appDirectory, "Data");
            Directory.CreateDirectory(dataDir); // Ensure Data directory exists
            _profilesFilePath = Path.Combine(dataDir, "server_profiles.json");

            _serversBaseDirectory = Path.Combine(_appDirectory, "Servers"); // Base dir for server installs

            _servers = new List<ArkServer>();

            // Initial logging
            LogOutput($"Application Directory: {_appDirectory}");
            LogOutput($"SteamCMD Path: {_steamCmdPath}");
            LogOutput($"Server Profiles Path: {_profilesFilePath}");
            LogOutput($"Servers Base Directory: {_serversBaseDirectory}");
        }

        /// <summary>
        /// Performs initial setup, like ensuring SteamCMD exists.
        /// </summary>
        public async Task InitializeAsync()
        {
            await EnsureSteamCmdExists();
        }

        #region Server Lifecycle Management

        /// <summary>
        /// Launches the currently selected server.
        /// </summary>
        public async Task LaunchServer()
        {
            if (_currentServer == null)
            {
                LogOutput("ERROR: No server selected. Cannot launch.");
                return;
            }

            if (_serverProcess != null && !_serverProcess.HasExited)
            {
                LogOutput($"Server '{_currentServer.Name}' is already running (PID: {_serverProcess.Id}).");
                return;
            }

            // Ensure latest settings are saved before launch
            SaveServerProfiles(); // Save all profiles just in case basic settings changed
            LogOutput($"Preparing to launch server '{_currentServer.Name}'...");

            // 1. Generate INI Files
            if (!await GenerateIniFilesAsync(_currentServer))
            {
                return; // Error already logged in GenerateIniFilesAsync
            }

            // 2. Check Server Executable Exists (and optionally validate/update)
            string serverExePath = Path.Combine(_currentServer.InstallPath, "ShooterGame", "Binaries", "Win64", "ArkAscendedServer.exe");
            if (!File.Exists(serverExePath))
            {
                LogOutput($"Server executable not found at '{serverExePath}'. Attempting validation/update...");
                bool setupSuccess = await SetupServerDependencies(_currentServer);
                if (!setupSuccess || !File.Exists(serverExePath))
                {
                    LogOutput($"ERROR: Server executable missing or validation failed for '{_currentServer.Name}'. Cannot launch.");
                    return;
                }
                LogOutput("Server executable found after validation.");
            }

            // 3. Construct Launch Arguments
            string arguments = BuildLaunchArguments(_currentServer);
            LogOutput($"Server Launch Arguments: {arguments}");

            // 4. Start the Process
            try
            {
                _serverProcess = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = serverExePath,
                        Arguments = arguments,
                        WorkingDirectory = Path.GetDirectoryName(serverExePath), // Crucial for the server finding its files
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false, // Required for redirection
                        CreateNoWindow = true,   // Run in background
                        StandardOutputEncoding = Encoding.UTF8, // Handle various characters
                        StandardErrorEncoding = Encoding.UTF8
                    },
                    EnableRaisingEvents = true // Needed for Exited event
                };

                // Attach event handlers BEFORE starting
                _serverProcess.OutputDataReceived += ServerProcess_OutputDataReceived;
                _serverProcess.ErrorDataReceived += ServerProcess_ErrorDataReceived;
                _serverProcess.Exited += ServerProcess_Exited;

                bool started = _serverProcess.Start();

                if (started)
                {
                    _serverProcess.BeginOutputReadLine();
                    _serverProcess.BeginErrorReadLine();
                    _currentServer.RunningProcess = _serverProcess;
                    LogOutput($"Server '{_currentServer.Name}' process started successfully (PID: {_serverProcess.Id}).");
                    // Update UI state via dispatcher
                    _dispatcher.InvokeAsync(() => (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection());
                }
                else
                {
                    LogOutput($"ERROR: Process.Start() returned false for '{_currentServer.Name}'. Launch failed.");
                    _serverProcess.Dispose(); // Clean up the failed process object
                    _serverProcess = null;

                    if (_currentServer != null)
                    {
                        _currentServer.RunningProcess = null;
                    }
                    _dispatcher.InvokeAsync(() => (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection()); // Update UI on failure too
                }
            }
            catch (Exception ex)
            {
                LogOutput($"FATAL LAUNCH ERROR for '{_currentServer.Name}': {ex.Message}");
                LogOutput($"Details: {ex.StackTrace}"); // More detail for debugging
                _serverProcess?.Dispose(); // Ensure disposal even on exception
                _serverProcess = null;
                // Update UI state via dispatcher
                if (_currentServer != null)
                {
                    _currentServer.RunningProcess = null;
                }
                _dispatcher.InvokeAsync(() => (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection()); // Update UI on exception
            }
        }

        /// <summary>
        /// Forcefully terminates the currently running server process.
        /// </summary>
        public void TerminateServer()
        {
            if (_serverProcess == null || _serverProcess.HasExited)
            {
                LogOutput("No server process appears to be running.");
                // Ensure UI reflects this state potentially, even if called erroneously
                _dispatcher.InvokeAsync(() => (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection());
                return;
            }

            // Capture info before potential disposal in Exited event
            Process processToKill = _serverProcess;
            string serverName = _currentServer?.Name ?? "Unknown Server";
            int processId = -1;
            try { processId = processToKill.Id; } catch { /* Ignore error getting ID if process is weird */ }

            LogOutput($"Attempting to terminate server '{serverName}' (PID: {processId})...");

            try
            {
                processToKill.Kill();
                LogOutput($"Kill signal sent to process PID: {processId}. Waiting for Exited event...");
                // Don't nullify _serverProcess here; let the Exited event handle it cleanly.
            }
            catch (InvalidOperationException)
            {
                LogOutput($"WARN: Process (PID: {processId}) may have already exited before Kill() was called.");
                if (_serverProcess == processToKill)
                {
                    _serverProcess?.Dispose();
                    _serverProcess = null;
                    LogOutput("Manually cleared server process reference after Kill failed (already exited?).");
                }
            }
            catch (Exception ex)
            {
                LogOutput($"ERROR terminating server '{serverName}' (PID: {processId}): {ex.Message}");
                if (_serverProcess == processToKill)
                {
                    _serverProcess?.Dispose();
                    _serverProcess = null;
                    LogOutput("Manually cleared server process reference after Kill exception.");
                }
            }
            finally
            {
                // Ensure UI updates regardless of kill success/failure
                _dispatcher.InvokeAsync(() => (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection());
            }
        }

        // --- Process Event Handlers ---

        private void ServerProcess_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null) // Avoid logging null lines often sent when streams close
            {
                LogOutput($"[{_currentServer?.Name ?? "Server"}] {e.Data}");
            }
        }

        private void ServerProcess_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                LogOutput($"[{_currentServer?.Name ?? "Server"} ERR] {e.Data}");
            }
        }

        private void ServerProcess_Exited(object sender, EventArgs e)
        {
            Process exitedProcess = sender as Process;
            int exitCode = -999; // Default for unknown exit code
            string serverName = "Unknown Server"; // Default name
            int processId = exitedProcess?.Id ?? -1; // Get PID if possible

            try
            {
                if (exitedProcess != null && exitedProcess.HasExited)
                {
                    exitCode = exitedProcess.ExitCode;
                }
            }
            catch (Exception ex)
            {
                LogOutput($"WARN: Could not retrieve exit code for PID {processId}: {ex.Message}");
            }

            LogOutput($"Server process (PID: {processId}) has exited with code {exitCode}.");

            _dispatcher.InvokeAsync(() =>
            {
                ArkServer serverThatExited = null;
                try
                {
                    serverThatExited = _servers.FirstOrDefault(s => s.RunningProcess == exitedProcess);

                    if (exitedProcess != null && _serverProcess == exitedProcess)
                    {
                        string exitedServerName = serverThatExited?.Name ?? _currentServer?.Name ?? "Unknown Server";
                        LogOutput($"Cleaning up resources for server '{exitedServerName}'.");

                        if (_serverProcess != null)
                        {
                            _serverProcess.OutputDataReceived -= ServerProcess_OutputDataReceived;
                            _serverProcess.ErrorDataReceived -= ServerProcess_ErrorDataReceived;
                            _serverProcess.Exited -= ServerProcess_Exited;
                            _serverProcess.Dispose();
                        }
                        _serverProcess = null;

                        if (serverThatExited != null)
                        {
                            serverThatExited.RunningProcess = null;
                            Debug.WriteLine($"Cleared RunningProcess property for server '{serverThatExited.Name}'");
                        }
                        else if (_currentServer != null && _currentServer.RunningProcess == exitedProcess)
                        {
                            _currentServer.RunningProcess = null;
                            Debug.WriteLine($"Cleared RunningProcess property for current server '{_currentServer.Name}' (fallback)");
                        }

                        LogOutput($"Server process field cleared for '{exitedServerName}'.");

                        if (Application.Current.MainWindow is MainWindow mainWin)
                        {
                            mainWin.UpdateUIFromSelection();
                        }
                    }
                    else if (exitedProcess != null)
                    {
                        LogOutput($"WARN: Exited event received for an unexpected process (PID: {exitedProcess.Id}). Our tracked process is PID: {_serverProcess?.Id.ToString() ?? "None"}.");

                        if (serverThatExited != null)
                        {
                            serverThatExited.RunningProcess = null;
                            Debug.WriteLine($"Cleared RunningProcess property for server '{serverThatExited.Name}' which unexpectedly exited.");
                        }
                        exitedProcess.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    LogOutput($"ERROR during Exited event cleanup: {ex.Message}");
                    if (exitedProcess != null && _serverProcess == exitedProcess) { _serverProcess = null; }
                    if (serverThatExited != null) { serverThatExited.RunningProcess = null; }
                    else if (_currentServer?.RunningProcess == exitedProcess) { _currentServer.RunningProcess = null; }
                    if (Application.Current.MainWindow is MainWindow mainWin) { mainWin.UpdateUIFromSelection(); }
                }
            });
        }

        // --- Helper Methods for Launch ---

        private async Task<bool> GenerateIniFilesAsync(ArkServer server)
        {
            string configDir = Path.Combine(server.InstallPath, "ShooterGame", "Saved", "Config", "WindowsServer");
            try
            {
                Directory.CreateDirectory(configDir); // Ensure directory exists

                // Write Game.ini
                string gameIniPath = Path.Combine(configDir, "Game.ini");
                string gameIniContent = server.GameSettings?.ToIniString() ?? "[/Script/ShooterGame.ShooterGameMode]\n"; // Default if null
                await File.WriteAllTextAsync(gameIniPath, gameIniContent);
                LogOutput($"Generated Game.ini for '{server.Name}'.");

                // Write GameUserSettings.ini (combining multiple sections)
                string gusIniPath = Path.Combine(configDir, "GameUserSettings.ini");
                var gusBuilder = new StringBuilder();

                // [ServerSettings] section
                gusBuilder.AppendLine(server.ServerSettings?.ToIniString() ?? "[ServerSettings]\n");
                // Add passwords if they exist
                if (!string.IsNullOrWhiteSpace(server.ServerSettings?.ServerPassword))
                    gusBuilder.AppendLine($"ServerPassword={server.ServerSettings.ServerPassword}");
                if (!string.IsNullOrWhiteSpace(server.ServerSettings?.ServerAdminPassword))
                    gusBuilder.AppendLine($"ServerAdminPassword={server.ServerSettings.ServerAdminPassword}");
                gusBuilder.AppendLine(); // Separator

                // [/Script/ShooterGame.ShooterGameUserSettings] section
                gusBuilder.AppendLine(server.UserSettings?.ToIniString() ?? "[/Script/ShooterGame.ShooterGameUserSettings]\n");
                gusBuilder.AppendLine(); // Separator

                // [SessionSettings] section
                gusBuilder.AppendLine("[SessionSettings]");
                // Handle spaces in Session Name correctly by quoting
                string sessionName = server.Name;
                if (sessionName.Contains(' '))
                {
                    sessionName = $"\"{sessionName}\"";
                }
                gusBuilder.AppendLine($"SessionName={sessionName}");
                // Save selected map into GUS so it can be reloaded later
                if (!string.IsNullOrWhiteSpace(server.Map))
                {
                    gusBuilder.AppendLine($"Map={server.Map}");
                }
                // Save active mods list into GUS (comma-separated)
                if (server.Mods != null && server.Mods.Any())
                {
                    string modsCsv = string.Join(",", server.Mods.Where(m => !string.IsNullOrWhiteSpace(m)));
                    gusBuilder.AppendLine($"ActiveMods={modsCsv}");
                }
                gusBuilder.AppendLine(); // Separator

                // [/Script/Engine.GameSession] section (for MaxPlayers)
                gusBuilder.AppendLine("[/Script/Engine.GameSession]");
                gusBuilder.AppendLine($"MaxPlayers={server.PlayerLimit}");
                gusBuilder.AppendLine();

                await File.WriteAllTextAsync(gusIniPath, gusBuilder.ToString());
                LogOutput($"Generated GameUserSettings.ini for '{server.Name}'.");

                return true;
            }
            catch (IOException ioEx)
            {
                LogOutput($"ERROR generating INI files (IO): {ioEx.Message} - Check permissions and disk space for '{configDir}'.");
                return false;
            }
            catch (UnauthorizedAccessException uaEx)
            {
                LogOutput($"ERROR generating INI files (Access): {uaEx.Message} - Check permissions for '{configDir}'.");
                return false;
            }
            catch (Exception ex)
            {
                LogOutput($"ERROR generating INI files (General): {ex.Message}");
                return false;
            }
        }

        private string BuildLaunchArguments(ArkServer server)
        {
            var argsBuilder = new StringBuilder();

            // Map is the first argument, no prefix
            argsBuilder.Append(server.Map ?? "TheIsland_WP"); // Default map if null

            // Session Name (handle quotes)
            string sessionName = server.Name ?? "ARK Server";
            if (sessionName.Contains(' ')) { sessionName = $"\"{sessionName}\""; }
            argsBuilder.Append($"?SessionName={sessionName}");

            // Ports
            argsBuilder.Append($"?Port={server.GamePort}");
            argsBuilder.Append($"?QueryPort={server.QueryPort}");

            // Player Limit
            argsBuilder.Append($"?MaxPlayers={server.PlayerLimit}");

            // Basic required args
            argsBuilder.Append(" -log"); // Enable server logging to console/files
            argsBuilder.Append(" -NoBattlEye"); // Often needed for unofficial servers, adjust if BattlEye is desired/required
            if (server.ServerSettings.bAllowFlyerSpeedLeveling || server.GameSettings.bAllowFlyerSpeedLeveling)
                argsBuilder.Append(" -AllowFlyerSpeedLeveling"); // 

            // Mods (carefully format -mods="id1,id2")
            var validMods = server.Mods?.Where(m => !string.IsNullOrWhiteSpace(m) && long.TryParse(m, out _)).ToList() ?? new List<string>();
            if (validMods.Any())
            {
                string modsArgument = $"-mods={string.Join(",", validMods)}"; // No quotes around the value needed usually
                argsBuilder.Append($" {modsArgument}");
                LogOutput($"Adding Mods Argument: {modsArgument}");
            }
            else
            {
                LogOutput("No valid mods configured or found.");
            }

            // RCON
            if (server.ServerSettings?.RCONEnabled == true)
            {
                argsBuilder.Append($" -EnableRCON -RCONPort={server.RconPort}");
                LogOutput($"Adding RCON Argument: -EnableRCON -RCONPort={server.RconPort}");
            }

            // MultiHome (Specific IP Binding)
            if (!string.IsNullOrEmpty(server.IpAddress) && server.IpAddress != "0.0.0.0")
            {
                if (IPAddress.TryParse(server.IpAddress, out _))
                {
                    argsBuilder.Append($" -MultiHome={server.IpAddress}");
                    LogOutput($"Adding MultiHome Argument: -MultiHome={server.IpAddress}");
                }
                else
                {
                    LogOutput($"WARN: Invalid IP address format '{server.IpAddress}' provided. Ignoring MultiHome argument.");
                }
            }
            else
            {
                LogOutput("No specific IP address provided (or 0.0.0.0), server will bind to default.");
            }

            // Add other common/useful arguments (optional)
            argsBuilder.Append(" -UseCache");
            // argsBuilder.Append(" -AllowAnyoneBabyImprintCuddle"); // If you want this via command line instead of INI

            return argsBuilder.ToString();
        }

        #endregion

        #region Profile Management

        /// <summary>
        /// Loads server profiles from the JSON file or discovers existing server installations.
        /// Updates the provided ListBox on the UI thread.
        /// </summary>
        /// <param name="serverList">The ListBox to populate with server names.</param>
        public void LoadServerProfiles(ListBox serverList) // Removed ModListBox param - updated separately
        {
            bool profileFileExisted = File.Exists(_profilesFilePath);
            bool discoveredNewServers = false;
            List<ArkServer> loadedServers = new List<ArkServer>();

            try
            {
                if (profileFileExisted)
                {
                    LogOutput($"Loading profiles from: {_profilesFilePath}");
                    string json = File.ReadAllText(_profilesFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            AllowTrailingCommas = true,
                            ReadCommentHandling = JsonCommentHandling.Skip // Allow comments in JSON if needed
                        };
                        loadedServers = JsonSerializer.Deserialize<List<ArkServer>>(json, options) ?? new List<ArkServer>();
                        LogOutput($"Successfully deserialized {loadedServers.Count} profiles from JSON.");
                    }
                    else
                    {
                        LogOutput("Profile file exists but is empty.");
                    }

                    // Even if profiles.json exists, scan the Servers base folder to find any new installs
                    var discovered = DiscoverServers(out bool foundInScan);
                    if (foundInScan && discovered.Any())
                    {
                        // Merge discovered servers into loaded servers if their InstallPath isn't already present
                        foreach (var ds in discovered)
                        {
                            if (!loadedServers.Any(s => s.InstallPath.Equals(ds.InstallPath, StringComparison.OrdinalIgnoreCase)))
                            {
                                LogOutput($"Merging discovered server '{ds.Name}' from '{ds.InstallPath}' into loaded profiles.");
                                InitializeServerInstanceDefaults(ds);
                                LoadSettingsFromInstallPath(ds); // Attempt to read INI values
                                loadedServers.Add(ds);
                                discoveredNewServers = true;
                            }
                        }
                    }
                }
                else
                {
                    LogOutput($"Profile file not found at '{_profilesFilePath}'. Scanning for existing server installs...");
                    loadedServers = DiscoverServers(out discoveredNewServers); // Discover servers if no profile file
                }

                // Ensure all loaded/discovered servers have non-null settings objects and try to load INI values
                foreach (var server in loadedServers)
                {
                    InitializeServerInstanceDefaults(server);
                    // Try to load INI values if not already loaded
                    LoadSettingsFromInstallPath(server);
                }

                _servers = loadedServers; // Assign to the main list

                // Save profiles if new servers were discovered and added
                if (discoveredNewServers && _servers.Any())
                {
                    LogOutput($"Saving profile file after discovering {_servers.Count} servers.");
                    SaveServerProfiles();
                }

                // --- UI Update ---
                _dispatcher.Invoke(() =>
                {
                    serverList.Items.Clear();
                    if (_servers.Any())
                    {
                        _servers.ForEach(s => serverList.Items.Add(s.Name));
                        LogOutput($"Populated UI server list with {_servers.Count} servers.");
                    }
                    else
                    {
                        LogOutput("No server profiles loaded or discovered. UI list is empty.");
                        SelectServer(null); // Update internal state
                        (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection(); // Update UI to reflect null selection
                    }
                });
            }
            catch (JsonException jsonEx)
            {
                LogOutput($"ERROR parsing profiles JSON: {jsonEx.Message} - Path: {jsonEx.Path}, Line: {jsonEx.LineNumber}, Pos: {jsonEx.BytePositionInLine}");
                MessageBox.Show($"Error reading server profiles:\n{jsonEx.Message}\n\nA default empty list will be used. Check '{_profilesFilePath}' for issues.", "Profile Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _servers = new List<ArkServer>(); // Reset to empty list on error
                _dispatcher.Invoke(() => // Clear UI list on error
                {
                    serverList.Items.Clear();
                    SelectServer(null);
                    (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection();
                });
            }
            catch (Exception ex)
            {
                LogOutput($"ERROR loading/discovering profiles: {ex.GetType().Name} - {ex.Message}");
                LogOutput($"Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"An unexpected error occurred loading server profiles:\n{ex.Message}\n\nA default empty list will be used.", "Profile Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _servers = new List<ArkServer>(); // Reset to empty list
                _dispatcher.Invoke(() => // Clear UI list on error
                {
                    serverList.Items.Clear();
                    SelectServer(null);
                    (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection();
                });
            }
        }

        /// <summary>
        /// Scans the default Servers directory for potential ARK server installations.
        /// </summary>
        private List<ArkServer> DiscoverServers(out bool discoveredNew)
        {
            discoveredNew = false;
            var discoveredServers = new List<ArkServer>();
            Directory.CreateDirectory(_serversBaseDirectory); // Ensure base directory exists

            if (Directory.Exists(_serversBaseDirectory))
            {
                var potentialServerDirs = Directory.GetDirectories(_serversBaseDirectory);
                LogOutput($"Scanning '{_serversBaseDirectory}': Found {potentialServerDirs.Length} potential directories.");

                foreach (var dirPath in potentialServerDirs)
                {
                    string serverExePath = Path.Combine(dirPath, "ShooterGame", "Binaries", "Win64", "ArkAscendedServer.exe");
                    if (File.Exists(serverExePath))
                    {
                        string directoryName = Path.GetFileName(dirPath);
                        string serverName = directoryName.Replace("_", " ").Trim();

                        if (_servers.Any(s => s.InstallPath.Equals(dirPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            LogOutput($"Skipping discovered server in '{directoryName}' - path already exists in loaded profiles.");
                            continue;
                        }
                        if (discoveredServers.Any(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase)))
                        {
                            LogOutput($"Skipping discovered server in '{directoryName}' - generated name '{serverName}' conflicts with another discovery.");
                            continue;
                        }

                        LogOutput($"Discovered potential server: '{serverName}' in directory '{directoryName}'");
                        var discoveredServer = new ArkServer
                        {
                            Name = serverName,
                            InstallPath = dirPath
                        };

                        // Initialize and attempt to read INI values to populate settings
                        InitializeServerInstanceDefaults(discoveredServer);
                        LoadSettingsFromInstallPath(discoveredServer);

                        discoveredServers.Add(discoveredServer);
                        discoveredNew = true;
                    }
                    else
                    {
                        LogOutput($"Skipping directory '{Path.GetFileName(dirPath)}' - 'ArkAscendedServer.exe' not found at expected path.");
                    }
                }

                if (discoveredNew)
                {
                    LogOutput($"Discovery complete. Found {discoveredServers.Count} new server installations.");
                }
                else
                {
                    LogOutput("Discovery complete. No new server installations found.");
                }
            }
            else
            {
                LogOutput($"Servers base directory '{_serversBaseDirectory}' does not exist. Cannot discover servers.");
            }
            return discoveredServers;
        }

        /// <summary>
        /// Ensures the server object has non-null instances for its settings properties.
        /// </summary>
        private void InitializeServerInstanceDefaults(ArkServer server)
        {
            server.GameSettings ??= new GameSettings();
            server.UserSettings ??= new UserSettings();
            server.ServerSettings ??= new ServerSettings();
            server.Mods ??= new List<string>();
        }

        /// <summary>
        /// Creates a new server profile with default settings and adds it to the list and UI.
        /// </summary>
        /// <param name="serverList">The ListBox to update.</param>
        public void CreateNewServer(ListBox serverList)
        {
            int count = 1;
            string baseName = "New ARK Server";
            string name = baseName;
            while (_servers.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                count++;
                name = $"{baseName} {count}";
            }

            string installDirName = name.Replace(" ", "_");
            string installPath = Path.Combine(_serversBaseDirectory, installDirName);

            var newServer = new ArkServer
            {
                Name = name,
                InstallPath = installPath
            };
            InitializeServerInstanceDefaults(newServer); // Ensure settings objects are created

            try
            {
                Directory.CreateDirectory(newServer.InstallPath);
                LogOutput($"Created directory for new server: '{newServer.InstallPath}'");
            }
            catch (Exception ex)
            {
                LogOutput($"ERROR creating directory '{newServer.InstallPath}': {ex.Message}");
                MessageBox.Show($"Failed to create directory for server '{newServer.Name}':\n{ex.Message}\n\nThe profile will be created, but installation might fail later.", "Directory Creation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            _servers.Add(newServer);

            // Update UI on the UI thread and force a full UI refresh (so Settings panels populate immediately)
            _dispatcher.Invoke(() =>
            {
                serverList.Items.Add(newServer.Name);
                serverList.SelectedItem = newServer.Name; // Select the newly added server
            });

            // Force main UI to refresh (ensures settings panels are updated immediately)
            _dispatcher.InvokeAsync(() => (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection());

            SaveServerProfiles(); // Save the updated list
            LogOutput($"Created and saved new server profile: '{newServer.Name}'");
        }

        /// <summary>
        /// Deletes a server profile from the list and UI. Does NOT delete server files.
        /// </summary>
        /// <param name="serverName">Name of the server profile to delete.</param>
        /// <param name="serverList">The ListBox to update.</param>
        public void DeleteServer(string serverName, ListBox serverList)
        {
            ArkServer serverToDelete = _servers.FirstOrDefault(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));

            if (serverToDelete == null)
            {
                LogOutput($"ERROR: Cannot delete profile. Server '{serverName}' not found.");
                return;
            }

            bool isRunning = false;
            if (_serverProcess != null && !_serverProcess.HasExited && _currentServer == serverToDelete)
            {
                isRunning = true;
            }

            if (isRunning)
            {
                LogOutput($"Cannot delete profile '{serverName}' because the server is currently running (PID: {_serverProcess.Id}). Please stop the server first.");
                MessageBox.Show($"The server '{serverName}' is currently running.\nPlease stop the server before deleting its profile.", "Server Running", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _servers.Remove(serverToDelete);

            _dispatcher.Invoke(() =>
            {
                serverList.Items.Remove(serverName);

                if (_servers.Any())
                {
                    serverList.SelectedIndex = 0;
                }
                else
                {
                    serverList.SelectedItem = null;
                    SelectServer(null); // Clear internal selection
                    (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection(); // Update UI to empty state
                }
            });

            SaveServerProfiles(); // Save the changes
            LogOutput($"Deleted server profile: '{serverName}'. Server files were NOT deleted from '{serverToDelete.InstallPath}'.");
        }

        /// <summary>
        /// Sets the internally tracked current server.
        /// </summary>
        /// <param name="serverName">Name of the server to select, or null to clear selection.</param>
        public void SelectServer(string serverName)
        {
            if (string.IsNullOrEmpty(serverName))
            {
                if (_currentServer != null) // Only log if changing from a selected server
                {
                    LogOutput("Server selection cleared.");
                }
                _currentServer = null;
            }
            else
            {
                var newSelection = _servers.FirstOrDefault(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase));
                if (newSelection != null)
                {
                    if (_currentServer != newSelection) // Only log if changing selection
                    {
                        _currentServer = newSelection;
                        LogOutput($"Selected server: '{_currentServer.Name}'");
                    }
                }
                else
                {
                    LogOutput($"WARN: Attempted to select non-existent server name '{serverName}'. Selection unchanged.");
                }
            }
        }

        /// <summary>
        /// Saves all current server profiles to the JSON file.
        /// </summary>
        public void SaveServerProfiles()
        {
            var serversToSave = _servers.ToList();
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true // Make the JSON human-readable
                };
                string json = JsonSerializer.Serialize(serversToSave, options);
                File.WriteAllText(_profilesFilePath, json);
            }
            catch (IOException ioEx) { LogOutput($"ERROR saving profiles (IO): {ioEx.Message}"); }
            catch (UnauthorizedAccessException uaEx) { LogOutput($"ERROR saving profiles (Access): {uaEx.Message}"); }
            catch (Exception ex) { LogOutput($"ERROR saving profiles (General): {ex.Message}"); }
        }

        /// <summary>
        /// Saves settings for the current server immediately.
        /// Typically called after a direct property change.
        /// </summary>
        public void SaveCurrentServerSettings()
        {
            if (_currentServer == null) return;
            SaveServerProfiles();
        }

        #endregion

        #region Mod Management

        public void UpdateModListUI(ListBox modListBox)
        {
            _dispatcher.InvokeAsync(() =>
            {
                modListBox.Items.Clear();
                if (_currentServer?.Mods != null)
                {
                    foreach (var modId in _currentServer.Mods)
                    {
                        modListBox.Items.Add(modId);
                    }
                }
                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    mainWin.RemoveModButton.IsEnabled = mainWin.ModListBox.SelectedItem != null;
                }
            });
        }

        public void AddMod(string modId)
        {
            if (_currentServer == null)
            {
                LogOutput("Cannot add mod: No server selected.");
                return;
            }

            if (string.IsNullOrWhiteSpace(modId) || !long.TryParse(modId, out _))
            {
                LogOutput($"ERROR: Invalid Mod ID format: '{modId}'. Must be numeric.");
                MessageBox.Show($"Invalid Mod ID: '{modId}'\nMod IDs must be numeric.", "Invalid Mod ID", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            _currentServer.Mods ??= new List<string>();

            if (!_currentServer.Mods.Contains(modId))
            {
                _currentServer.Mods.Add(modId);
                SaveCurrentServerSettings(); // Save the change
                LogOutput($"Added mod ID {modId} to server '{_currentServer.Name}'. Restart server for changes to take effect.");
                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    UpdateModListUI(mainWin.ModListBox);
                }
            }
            else
            {
                LogOutput($"Mod ID {modId} already exists for server '{_currentServer.Name}'.");
            }
        }

        public void RemoveMod(string modId)
        {
            if (_currentServer == null)
            {
                LogOutput("Cannot remove mod: No server selected.");
                return;
            }
            if (string.IsNullOrWhiteSpace(modId))
            {
                LogOutput("Cannot remove mod: No mod ID provided.");
                return;
            }

            if (_currentServer.Mods != null && _currentServer.Mods.Remove(modId))
            {
                SaveCurrentServerSettings(); // Save the change
                LogOutput($"Removed mod ID {modId} from server '{_currentServer.Name}'. Restart server for changes to take effect.");
                if (Application.Current.MainWindow is MainWindow mainWin)
                {
                    UpdateModListUI(mainWin.ModListBox);
                }
            }
            else
            {
                LogOutput($"Mod ID {modId} not found for server '{_currentServer.Name}'.");
            }
        }

        #endregion

        #region SteamCMD Operations

        public async Task ValidateUpdateServer(ArkServer server)
        {
            if (server == null)
            {
                LogOutput("ERROR: No server provided for validation/update.");
                return;
            }

            if (server == _currentServer && _serverProcess != null && !_serverProcess.HasExited)
            {
                LogOutput($"Cannot validate/update server '{server.Name}' while it is running (PID: {_serverProcess.Id}). Please stop the server first.");
                MessageBox.Show($"The server '{server.Name}' is currently running.\nPlease stop the server before validating or updating its files.", "Server Running", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LogOutput($"Starting validation/update for server '{server.Name}' at '{server.InstallPath}'...");

            bool success = await SetupServerDependencies(server);

            LogOutput(success ? $"Validation/update process completed for '{server.Name}'."
                              : $"ERROR: Validation/update process FAILED for '{server.Name}'. Check logs for details.");

            if (server.Mods != null && server.Mods.Any())
            {
                LogOutput($"INFO: Server has mods configured ({string.Join(",", server.Mods)}). Mod updates are typically handled by the server on launch.");
            }
        }

        private async Task<bool> SetupServerDependencies(ArkServer server)
        {
            if (string.IsNullOrEmpty(_steamCmdPath) || !File.Exists(_steamCmdPath))
            {
                LogOutput("SteamCMD executable not found. Attempting to download...");
                await EnsureSteamCmdExists();
                if (!File.Exists(_steamCmdPath))
                {
                    LogOutput("FATAL: SteamCMD not found and download failed. Cannot update server.");
                    return false;
                }
            }

            if (_steamCmdProcess != null && !_steamCmdProcess.HasExited)
            {
                LogOutput("ERROR: Another SteamCMD operation appears to be in progress. Please wait.");
                return false;
            }

            try
            {
                Directory.CreateDirectory(server.InstallPath);
            }
            catch (Exception ex)
            {
                LogOutput($"ERROR creating server install directory '{server.InstallPath}': {ex.Message}");
                return false;
            }

            string serverAppId = "2430930"; // ARK: Survival Ascended Dedicated Server App ID
            bool success = false;
            int attempt = 0;
            const int maxRetries = 3; // Retry once on failure

            LogOutput($"Updating/Validating ARK Server (AppId: {serverAppId}) in '{server.InstallPath}'...");

            while (attempt < maxRetries && !success)
            {
                attempt++;
                if (attempt > 1)
                {
                    LogOutput($"Retrying SteamCMD operation (Attempt {attempt}/{maxRetries})...");
                }

                string args = $"+force_install_dir \"{server.InstallPath}\" +login anonymous +app_update {serverAppId} validate +quit";

                success = await RunSteamCmdAsync(args);

                if (!success && attempt < maxRetries)
                {
                    await Task.Delay(5000);
                }
            }

            if (!success)
            {
                LogOutput($"ERROR: Failed to update/validate server files after {maxRetries} attempts.");
                return false;
            }

            LogOutput("SteamCMD operation completed successfully.");

            string serverExePath = Path.Combine(server.InstallPath, "ShooterGame", "Binaries", "Win64", "ArkAscendedServer.exe");
            if (!File.Exists(serverExePath))
            {
                LogOutput($"ERROR: Server executable still missing at '{serverExePath}' after SteamCMD operation.");
                return false;
            }

            LogOutput("Server dependency check and update successful.");
            return true;
        }

        private async Task<bool> RunSteamCmdAsync(string arguments)
        {
            bool success = false;

            if (_steamCmdProcess != null && !_steamCmdProcess.HasExited)
            {
                LogOutput("Error: SteamCMD instance is already running.");
                return false;
            }

            using (var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _steamCmdPath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                },
                EnableRaisingEvents = true
            })
            {
                _steamCmdProcess = process;

                var outputLog = new List<string>();
                var errorLog = new List<string>();
                var processCompletionSource = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (s, evt) =>
                {
                    if (evt.Data != null)
                    {
                        LogOutput($"SteamCMD: {evt.Data}");
                        outputLog.Add(evt.Data);
                    }
                };
                process.ErrorDataReceived += (s, evt) =>
                {
                    if (evt.Data != null)
                    {
                        LogOutput($"SteamCMD ERR: {evt.Data}");
                        errorLog.Add(evt.Data);
                    }
                };

                process.Exited += (s, evt) =>
                {
                    LogOutput($"SteamCMD process exited. Code: {process.ExitCode}");
                    processCompletionSource.TrySetResult(process.ExitCode == 0);
                };

                try
                {
                    LogOutput($"Executing SteamCMD: {_steamCmdPath} {arguments}");
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool exitedGracefully = await processCompletionSource.Task;

                    if (process.ExitCode == 0)
                    {
                        bool downloadFailed = outputLog.Any(l => l.Contains("ERROR! Download item", StringComparison.OrdinalIgnoreCase) && l.Contains("failed", StringComparison.OrdinalIgnoreCase)) ||
                                              errorLog.Any(l => l.Contains("ERROR! Download item", StringComparison.OrdinalIgnoreCase) && l.Contains("failed", StringComparison.OrdinalIgnoreCase)) ||
                                              outputLog.Any(l => l.Contains("Timeout downloading item", StringComparison.OrdinalIgnoreCase)) ||
                                              errorLog.Any(l => l.Contains("Timeout downloading item", StringComparison.OrdinalIgnoreCase));

                        if (downloadFailed)
                        {
                            LogOutput("ERROR: SteamCMD reported download failure/timeout in logs despite exit code 0.");
                            success = false;
                        }
                        else if (outputLog.Any(l => l.Contains("Success! App", StringComparison.OrdinalIgnoreCase) && l.Contains("fully installed", StringComparison.OrdinalIgnoreCase)) ||
                                 outputLog.Any(l => l.Contains("Success! Downloaded item", StringComparison.OrdinalIgnoreCase)) ||
                                 outputLog.Any(l => l.Contains("already up to date", StringComparison.OrdinalIgnoreCase)))
                        {
                            LogOutput("SteamCMD success message detected in logs.");
                            success = true;
                        }
                        else
                        {
                            LogOutput("WARN: SteamCMD exit code 0, but standard success message not detected in logs. Assuming success.");
                            success = true;
                        }
                    }
                    else
                    {
                        LogOutput($"ERROR: SteamCMD process exited with non-zero code: {process.ExitCode}.");
                        LogOutput("--- Last 5 Output Lines ---");
                        outputLog.Skip(Math.Max(0, outputLog.Count - 5)).ToList().ForEach(l => LogOutput($" > {l}"));
                        LogOutput("--- Last 5 Error Lines ---");
                        errorLog.Skip(Math.Max(0, errorLog.Count - 5)).ToList().ForEach(l => LogOutput($" > {l}"));
                        success = false;
                    }
                }
                catch (Exception ex)
                {
                    LogOutput($"ERROR running SteamCMD: {ex.Message}");
                    LogOutput($"Stack Trace: {ex.StackTrace}");
                    success = false;
                    processCompletionSource.TrySetResult(false);
                }
                finally
                {
                    // If failure, write a combined log file for inspection
                    if (!success)
                    {
                        try
                        {
                            string fileName = $"steamcmd_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid()}.log";
                            string path = Path.Combine(Path.GetTempPath(), fileName);
                            var sb = new StringBuilder();
                            sb.AppendLine($"Command: {_steamCmdPath} {arguments}");
                            sb.AppendLine("---- STDOUT ----");
                            foreach (var line in outputLog) sb.AppendLine(line);
                            sb.AppendLine("---- STDERR ----");
                            foreach (var line in errorLog) sb.AppendLine(line);
                            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                            LogOutput($"SteamCMD failure log written to: {path}");
                            // Show a message to the user on UI thread
                            _dispatcher.Invoke(() => MessageBox.Show($"SteamCMD failed. Log saved to:\n{path}\n\nOpen the log to inspect the error output.", "SteamCMD Error", MessageBoxButton.OK, MessageBoxImage.Error));
                        }
                        catch (Exception ex2)
                        {
                            LogOutput($"Failed to write SteamCMD failure log: {ex2.Message}");
                        }
                    }

                    _steamCmdProcess = null;
                }
            }

            return success;
        }


        private async Task EnsureSteamCmdExists()
        {
            if (!File.Exists(_steamCmdPath))
            {
                LogOutput($"SteamCMD not found at '{_steamCmdPath}'. Attempting to download...");
                try
                {
                    string steamCmdZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
                    string tempZipPath = Path.Combine(Path.GetTempPath(), $"steamcmd_{Guid.NewGuid()}.zip");

                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromMinutes(2);
                        LogOutput($"Downloading from {steamCmdZipUrl}...");
                        byte[] fileBytes = await client.GetByteArrayAsync(steamCmdZipUrl);
                        await File.WriteAllBytesAsync(tempZipPath, fileBytes);
                        LogOutput($"Downloaded successfully to '{tempZipPath}'.");
                    }

                    LogOutput($"Extracting 'steamcmd.exe' to '{_steamCmdPath}'...");
                    using (ZipArchive archive = ZipFile.OpenRead(tempZipPath))
                    {
                        ZipArchiveEntry steamCmdEntry = archive.Entries.FirstOrDefault(entry => entry.Name.Equals("steamcmd.exe", StringComparison.OrdinalIgnoreCase));
                        if (steamCmdEntry != null)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(_steamCmdPath));
                            steamCmdEntry.ExtractToFile(_steamCmdPath, true);
                            LogOutput("SteamCMD extracted successfully.");
                        }
                        else
                        {
                            LogOutput("ERROR: 'steamcmd.exe' not found within the downloaded zip file.");
                        }
                    }

                    File.Delete(tempZipPath);
                }
                catch (HttpRequestException httpEx)
                {
                    LogOutput($"ERROR downloading SteamCMD (Network): {httpEx.Message} - Check internet connection and URL.");
                }
                catch (IOException ioEx)
                {
                    LogOutput($"ERROR saving or extracting SteamCMD (IO): {ioEx.Message} - Check permissions and disk space.");
                }
                catch (UnauthorizedAccessException uaEx)
                {
                    LogOutput($"ERROR saving or extracting SteamCMD (Access): {uaEx.Message} - Check permissions.");
                }
                catch (Exception ex)
                {
                    LogOutput($"ERROR downloading or extracting SteamCMD (General): {ex.GetType().Name} - {ex.Message}");
                }
            }
            else
            {
                LogOutput($"SteamCMD already exists at '{_steamCmdPath}'.");
            }
        }

        #endregion

        #region RCON (Placeholder)

        public void SendCommand(string command)
        {
            if (_currentServer == null)
            {
                LogOutput("ERROR: No server selected to send RCON command.");
                return;
            }
            if (_serverProcess == null || _serverProcess.HasExited)
            {
                LogOutput($"ERROR: Server '{_currentServer.Name}' is not running. Cannot send RCON command.");
                return;
            }
            if (_currentServer.ServerSettings?.RCONEnabled != true)
            {
                LogOutput($"ERROR: RCON is not enabled in settings for server '{_currentServer.Name}'.");
                return;
            }
            if (string.IsNullOrWhiteSpace(_currentServer.ServerSettings?.ServerAdminPassword))
            {
                LogOutput($"ERROR: RCON requires an Admin Password to be set for server '{_currentServer.Name}'.");
                MessageBox.Show("RCON requires an Admin Password.\nPlease set the Admin Password in the server's settings.", "RCON Password Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(command))
            {
                LogOutput("WARN: Attempted to send empty RCON command.");
                return;
            }

            LogOutput($"RCON > {command} (To: {_currentServer.IpAddress}:{_currentServer.RconPort}) [Placeholder - Not Sent]");
            MessageBox.Show($"RCON functionality is not yet implemented.\n\nCommand:\n{command}\n\nTarget:\n{_currentServer.IpAddress}:{_currentServer.RconPort}", "RCON Placeholder", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region INI Loading Helpers

        /// <summary>
        /// Attempts to load GameUserSettings.ini and Game.ini from a server install path and populate the server's settings objects.
        /// Non-fatal: logs and continues on errors.
        /// </summary>
        private void LoadSettingsFromInstallPath(ArkServer server)
        {
            try
            {
                if (server == null || string.IsNullOrWhiteSpace(server.InstallPath)) return;

                string configDir = Path.Combine(server.InstallPath, "ShooterGame", "Saved", "Config", "WindowsServer");
                if (!Directory.Exists(configDir)) return;

                string gusPath = Path.Combine(configDir, "GameUserSettings.ini");
                string gamePath = Path.Combine(configDir, "Game.ini");

                // Ensure defaults exist
                InitializeServerInstanceDefaults(server);

                if (File.Exists(gusPath))
                {
                    var sections = ParseIniFile(gusPath);
                    if (sections.TryGetValue("ServerSettings", out var serverLines))
                    {
                        PopulateSettingsFromKeyValues(server.ServerSettings, serverLines);
                    }
                    if (sections.TryGetValue("/Script/ShooterGame.ShooterGameUserSettings", out var userLines) ||
                        sections.TryGetValue("/Script/ShooterGame.ShooterGameUserSettings]", out userLines)) // tolerate trailing bracket differences
                    {
                        PopulateSettingsFromKeyValues(server.UserSettings, userLines);
                    }
                    // Read [SessionSettings] for SessionName, Map, ActiveMods
                    if (sections.TryGetValue("SessionSettings", out var sessionLines))
                    {
                        foreach (var line in sessionLines)
                        {
                            int eq = line.IndexOf('=');
                            if (eq < 0) continue;
                            string key = line.Substring(0, eq).Trim();
                            string val = line.Substring(eq + 1).Trim();
                            if (string.Equals(key, "SessionName", StringComparison.OrdinalIgnoreCase))
                            {
                                // Remove surrounding quotes if present
                                if (val.StartsWith("\"") && val.EndsWith("\"")) val = val.Substring(1, val.Length - 2);
                                server.Name = val;
                            }
                            else if (string.Equals(key, "Map", StringComparison.OrdinalIgnoreCase))
                            {
                                server.Map = val;
                            }
                            else if (string.Equals(key, "ActiveMods", StringComparison.OrdinalIgnoreCase))
                            {
                                var mods = val.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
                                if (mods.Any()) server.Mods = mods;
                            }
                        }
                    }
                    // SessionSettings and Engine.GameSession are handled by launch args and ark-specific values; ignore for now
                }

                if (File.Exists(gamePath))
                {
                    var sections = ParseIniFile(gamePath);
                    if (sections.TryGetValue("/Script/ShooterGame.ShooterGameMode", out var gameLines))
                    {
                        PopulateGameSettingsFromKeyValues(server.GameSettings, gameLines);
                    }
                }
            }
            catch (Exception ex)
            {
                LogOutput($"WARN: Failed to read INI settings for '{server.Name}' at '{server.InstallPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Basic INI parser that returns a mapping of section name -> list of raw key=value lines (ignores comments).
        /// Section names are stored without surrounding brackets.
        /// </summary>
        private Dictionary<string, List<string>> ParseIniFile(string filePath)
        {
            var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            string currentSection = "";
            foreach (var raw in File.ReadAllLines(filePath))
            {
                string line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith(";") || line.StartsWith("//")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!dict.ContainsKey(currentSection)) dict[currentSection] = new List<string>();
                    continue;
                }
                // If not in a section, skip lines until a section is found
                if (string.IsNullOrEmpty(currentSection)) continue;
                dict[currentSection].Add(line);
            }
            return dict;
        }

        /// <summary>
        /// Populate simple scalar properties on a settings object using key=value lines.
        /// Uses reflection to map keys to properties; tolerant to small name differences.
        /// </summary>
        private void PopulateSettingsFromKeyValues(object settingsObj, List<string> lines)
        {
            if (settingsObj == null || lines == null) return;
            var props = settingsObj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                    .Where(p => p.CanWrite).ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                // Try exact match first
                if (!props.TryGetValue(key, out var prop))
                {
                    // Try removing underscores / brackets variants
                    string keyNormalized = key.Replace("_", "").Split('[')[0];
                    props.TryGetValue(keyNormalized, out prop);
                }
                if (prop == null) continue;

                try
                {
                    if (prop.PropertyType == typeof(string))
                    {
                        prop.SetValue(settingsObj, val);
                    }
                    else if (prop.PropertyType == typeof(bool))
                    {
                        if (bool.TryParse(val, out var bv)) prop.SetValue(settingsObj, bv);
                    }
                    else if (prop.PropertyType == typeof(int))
                    {
                        if (int.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var iv)) prop.SetValue(settingsObj, iv);
                    }
                    else if (prop.PropertyType == typeof(float))
                    {
                        if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var fv)) prop.SetValue(settingsObj, fv);
                    }
                    // ignore other types
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PopulateSettingsFromKeyValues: Failed to set {prop.Name} = {val}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Populates GameSettings including array entries like PerLevelStatsMultiplier_Player[0]=...
        /// </summary>
        private void PopulateGameSettingsFromKeyValues(GameSettings gameSettings, List<string> lines)
        {
            if (gameSettings == null || lines == null) return;

            var props = typeof(GameSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                            .Where(p => p.CanWrite).ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);

            foreach (var line in lines)
            {
                int eq = line.IndexOf('=');
                if (eq < 0) continue;
                string key = line.Substring(0, eq).Trim();
                string val = line.Substring(eq + 1).Trim();

                // Detect array entry: e.g., PerLevelStatsMultiplier_Player[0]
                int bracket = key.IndexOf('[');
                if (bracket > 0 && key.EndsWith("]"))
                {
                    string baseKey = key.Substring(0, bracket);
                    string indexStr = key.Substring(bracket + 1, key.Length - bracket - 2);
                    if (!int.TryParse(indexStr, out int idx)) continue;

                    // Normalize baseKey by removing underscores to match property names (PerLevelStatsMultiplier_Player -> PerLevelStatsMultiplierPlayer)
                    string propName = baseKey.Replace("_", "");
                    if (!props.TryGetValue(propName, out var prop)) continue;

                    if (prop.PropertyType.IsArray && prop.PropertyType.GetElementType() == typeof(float))
                    {
                        try
                        {
                            var arr = prop.GetValue(gameSettings) as float[];
                            if (arr != null && idx >= 0 && idx < arr.Length)
                            {
                                if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out float fv))
                                {
                                    arr[idx] = fv;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"PopulateGameSettingsFromKeyValues: Failed to set array {propName}[{idx}] = {val}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    // Scalar - try direct property name then fallback normalized
                    if (!props.TryGetValue(key, out var prop))
                    {
                        string keyNormalized = key.Replace("_", "");
                        props.TryGetValue(keyNormalized, out prop);
                    }
                    if (prop == null) continue;

                    try
                    {
                        if (prop.PropertyType == typeof(string))
                        {
                            prop.SetValue(gameSettings, val);
                        }
                        else if (prop.PropertyType == typeof(bool))
                        {
                            if (bool.TryParse(val, out var bv)) prop.SetValue(gameSettings, bv);
                        }
                        else if (prop.PropertyType == typeof(int))
                        {
                            if (int.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var iv)) prop.SetValue(gameSettings, iv);
                        }
                        else if (prop.PropertyType == typeof(float))
                        {
                            if (float.TryParse(val, NumberStyles.Any, CultureInfo.InvariantCulture, out var fv)) prop.SetValue(gameSettings, fv);
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"PopulateGameSettingsFromKeyValues: Failed to set {prop.Name} = {val}: {ex.Message}");
                    }
                }
            }
        }

        #endregion

        #region Add Existing Server (public) - used by UI folder picker

        /// <summary>
        /// Validates and adds an already-installed server folder to profiles, reads its INI values if present,
        /// updates UI and saves profiles. Returns true on success.
        /// </summary>
        public bool AddExistingServer(string installPath, System.Windows.Controls.ListBox serverList)
        {
            if (string.IsNullOrWhiteSpace(installPath))
            {
                LogOutput("AddExistingServer: No path provided.");
                return false;
            }

            if (!Directory.Exists(installPath))
            {
                LogOutput($"AddExistingServer: Path does not exist: '{installPath}'");
                MessageBox.Show($"The selected folder does not exist:\n{installPath}", "Invalid Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string serverExePath = Path.Combine(installPath, "ShooterGame", "Binaries", "Win64", "ArkAscendedServer.exe");
            if (!File.Exists(serverExePath))
            {
                LogOutput($"AddExistingServer: ArkAscendedServer.exe not found at expected path: '{serverExePath}'");
                MessageBox.Show("The selected folder does not appear to contain an ARK dedicated server installation.\n" +
                                       "Make sure the folder contains ShooterGame\\Binaries\\Win64\\ArkAscendedServer.exe", "Not an ARK Server Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (_servers.Any(s => s.InstallPath.Equals(installPath, StringComparison.OrdinalIgnoreCase)))
            {
                LogOutput($"AddExistingServer: Server with path '{installPath}' already exists in profiles.");
                MessageBox.Show("A profile already exists for the selected folder.", "Duplicate Profile", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }

            string directoryName = Path.GetFileName(installPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string baseName = (string.IsNullOrWhiteSpace(directoryName) ? "Existing ARK Server" : directoryName.Replace("_", " ").Trim());
            string serverName = baseName;
            int suffix = 1;
            while (_servers.Any(s => s.Name.Equals(serverName, StringComparison.OrdinalIgnoreCase)))
            {
                suffix++;
                serverName = $"{baseName} {suffix}";
            }

            var newServer = new ArkServer
            {
                Name = serverName,
                InstallPath = installPath
            };

            InitializeServerInstanceDefaults(newServer);
            // Try to load INI values so the UI is populated immediately
            LoadSettingsFromInstallPath(newServer);

            _servers.Add(newServer);

            _dispatcher.Invoke(() =>
            {
                serverList.Items.Add(newServer.Name);
                serverList.SelectedItem = newServer.Name;
            });

            // Force UI refresh to populate settings panels
            _dispatcher.InvokeAsync(() => (Application.Current.MainWindow as MainWindow)?.UpdateUIFromSelection());

            SaveServerProfiles();
            LogOutput($"Added existing server profile: '{newServer.Name}' -> '{newServer.InstallPath}'");

            return true;
        }

        #endregion

        #region Logging

        /// <summary>
        /// Logs a message to the console output TextBox on the UI thread.
        /// Uses Normal priority so updates appear even if the console tab isn't being re-rendered.
        /// </summary>
        /// <param name="message">The message to log.</param>
        private void LogOutput(string message)
        {
            if (string.IsNullOrEmpty(message) || _dispatcher == null) return;

            try
            {
                // Use InvokeAsync for non-blocking UI update but at Normal priority so UI updates reliably
                _dispatcher.InvokeAsync(() =>
                {
                    if (_consoleOutput == null) return;

                    const int MaxLogLength = 30000; // Max characters in TextBox
                    const int TrimLength = 15000; // Amount to keep when trimming
                    if (_consoleOutput.Text.Length > MaxLogLength)
                    {
                        _consoleOutput.Text = _consoleOutput.Text.Substring(_consoleOutput.Text.Length - TrimLength);
                        _consoleOutput.AppendText("...(Log Trimmed)...\n");
                    }

                    _consoleOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
                    _consoleOutput.ScrollToEnd();
                }, DispatcherPriority.Normal);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"!!! LogOutput Dispatcher Error: {ex.Message} !!!");
                Debug.WriteLine($"!!! Original Message: {message} !!!");
            }
        }

        #endregion
    }
}