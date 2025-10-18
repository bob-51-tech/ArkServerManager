using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media; // For Brushes/Color in dialog
using System.ComponentModel; // Keep for potential future use like Closing event args
using System.Net.Sockets; // For AddressFamily
using System.Net.NetworkInformation; // For NetworkInterface
using Forms = System.Windows.Forms; // Added using alias for Windows Forms

namespace ArkServerManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ServerManager serverManager;
        private readonly SettingsManager settingsManager;
        public bool isInitializing = true; // Flag to prevent event handlers during setup // Made public for SettingsManager check

        public MainWindow()
        {
            InitializeComponent();
            serverManager = new ServerManager(ConsoleOutput); // Pass console output TextBox
            settingsManager = new SettingsManager(serverManager, GameSettingsPanel, UserSettingsPanel, ServerSettingsPanel);
            Loaded += MainWindow_Loaded; // Subscribe to Loaded event
            Closing += MainWindow_Closing; // Subscribe to Closing event
        }

        /// <summary>
        /// Handles initialization tasks after the window is fully loaded.
        /// </summary>
        // Inside MainWindow.xaml.cs -> MainWindow_Loaded()
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainWindow Loaded event started.");
            isInitializing = true; // Ensure flag is set at the start

            try
            {
                // 1. Init Manager
                LogStatus("Initializing manager...");
                await serverManager.InitializeAsync();
                LogStatus("Manager initialized.");

                // 2. Init UI Panel Structure
                LogStatus("Initializing UI panel structure...");
                settingsManager.InitializeGameSettingsPanel();
                settingsManager.InitializeServerSettingsPanel();
                settingsManager.InitializeUserSettingsPanel();
                LogStatus("UI panel structure initialized.");

                // 3. Load Profiles
                LogStatus("Loading server profiles...");
                serverManager.LoadServerProfiles(ServerList); // Populates ServerList.Items
                LogStatus("Server profiles loading process finished.");

                // 4. Select First Item *if* profiles exist
                if (ServerList.Items.Count > 0)
                {
                    LogStatus($"Profiles loaded. Selecting first server internally: {ServerList.Items[0]}");
                    // Select the item programmatically. This *might* trigger SelectionChanged,
                    // but we won't rely on it for the initial settings load anymore.
                    ServerList.SelectedIndex = 0;

                    // Make sure the ServerManager knows about the selection
                    serverManager.SelectServer(ServerList.Items[0].ToString());

                    // Update the basic UI elements (textboxes, buttons etc.) immediately
                    LogStatus("Performing initial basic UI update...");
                    UpdateUIFromSelection(); // Updates basic fields and button states

                    // **** NEW: Directly update SettingsManager UI AFTER basic UI update ****
                    LogStatus("Performing initial SettingsManager UI update...");
                    // This runs after the main window is loaded and the first item is selected.
                    settingsManager.UpdateUIFromSelection();
                    LogStatus("Initial SettingsManager UI update complete.");
                    // **** END NEW ****

                    // Fetch IP after everything else is done
                    await FetchPublicIpAndSetIfNeeded();

                }
                else
                {
                    // No profiles loaded.
                    LogStatus("No profiles loaded or found. Setting UI to empty state.");
                    serverManager.SelectServer(null);
                    UpdateUIFromSelection(); // Update UI controls to disabled/empty state
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FATAL ERROR during MainWindow Load: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"An critical error occurred during application startup:\n\n{ex.Message}\n\nThe application might not function correctly.", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                LogStatus("CRITICAL ERROR during startup.");
            }
            finally
            {
                // Set isInitializing to false AFTER all setup, including the explicit initial update.
                isInitializing = false;
                Debug.WriteLine("MainWindow initialization complete (isInitializing set to false).");
                LogStatus("Ready.");
            }
        }

        /// <summary>
        /// Logs a status message to the ConsoleOutput and Debug output.
        /// </summary>
        private void LogStatus(string message)
        {
            Debug.WriteLine($"[STATUS] {message}");
            // Optional: Add to a status bar or the console output if desired
            // Dispatcher.InvokeAsync(() => ConsoleOutput.AppendText($"[{DateTime.Now:HH:mm:ss}] [STATUS] {message}\n"));
        }

        /// <summary>
        /// Fetches the local IPv4 address suitable for server binding (usually 0.0.0.0) or uses existing profile IP.
        /// </summary>
        private async Task FetchPublicIpAndSetIfNeeded() // Method name kept, but functionality changed to local IP
        {
            if (serverManager?.CurrentServer == null)
            {
                await Dispatcher.InvokeAsync(() => IpAddressTextBox.Text = "N/A");
                return;
            }

            // Only fetch/set if the current IP is the default "0.0.0.0" placeholder
            if (serverManager.CurrentServer.IpAddress == "0.0.0.0")
            {
                LogStatus("Current IP is 0.0.0.0, attempting to determine best local bind address...");
                string localBindAddress = "0.0.0.0"; // Default to binding all interfaces
                bool fetchAttempted = false;

                try
                {
                    fetchAttempted = true;
                    await Dispatcher.InvokeAsync(() => IpAddressTextBox.Text = "Determining...");

                    // GetLocalIPv4Address now correctly returns 0.0.0.0 for most typical setups,
                    // which is what ARK needs for -MultiHome=0.0.0.0 or no -MultiHome arg.
                    // We keep the call structure in case future logic needs a specific local IP.
                    localBindAddress = GetLocalIPv4Address(); // Gets "0.0.0.0" or a specific non-private IP if found

                    if (string.IsNullOrWhiteSpace(localBindAddress)) // Should ideally not happen if GetLocalIPv4Address works
                    {
                        LogStatus($"Failed to determine local bind address. Using default 0.0.0.0.");
                        localBindAddress = "0.0.0.0"; // Fallback
                    }
                    else
                    {
                        LogStatus($"Determined local bind address: {localBindAddress}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Determine Local Bind Address Error: {ex.Message}");
                    LogStatus("Failed to determine local bind address. Using default 0.0.0.0.");
                    localBindAddress = "0.0.0.0";
                }
                finally
                {
                    // Update UI and save if necessary
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (serverManager.CurrentServer != null)
                        {
                            IpAddressTextBox.Text = localBindAddress; // Update UI

                            // Save if the determined address changed from the initial 0.0.0.0 placeholder
                            if (fetchAttempted && serverManager.CurrentServer.IpAddress != localBindAddress)
                            {
                                serverManager.CurrentServer.IpAddress = localBindAddress;
                                serverManager.SaveCurrentServerSettings();
                                LogStatus($"Saved bind address '{localBindAddress}' to profile '{serverManager.CurrentServer.Name}'.");
                            }
                        }
                        else
                        {
                            IpAddressTextBox.Text = "N/A";
                        }
                    });
                }
            }
            else
            {
                // IP was already set to something specific, just ensure UI reflects it
                LogStatus($"Using existing IP from profile: {serverManager.CurrentServer.IpAddress}");
                await Dispatcher.InvokeAsync(() =>
                {
                    if (serverManager.CurrentServer != null)
                    {
                        IpAddressTextBox.Text = serverManager.CurrentServer.IpAddress;
                    }
                    else
                    {
                        IpAddressTextBox.Text = "N/A";
                    }
                });
            }
        }

        /// <summary>
        /// Gets the recommended IP address for server binding. Returns "0.0.0.0" for typical
        /// private network setups, allowing the server to bind correctly. Can be adapted
        /// if a specific public/non-private IP is needed and available directly on an interface.
        /// </summary>
        /// <returns>Typically "0.0.0.0", or a specific non-private IPv4 if found and desired.</returns>
        private string GetLocalIPv4Address()
        {
            try
            {
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // Consider only operational interfaces (Up) and skip loopback/tunnel/etc.
                    if (netInterface.OperationalStatus == OperationalStatus.Up &&
                        (netInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                         netInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet)) // Add relevant types
                    {
                        var ipProps = netInterface.GetIPProperties();
                        foreach (var addrInfo in ipProps.UnicastAddresses)
                        {
                            // Focus on IPv4 addresses
                            if (addrInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                string ipString = addrInfo.Address.ToString();

                                // Standard private ranges - For these, binding to 0.0.0.0 is usually best practice
                                // unless you have a very specific multi-homed setup.
                                if (ipString.StartsWith("192.168.") ||
                                    ipString.StartsWith("10.") ||
                                    (ipString.StartsWith("172.") && IsInRange(ipString, "172.16.0.0", "172.31.255.255")))
                                {
                                    // Found a private IP. Return "0.0.0.0" to let ARK bind to all.
                                    return "0.0.0.0";
                                }
                                else if (!ipString.StartsWith("169.254.")) // Exclude APIPA addresses
                                {
                                    // Found a potentially public or non-standard IP directly on the interface.
                                    // You *could* return this IP if needed for a specific scenario,
                                    // but for general use, 0.0.0.0 is safer.
                                    // return ipString; // Uncomment this line ONLY if you need to bind to a specific non-private IP
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting local IP addresses: {ex.Message}");
            }

            // Default fallback: Let the server bind to all available interfaces.
            return "0.0.0.0";
        }


        /// <summary>
        /// Checks if an IPv4 address string is within a given range (inclusive).
        /// Assumes valid IPv4 strings are provided.
        /// </summary>
        private bool IsInRange(string ipAddress, string startIp, string endIp)
        {
            try // Add try-catch for robustness
            {
                long ipNum = BitConverter.ToUInt32(System.Net.IPAddress.Parse(ipAddress).GetAddressBytes().Reverse().ToArray(), 0);
                long startNum = BitConverter.ToUInt32(System.Net.IPAddress.Parse(startIp).GetAddressBytes().Reverse().ToArray(), 0);
                long endNum = BitConverter.ToUInt32(System.Net.IPAddress.Parse(endIp).GetAddressBytes().Reverse().ToArray(), 0);

                return ipNum >= startNum && ipNum <= endNum;
            }
            catch (FormatException)
            {
                Debug.WriteLine($"Error parsing IP address in IsInRange: ip={ipAddress}, start={startIp}, end={endIp}");
                return false; // Treat parse errors as not in range
            }
        }

        #region Server List Management Events

        /// <summary>
        /// Handles creating a new server profile.
        /// </summary>
        private void NewServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager == null) return; // Prevent action during load
            LogStatus("Creating new server profile...");
            serverManager.CreateNewServer(ServerList);
            LogStatus("New server profile created.");
            // The new server is automatically selected in CreateNewServer,
            // which will trigger ServerList_SelectionChanged to update the UI.
        }

        /// <summary>
        /// Handles deleting the selected server profile.
        /// </summary>
        private void DeleteServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager == null || ServerList.SelectedItem == null) return;

            string nameToDelete = ServerList.SelectedItem.ToString();
            var result = MessageBox.Show(
                $"Are you sure you want to delete the profile '{nameToDelete}'?\n\n" +
                "This action CANNOT be undone.\n" +
                "Server files and saved data will NOT be deleted.",
                "Confirm Profile Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No // Default to No
            );

            if (result == MessageBoxResult.Yes)
            {
                LogStatus($"Attempting to delete profile: {nameToDelete}");
                serverManager.DeleteServer(nameToDelete, ServerList);
                // If deletion was successful and items remain, SelectionChanged will update UI.
                // If list becomes empty, DeleteServer handles UI update internally.
                LogStatus($"Profile '{nameToDelete}' deleted.");
            }
        }

        /// <summary>
        /// Handles changes in the server list selection. This is the primary trigger for UI updates.
        /// </summary>
        private async void ServerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // IMPORTANT: Ignore selection changes during initial window loading
            if (isInitializing || serverManager == null)
            {
                // Check addedItems count as well, selection might fire briefly with null during init
                if (e.AddedItems.Count > 0)
                {
                    Debug.WriteLine($"ServerList_SelectionChanged ignored during initialization. Added: {e.AddedItems[0]}");
                }
                else
                {
                    Debug.WriteLine("ServerList_SelectionChanged ignored during initialization.");
                }
                return;
            }

            string selectedServerName = ServerList.SelectedItem?.ToString();

            if (!string.IsNullOrEmpty(selectedServerName))
            {
                // An item was selected (or re-selected)
                Debug.WriteLine($"ServerList_SelectionChanged: Handling selection of '{selectedServerName}'");
                LogStatus($"Selecting server: {selectedServerName}");

                // 1. Update the ServerManager's current server reference
                serverManager.SelectServer(selectedServerName);

                // 2. Update the entire UI based on the new selection
                // This should now correctly call settingsManager.UpdateUIFromSelection
                // because the controls were created *before* this event handler runs.
                UpdateUIFromSelection();

                // 3. Fetch public IP if necessary (after UI is updated with current data)
                await FetchPublicIpAndSetIfNeeded();

                LogStatus($"UI updated for server: {selectedServerName}");
            }
            else if (e.RemovedItems.Count > 0) // Check if an item was deselected and list is now empty
            {
                // Selection cleared (e.g., list became empty after delete)
                Debug.WriteLine("ServerList_SelectionChanged: Selection cleared.");
                LogStatus("Server selection cleared.");
                serverManager.SelectServer(null); // Ensure internal state is null
                UpdateUIFromSelection(); // Update UI to show empty/disabled state
            }
            // Else: No selection or invalid state, do nothing.
        }

        /// <summary>
        /// Handles adding an existing server installation to the profile list.
        /// Opens a folder browser dialog to select the server folder.
        /// </summary>
        private void AddExistingServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager == null) return;

            // Use WinForms FolderBrowserDialog for a simple folder picker
            using (var dlg = new Forms.FolderBrowserDialog())
            {
                dlg.Description = "Select an existing ARK server folder (contains ShooterGame\\Binaries\\Win64\\ArkAscendedServer.exe)";
                dlg.ShowNewFolderButton = false;

                var result = dlg.ShowDialog();
                if (result == Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
                {
                    LogStatus($"Attempting to add existing server from: {dlg.SelectedPath}");
                    bool added = serverManager.AddExistingServer(dlg.SelectedPath, ServerList);
                    if (added) LogStatus("Existing server added successfully.");
                    else LogStatus("Failed to add existing server. See console output for details.");
                }
            }
        }

        #endregion

        #region Core UI Update Logic

        /// <summary>
        /// Updates all UI elements based on the currently selected server in ServerManager.
        /// Should be called ONLY from the UI thread.
        /// </summary>
        public void UpdateUIFromSelection()
        {
            Dispatcher.InvokeAsync(() => // Use InvokeAsync for safety and responsiveness
            {
                if (serverManager == null || settingsManager == null)
                {
                    Debug.WriteLine("UpdateUIFromSelection skipped: Managers not ready.");
                    return;
                }

                bool serverIsSelected = serverManager.CurrentServer != null;
                ArkServer selectedServer = serverManager.CurrentServer; // Cache for readability
                string initStatus = isInitializing ? "YES" : "NO";
                Debug.WriteLine($"UpdateUIFromSelection executing. Server selected: {serverIsSelected} (Name: {selectedServer?.Name ?? "None"}), isInitializing: {initStatus}");

                try
                {
                    // --- Update Basic Server Info Controls ---
                    ServerNameTextBox.Text = serverIsSelected ? selectedServer.Name : "";
                    PlayerLimitTextBox.Text = serverIsSelected ? selectedServer.PlayerLimit.ToString() : "";
                    QueryPortTextBox.Text = serverIsSelected ? selectedServer.QueryPort.ToString() : "";
                    GamePortTextBox.Text = serverIsSelected ? selectedServer.GamePort.ToString() : "";
                    RconPortTextBox.Text = serverIsSelected ? selectedServer.RconPort.ToString() : "";
                    // IpAddressTextBox updated via FetchPublicIpAndSetIfNeeded or remains N/A initially
                    if (!serverIsSelected) IpAddressTextBox.Text = "N/A"; // Explicitly clear if no server selected

                    AdminPasswordTextBox.Text = serverIsSelected ? selectedServer.ServerSettings?.ServerAdminPassword ?? "" : "";
                    ServerPasswordTextBox.Text = serverIsSelected ? selectedServer.ServerSettings?.ServerPassword ?? "" : "";
                    RconEnabledCheckBox.IsChecked = serverIsSelected ? selectedServer.ServerSettings?.RCONEnabled ?? false : false;

                    // --- Update Map ComboBox ---
                    MapComboBox.IsEnabled = serverIsSelected;
                    if (serverIsSelected)
                    {
                        var mapItem = MapComboBox.Items.Cast<ComboBoxItem>()
                                     .FirstOrDefault(item => item.Content?.ToString().Equals(selectedServer.Map, StringComparison.OrdinalIgnoreCase) ?? false);
                        MapComboBox.SelectedItem = mapItem;
                        if (mapItem == null && !string.IsNullOrEmpty(selectedServer.Map))
                        {
                            Debug.WriteLine($"WARN: Map '{selectedServer.Map}' not found in MapComboBox items.");
                        }
                    }
                    else
                    {
                        MapComboBox.SelectedIndex = -1;
                    }

                    // --- Update Mod ListBox ---
                    serverManager.UpdateModListUI(ModListBox);

                    // --- Update Dynamic Settings Panels ---
                    // Removed the '!isInitializing' check here. Now that Initialize happens first,
                    // this call should always execute when selection changes.
                    Debug.WriteLine($"UpdateUI: Calling settingsManager.UpdateUIFromSelection for {selectedServer?.Name ?? "null"}");
                    settingsManager.UpdateUIFromSelection(); // This should now work correctly even for initial load selection

                    // --- Enable/Disable Controls Based on Selection & State ---
                    bool controlsEnabled = serverIsSelected;
                    bool serverIsRunning = serverIsSelected
                                            && selectedServer.RunningProcess != null
                                            && !selectedServer.RunningProcess.HasExited;

                    // Basic Info Tab
                    ServerNameTextBox.IsEnabled = controlsEnabled;
                    PlayerLimitTextBox.IsEnabled = controlsEnabled;
                    QueryPortTextBox.IsEnabled = controlsEnabled;
                    GamePortTextBox.IsEnabled = controlsEnabled;
                    RconPortTextBox.IsEnabled = controlsEnabled;
                    IpAddressTextBox.IsEnabled = controlsEnabled;
                    AdminPasswordTextBox.IsEnabled = controlsEnabled;
                    ServerPasswordTextBox.IsEnabled = controlsEnabled;
                    RconEnabledCheckBox.IsEnabled = controlsEnabled;
                    MapComboBox.IsEnabled = controlsEnabled;

                    // Mod Management
                    ModListBox.IsEnabled = controlsEnabled;
                    AddModButton.IsEnabled = controlsEnabled;
                    RemoveModButton.IsEnabled = controlsEnabled && ModListBox.SelectedItem != null;

                    // Server Actions
                    LaunchServerButton.IsEnabled = controlsEnabled && !serverIsRunning;
                    ValidateUpdateServerButton.IsEnabled = controlsEnabled && !serverIsRunning;
                    TerminateServerButton.IsEnabled = controlsEnabled && serverIsRunning;

                    // RCON
                    bool rconPossible = controlsEnabled && serverIsRunning && selectedServer?.ServerSettings?.RCONEnabled == true;
                    CommandInput.IsEnabled = rconPossible;
                    SendCommandButton.IsEnabled = rconPossible;

                    // Enable/Disable Settings Tabs
                    foreach (var item in MainTabControl.Items)
                    {
                        if (item is TabItem tab)
                        {
                            bool enableTab = serverIsSelected || tab.Header?.ToString() == "Console";
                            tab.IsEnabled = enableTab;
                        }
                    }

                    // Update Window Title
                    Title = $"ARK Server Manager{(serverIsSelected ? " - " + selectedServer.Name : "")}{(serverIsRunning ? " [Running]" : "")}";

                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ERROR during UpdateUIFromSelection: {ex.GetType().Name} - {ex.Message}\n{ex.StackTrace}");
                    LogStatus($"Error updating UI: {ex.Message}");
                }
                finally
                {
                    Debug.WriteLine("UpdateUIFromSelection finished execution.");
                }
            }); // End Dispatcher InvokeAsync
        }

        #endregion

        #region Basic Settings Event Handlers (Save on Change)

        // No changes needed in these methods (TextChanged, SelectionChanged, CheckBoxChanged)
        // ... (Keep existing code) ...
        /// <summary>
        /// Generic handler for TextChanged event on basic setting TextBoxes.
        /// </summary>
private void BasicSetting_TextChanged(object sender, TextChangedEventArgs e)
{
    if (isInitializing || serverManager?.CurrentServer == null) return; // Ignore during load or if no server

    var textBox = sender as TextBox;
    if (textBox == null) return;

    bool changed = false;
    string newValue = textBox.Text;
    var currentServer = serverManager.CurrentServer; // Cache reference

    try
    {
        switch (textBox.Name)
        {
            case nameof(ServerNameTextBox):
                // Ignore blank names
                if (string.IsNullOrWhiteSpace(newValue)) break;

                // Save caret/focus state so we can restore after ListBox update
                int caret = textBox.SelectionStart;
                bool hadFocus = textBox.IsFocused;

                // Keep the previous name for locating the ListBox item
                string oldName = currentServer.Name;

                if (!string.Equals(oldName, newValue, StringComparison.Ordinal))
                {
                    currentServer.Name = newValue;
                    changed = true;

                    // Update the ListBox item in-place on UI thread without stealing focus.
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        try
                        {
                            // Prefer using SelectedIndex when available (more robust than IndexOf oldName)
                            int index = ServerList.SelectedIndex;
                            if (index < 0)
                            {
                                index = ServerList.Items.IndexOf(oldName);
                            }

                            if (index >= 0 && index < ServerList.Items.Count)
                            {
                                ServerList.Items[index] = newValue;

                                // Re-select the item to keep it selected
                                ServerList.SelectedIndex = index;
                            }
                            else
                            {
                                // If we couldn't find the item by index/name, repopulate list entry for stability
                                // Find server object by InstallPath/name and rebuild list if needed (fallback)
                                // For now, ensure the selection remains on the same server object:
                                if (ServerList.Items.Contains(newValue))
                                {
                                    ServerList.SelectedItem = newValue;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error updating ServerList item: {ex.Message}");
                        }
                        finally
                        {
                            // Restore focus & caret to the TextBox to avoid losing typing ability
                            if (hadFocus)
                            {
                                // Use BeginInvoke to ensure focus restoration happens after ListBox changes
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    try
                                    {
                                        textBox.Focus();
                                        textBox.SelectionStart = Math.Min(caret, textBox.Text.Length);
                                    }
                                    catch { /* swallow */ }
                                }), System.Windows.Threading.DispatcherPriority.Input);
                            }
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }
                break;

            case nameof(PlayerLimitTextBox):
                if (int.TryParse(newValue, out int pl) && pl > 0 && currentServer.PlayerLimit != pl) { currentServer.PlayerLimit = pl; changed = true; }
                break;
            case nameof(QueryPortTextBox):
                if (ushort.TryParse(newValue, out ushort qp) && qp > 0 && currentServer.QueryPort != qp) { currentServer.QueryPort = qp; changed = true; }
                break;
            case nameof(GamePortTextBox):
                if (ushort.TryParse(newValue, out ushort gp) && gp > 0 && currentServer.GamePort != gp) { currentServer.GamePort = gp; changed = true; }
                break;
            case nameof(RconPortTextBox):
                if (ushort.TryParse(newValue, out ushort rp) && rp > 0 && currentServer.RconPort != rp) { currentServer.RconPort = rp; changed = true; }
                break;
            case nameof(IpAddressTextBox):
                if (currentServer.IpAddress != newValue) { currentServer.IpAddress = newValue; changed = true; }
                break;
            case nameof(AdminPasswordTextBox):
                if (currentServer.ServerSettings != null && currentServer.ServerSettings.ServerAdminPassword != newValue) { currentServer.ServerSettings.ServerAdminPassword = newValue; changed = true; }
                break;
            case nameof(ServerPasswordTextBox):
                if (currentServer.ServerSettings != null && currentServer.ServerSettings.ServerPassword != newValue) { currentServer.ServerSettings.ServerPassword = newValue; changed = true; }
                break;
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"Error handling TextChanged for {textBox.Name}: {ex.Message}");
    }

    if (changed)
    {
        serverManager.SaveCurrentServerSettings(); // Persist the change
    }
}

        /// <summary>
        /// Generic handler for SelectionChanged event on basic setting ComboBoxes.
        /// </summary>
        private void BasicSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;

            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem == null) return; // Ensure an item is actually selected

            bool changed = false;
            var currentServer = serverManager.CurrentServer;

            try
            {
                if (comboBox.Name == nameof(MapComboBox))
                {
                    // Get the content of the selected ComboBoxItem
                    string selectedMap = (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (selectedMap != null && currentServer.Map != selectedMap)
                    {
                        currentServer.Map = selectedMap;
                        changed = true;
                    }
                }
                // Add other ComboBoxes here if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling SelectionChanged for {comboBox.Name}: {ex.Message}");
            }

            if (changed)
            {
                serverManager.SaveCurrentServerSettings();
            }
        }

        /// <summary>
        /// Generic handler for Checked/Unchecked events on basic setting CheckBoxes.
        /// </summary>
        private void BasicSetting_CheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;

            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            bool changed = false;
            bool isChecked = checkBox.IsChecked ?? false; // Safely get boolean value
            var currentServer = serverManager.CurrentServer;

            try
            {
                if (checkBox.Name == nameof(RconEnabledCheckBox))
                {
                    if (currentServer.ServerSettings != null && currentServer.ServerSettings.RCONEnabled != isChecked)
                    {
                        currentServer.ServerSettings.RCONEnabled = isChecked;
                        changed = true;
                        // Immediately update dependent controls (RCON input/button)
                        bool serverIsRunning = currentServer.RunningProcess != null && !currentServer.RunningProcess.HasExited;
                        CommandInput.IsEnabled = isChecked && serverIsRunning;
                        SendCommandButton.IsEnabled = isChecked && serverIsRunning;
                    }
                }
                // Add other CheckBoxes here if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling CheckBoxChanged for {checkBox.Name}: {ex.Message}");
            }

            if (changed)
            {
                serverManager.SaveCurrentServerSettings();
            }
        }
        #endregion

        #region Mod Management Events
        // No changes needed in these methods (AddMod_Click, RemoveMod_Click, ModListBox_SelectionChanged)
        // ... (Keep existing code) ...
        /// <summary>
        /// Opens a dialog to add a new Mod ID.
        /// </summary>
        private void AddMod_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null)
            {
                MessageBox.Show(this, "Please select a server profile first.", "No Server Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // --- Simple Input Dialog ---
            // Consider using a dedicated custom dialog window for better UI/UX if needed.
            var dialog = new Window
            {
                Title = "Add Mod ID",
                SizeToContent = SizeToContent.WidthAndHeight, // Auto size
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, // Make it modal to this window
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)), // Match theme
                Foreground = Brushes.White,
                WindowStyle = WindowStyle.ToolWindow, // Simple window style
                ResizeMode = ResizeMode.NoResize // Prevent resizing
            };

            var mainPanel = new StackPanel { Margin = new Thickness(15) };
            mainPanel.Children.Add(new Label { Content = "Enter Numeric Mod ID:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 5) });
            var modInputTextBox = new TextBox { Name = "ModInput", MinWidth = 200, Background = new SolidColorBrush(Color.FromRgb(97, 97, 97)), Foreground = Brushes.White };
            mainPanel.Children.Add(modInputTextBox);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            var addButton = new Button { Content = "Add", MinWidth = 60, Margin = new Thickness(0, 0, 10, 0), IsDefault = true }; // IsDefault enables Enter key
            var cancelButton = new Button { Content = "Cancel", MinWidth = 60, IsCancel = true }; // IsCancel enables Esc key

            addButton.Click += (s, args) => {
                string inputText = modInputTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(inputText))
                {
                    MessageBox.Show(dialog, "Mod ID cannot be empty.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    modInputTextBox.Focus();
                    return;
                }
                if (long.TryParse(inputText, out _)) // Validate if numeric
                {
                    serverManager.AddMod(inputText); // AddMod handles saving and UI list update
                    dialog.DialogResult = true; // Indicate success
                    dialog.Close();
                }
                else
                {
                    MessageBox.Show(dialog, $"Invalid Mod ID: '{inputText}'.\nMod IDs must be numeric.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error);
                    modInputTextBox.Focus();
                    modInputTextBox.SelectAll();
                }
            };

            // Cancel button click is handled by IsCancel=true setting DialogResult to false and closing.

            buttonPanel.Children.Add(addButton);
            buttonPanel.Children.Add(cancelButton);
            mainPanel.Children.Add(buttonPanel);
            dialog.Content = mainPanel;

            // Set focus after content is set
            dialog.Loaded += (s, args) => modInputTextBox.Focus();

            // Show modally and wait for result
            dialog.ShowDialog();

            // No explicit UI update needed here, AddMod calls the necessary helper.
        }


        /// <summary>
        /// Removes the selected mod from the list.
        /// </summary>
        private void RemoveMod_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null || ModListBox.SelectedItem == null) return;

            string modToRemove = ModListBox.SelectedItem.ToString();
            LogStatus($"Removing mod ID: {modToRemove}");
            serverManager.RemoveMod(modToRemove); // RemoveMod handles saving and UI list update

            // Explicitly disable button after remove as selection might linger briefly
            // RemoveModButton.IsEnabled = false; // Let UpdateModListUI handle this
        }

        /// <summary>
        /// Updates the enabled state of the Remove Mod button based on selection.
        /// </summary>
        private void ModListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Enable button only if a server is selected AND a mod is selected in the list
            RemoveModButton.IsEnabled = serverManager.CurrentServer != null && ModListBox.SelectedItem != null;
        }
        #endregion

        #region Server Action Events
        // No changes needed in these methods (Launch, Validate, Terminate)
        // ... (Keep existing code) ...
        /// <summary>
        /// Handles launching the currently selected server.
        /// </summary>
        private async void LaunchServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;
            LogStatus($"Attempting to launch server: {serverManager.CurrentServer.Name}");
            await serverManager.LaunchServer(); // ServerManager handles logging/errors
                                                // LaunchServer internally triggers UI update on start/fail via dispatcher in process handling
                                                // UpdateUIFromSelection(); // Re-sync UI after attempt (redundant if process events work)
        }

        /// <summary>
        /// Handles validating/updating the files for the currently selected server.
        /// </summary>
        private async void ValidateUpdateServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;
            LogStatus($"Starting validation/update for: {serverManager.CurrentServer.Name}");
            // Disable button during operation?
            ValidateUpdateServerButton.IsEnabled = false;
            await serverManager.ValidateUpdateServer(serverManager.CurrentServer);
            ValidateUpdateServerButton.IsEnabled = true; // Re-enable
            LogStatus($"Validation/update process finished for: {serverManager.CurrentServer.Name}");
            // UpdateUIFromSelection(); // Refresh UI state (button enables etc)
        }

        /// <summary>
        /// Handles terminating the currently running server process.
        /// </summary>
        private void TerminateServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;
            LogStatus($"Attempting to terminate server: {serverManager.CurrentServer.Name}");
            serverManager.TerminateServer(); // ServerManager handles logging/errors
            // TerminateServer internally triggers UI update via dispatcher in process handling (Exited event)
            // UpdateUIFromSelection(); // Re-sync UI immediately (redundant if process events work)
        }
        #endregion

        #region RCON Events
        // No changes needed in these methods (SendCommand, KeyDown)
        // ... (Keep existing code) ...
        /// <summary>
        /// Handles sending the command from the RCON input box.
        /// </summary>
        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null || string.IsNullOrWhiteSpace(CommandInput.Text)) return;

            string command = CommandInput.Text.Trim();
            LogStatus($"Sending RCON command: {command}");
            serverManager.SendCommand(command); // ServerManager handles checks and execution (placeholder)

            // Clear input box and keep focus for next command
            CommandInput.Text = "";
            CommandInput.Focus();
        }

        /// <summary>
        /// Allows sending RCON command by pressing Enter in the input box.
        /// </summary>
        private void CommandInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SendCommandButton.IsEnabled) // Check if sending is possible
            {
                SendCommand_Click(sender, e); // Trigger the button click logic
                e.Handled = true; // Prevent further processing of the Enter key (like newline)
            }
        }
        #endregion

        #region Window Closing
        // No changes needed in this method
        // ... (Keep existing code) ...
        /// <summary>
        /// Handles actions needed before the main window closes.
        /// </summary>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("MainWindow Closing event triggered.");
            LogStatus("Shutting down...");

            // Optional: Check if a server is running and ask for confirmation
            bool serverIsRunning = serverManager.CurrentServer != null
                                 && serverManager.CurrentServer.RunningProcess != null
                                 && !serverManager.CurrentServer.RunningProcess.HasExited;
            // More robust check: `_serverProcess` in ServerManager

            if (serverIsRunning)
            {
                var result = MessageBox.Show(
                     $"The server '{serverManager.CurrentServer.Name}' appears to be running.\n\n" +
                     "Closing the manager will NOT automatically stop the server process.\n\n" +
                     "Do you want to close the manager anyway?",
                     "Server Running",
                     MessageBoxButton.YesNo,
                     MessageBoxImage.Warning,
                     MessageBoxResult.No);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true; // Prevent the window from closing
                    LogStatus("Shutdown cancelled by user.");
                    Debug.WriteLine("Window closing cancelled because server is running.");
                    return;
                }
                // If Yes, proceed with closing. Maybe attempt termination?
                // serverManager.TerminateServer(); // Optionally terminate on close
            }

            // Ensure profiles are saved one last time
            LogStatus("Saving profiles before exit...");
            serverManager.SaveServerProfiles();
            LogStatus("Shutdown complete.");

            // No need to call base.OnClosing(e); for Window event handlers
        }
        #endregion

        private void ModMove_Click(object sender, RoutedEventArgs e) 
        { 
            if (serverManager.CurrentServer == null || ModListBox.SelectedItem == null) 
                return;

            var selectedMod = ModListBox.SelectedItem as string; int currentIndex = ModListBox.SelectedIndex; var modsList = serverManager.CurrentServer.Mods;
            if (selectedMod == null || modsList == null || currentIndex < 0) 
                return;

            string direction = (sender as Button)?.Tag as string; int newIndex = -1;

            if (direction == "Up" && currentIndex > 0) 
            { 
                newIndex = currentIndex - 1;
            } 
            else if (direction == "Down" && currentIndex < modsList.Count - 1) 
            { 
                newIndex = currentIndex + 1;
            } 
            if (newIndex != -1)
            { 
                modsList.RemoveAt(currentIndex); modsList.Insert(newIndex, selectedMod); 
                serverManager.UpdateModListUI(ModListBox); ModListBox.SelectedIndex = newIndex; 
                serverManager.SaveCurrentServerSettings(); LogStatus($"Moved Mod ID {selectedMod} " +
                    $"{direction.ToLower()}."); UpdateModMoveButtons(); 
            } 
        }

        private void UpdateModMoveButtons() 
        { 
            bool modSelected = ModListBox.SelectedItem != null;
            int selectedIndex = ModListBox.SelectedIndex;
            int totalItems = ModListBox.Items.Count;
            ModUpButton.IsEnabled = modSelected && selectedIndex > 0; 
            ModDownButton.IsEnabled = modSelected && selectedIndex < totalItems - 1;
        }

        private void ModListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) 
        { 
            if (ModListBox.SelectedItem is string selectedModId && !string.IsNullOrWhiteSpace(selectedModId)) 
            { 
                try 
                { 
                    Clipboard.SetText(selectedModId);
                    LogStatus($"Copied Mod ID {selectedModId} to clipboard.");
                } 
                catch (Exception ex) 
                { 
                    LogStatus($"Error copying Mod ID to clipboard: {ex.Message}");
                    MessageBox.Show(this, $"Failed to copy to clipboard:\n{ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                } 
            } 
        }
    } // End class MainWindow
}