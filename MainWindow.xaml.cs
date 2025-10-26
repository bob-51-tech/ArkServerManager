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
using Forms = System.Windows.Forms;
using System.IO; // Added using alias for Windows Forms

namespace ArkServerManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Routed commands for context menu actions (used by XAML via x:Static)
        public static readonly RoutedUICommand DuplicateServerCommand = new RoutedUICommand("DuplicateServer", "DuplicateServer", typeof(MainWindow));
        public static readonly RoutedUICommand RemoveServerCommand = new RoutedUICommand("RemoveServer", "RemoveServer", typeof(MainWindow));
        public static readonly RoutedUICommand OpenInstallFolderCommand = new RoutedUICommand("OpenInstallFolder", "OpenInstallFolder", typeof(MainWindow));

        private readonly ServerManager serverManager;
        private readonly SettingsManager settingsManager;
        private readonly ThemeManager themeManager;
        public bool isInitializing = true; // Flag to prevent event handlers during setup // Made public for SettingsManager check

        public MainWindow()
        {
            InitializeComponent();
            // Register command bindings for context menu commands
            CommandBindings.Add(new CommandBinding(DuplicateServerCommand, OnDuplicateExecuted, OnDuplicateCanExecute));
            CommandBindings.Add(new CommandBinding(RemoveServerCommand, OnRemoveExecuted, OnRemoveCanExecute));
            CommandBindings.Add(new CommandBinding(OpenInstallFolderCommand, OnOpenInstallExecuted, OnOpenInstallCanExecute));
            serverManager = new ServerManager(ConsoleOutput); // Pass console output TextBox
            settingsManager = new SettingsManager(serverManager, GameSettingsPanel, UserSettingsPanel, ServerSettingsPanel);

            // The ThemeManager is instantiated here for use by the UI,
            // but the initial theme is now applied in App.xaml.cs before this window is even created.
            themeManager = new ThemeManager();

            Loaded += MainWindow_Loaded; // Subscribe to Loaded event
            Closing += MainWindow_Closing; // Subscribe to Closing event
        }

        // --- THEME UI HANDLERS ---

        private void EditTheme_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeListBox.SelectedItem == null) return;
            string name = ThemeListBox.SelectedItem.ToString();
            var win = new ThemeEditorWindow(themeManager, name) { Owner = this };
            win.ShowDialog();
            // FIX: No need to reapply here. The live update is handled by the corrected
            // ThemeManager.SaveThemeColors method, which is called from the editor window.
        }

        private void ApplyTheme_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeListBox.SelectedItem == null) return;
            string name = ThemeListBox.SelectedItem.ToString();
            if (themeManager.ApplyTheme(name))
            {
                LogStatus($"Applied theme: {name}");
            }
            else
            {
                MessageBox.Show(this, $"Failed to apply theme: {name}", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveTheme_Click(object sender, RoutedEventArgs e)
        {
            string name = PromptForThemeName();
            if (string.IsNullOrWhiteSpace(name)) return;
            if (themeManager.SaveCurrentColorsAsTheme(name))
            {
                if (!ThemeListBox.Items.Contains(name))
                {
                    ThemeListBox.Items.Add(name);
                }
                // Select the newly created theme
                ThemeListBox.SelectedItem = name;
                LogStatus($"Saved theme: {name}");
            }
            else
            {
                MessageBox.Show(this, $"Failed to save theme: {name}", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTheme_Click(object sender, RoutedEventArgs e)
        {
            if (ThemeListBox.SelectedItem == null) return;
            string name = ThemeListBox.SelectedItem.ToString();

            // Add check to prevent deleting the default theme
            if (name.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "The 'default' theme cannot be deleted.", "Action Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var res = MessageBox.Show(this, $"Delete theme '{name}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;

            if (themeManager.DeleteTheme(name))
            {
                ThemeListBox.Items.Remove(name);
                LogStatus($"Deleted theme: {name}");
            }
            else
            {
                MessageBox.Show(this, $"Failed to delete theme: {name}", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string PromptForThemeName()
        {
            var dlg = new Window { Title = "Theme Name", SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this };
            var sp = new StackPanel { Margin = new Thickness(10) };
            sp.Children.Add(new Label { Content = "Enter theme name:" });
            var tb = new TextBox { MinWidth = 200 };
            sp.Children.Add(tb);
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var ok = new Button { Content = "OK", IsDefault = true, Margin = new Thickness(5) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(5) };
            ok.Click += (s, e) => { dlg.DialogResult = true; dlg.Close(); };
            btnPanel.Children.Add(ok); btnPanel.Children.Add(cancel);
            sp.Children.Add(btnPanel);
            dlg.Content = sp;
            if (dlg.ShowDialog() == true) return tb.Text.Trim();
            return null;
        }

        // Command handlers for the context menu commands
        private void OnDuplicateCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = serverManager != null && ServerList?.SelectedItem != null;
        }

        private void OnDuplicateExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            DuplicateServerContext_Click(sender, new RoutedEventArgs());
        }

        private void OnRemoveCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = serverManager != null && ServerList?.SelectedItem != null;
        }

        private void OnRemoveExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            RemoveServerContext_Click(sender, new RoutedEventArgs());
        }

        private void OnOpenInstallCanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = serverManager != null && ServerList?.SelectedItem != null;
        }

        private void OnOpenInstallExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            OpenServerFolderContext_Click(sender, new RoutedEventArgs());
        }

        /// <summary>
        /// Handles initialization tasks after the window is fully loaded.
        /// </summary>
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine("MainWindow Loaded event started.");
            isInitializing = true; // Ensure flag is set at the start

            try
            {
                // 1. Initialize Theme UI
                // The theme is already applied by App.xaml.cs. Here we just load the list of themes.
                try
                {
                    ThemeListBox.Items.Clear();
                    foreach (var t in themeManager.ListThemes())
                    {
                        ThemeListBox.Items.Add(t);
                    }
                    var active = themeManager.GetActiveThemeName();
                    if (!string.IsNullOrEmpty(active) && ThemeListBox.Items.Contains(active))
                    {
                        ThemeListBox.SelectedItem = active;
                    }
                }
                catch (Exception ex)
                {
                    LogStatus($"Error initializing theme list: {ex.Message}");
                }

                // 2. Init Manager
                LogStatus("Initializing manager...");
                await serverManager.InitializeAsync();
                LogStatus("Manager initialized.");

                // 3. Init UI Panel Structure
                LogStatus("Initializing UI panel structure...");
                settingsManager.InitializeGameSettingsPanel();
                settingsManager.InitializeServerSettingsPanel();
                settingsManager.InitializeUserSettingsPanel();
                LogStatus("UI panel structure initialized.");

                // 4. Load Profiles
                LogStatus("Loading server profiles...");
                serverManager.LoadServerProfiles(ServerList); // Populates ServerList.Items
                LogStatus("Server profiles loading process finished.");

                // 5. Select First Item *if* profiles exist
                if (ServerList.Items.Count > 0)
                {
                    LogStatus($"Profiles loaded. Selecting first server internally: {ServerList.Items[0]}");
                    ServerList.SelectedIndex = 0;
                    serverManager.SelectServer(ServerList.Items[0].ToString());
                    LogStatus("Performing initial basic UI update...");
                    UpdateUIFromSelection();
                    LogStatus("Performing initial SettingsManager UI update...");
                    settingsManager.UpdateUIFromSelection();
                    LogStatus("Initial SettingsManager UI update complete.");

                    await FetchPublicIpAndSetIfNeeded();
                }
                else
                {
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
        private async Task FetchPublicIpAndSetIfNeeded()
        {
            if (serverManager?.CurrentServer == null)
            {
                await Dispatcher.InvokeAsync(() => IpAddressTextBox.Text = "N/A");
                return;
            }

            if (string.IsNullOrWhiteSpace(serverManager.CurrentServer.IpAddress) || serverManager.CurrentServer.IpAddress == "0.0.0.0")
            {
                LogStatus("Current IP is 0.0.0.0, attempting to determine best local bind address...");
                string localBindAddress = "0.0.0.0";
                bool fetchAttempted = false;

                try
                {
                    fetchAttempted = true;
                    await Dispatcher.InvokeAsync(() => IpAddressTextBox.Text = "Determining...");

                    localBindAddress = GetLocalIPv4Address();

                    if (string.IsNullOrWhiteSpace(localBindAddress))
                    {
                        LogStatus($"Failed to determine local bind address. Using default 0.0.0.0.");
                        localBindAddress = "0.0.0.0";
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
                    await Dispatcher.InvokeAsync(() =>
                    {
                        if (serverManager.CurrentServer != null)
                        {
                            IpAddressTextBox.Text = localBindAddress;

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

        private string GetLocalIPv4Address()
        {
            try
            {
                var candidates = new System.Collections.Generic.List<string>();
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus != OperationalStatus.Up) continue;
                    if (netInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        netInterface.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                        continue;

                    var ipProps = netInterface.GetIPProperties();
                    foreach (var addrInfo in ipProps.UnicastAddresses)
                    {
                        if (addrInfo.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ipString = addrInfo.Address.ToString();
                        if (string.IsNullOrWhiteSpace(ipString)) continue;
                        if (ipString.StartsWith("169.254.")) continue;
                        if (ipString.StartsWith("127.")) continue;
                        candidates.Add(ipString);
                    }
                }

                string[] virtualIndicators = new[] { "virtual", "vmware", "vbox", "hyper-v", "vethernet", "docker", "nat", "loopback", "tunnel", "virtualbox", "vmnet" };
                var physicalCandidates = new System.Collections.Generic.List<string>();
                var otherCandidates = new System.Collections.Generic.List<string>();

                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus != OperationalStatus.Up) continue;
                    var ipProps = netInterface.GetIPProperties();
                    foreach (var addrInfo in ipProps.UnicastAddresses)
                    {
                        if (addrInfo.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        string ipString = addrInfo.Address.ToString();
                        if (string.IsNullOrWhiteSpace(ipString) || ipString.StartsWith("169.254.") || ipString.StartsWith("127.")) continue;

                        string nameLower = (netInterface.Name ?? "").ToLowerInvariant();
                        string descLower = (netInterface.Description ?? "").ToLowerInvariant();
                        bool looksVirtual = virtualIndicators.Any(v => nameLower.Contains(v) || descLower.Contains(v));

                        if (!looksVirtual && (netInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet || netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || netInterface.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet))
                        {
                            physicalCandidates.Add(ipString);
                        }
                        else
                        {
                            otherCandidates.Add(ipString);
                        }
                    }
                }

                Func<string, bool> isPrivate = ip => ip.StartsWith("192.168.") || ip.StartsWith("10.") || (ip.StartsWith("172.") && IsInRange(ip, "172.16.0.0", "172.31.255.255"));
                var privateFromPhysical = physicalCandidates.FirstOrDefault(isPrivate);
                if (!string.IsNullOrEmpty(privateFromPhysical)) return privateFromPhysical;
                if (physicalCandidates.Any()) return physicalCandidates[0];
                var privateFromOther = otherCandidates.FirstOrDefault(isPrivate);
                if (!string.IsNullOrEmpty(privateFromOther)) return privateFromOther;
                if (otherCandidates.Any()) return otherCandidates[0];
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting local IP addresses: {ex.Message}");
            }
            return "0.0.0.0";
        }

        private bool IsInRange(string ipAddress, string startIp, string endIp)
        {
            try
            {
                long ipNum = BitConverter.ToUInt32(System.Net.IPAddress.Parse(ipAddress).GetAddressBytes().Reverse().ToArray(), 0);
                long startNum = BitConverter.ToUInt32(System.Net.IPAddress.Parse(startIp).GetAddressBytes().Reverse().ToArray(), 0);
                long endNum = BitConverter.ToUInt32(System.Net.IPAddress.Parse(endIp).GetAddressBytes().Reverse().ToArray(), 0);
                return ipNum >= startNum && ipNum <= endNum;
            }
            catch (FormatException)
            {
                Debug.WriteLine($"Error parsing IP address in IsInRange: ip={ipAddress}, start={startIp}, end={endIp}");
                return false;
            }
        }

        #region Server List Management Events

        private void NewServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager == null) return;
            LogStatus("Creating new server profile...");
            serverManager.CreateNewServer(ServerList);
            LogStatus("New server profile created.");
        }

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
                MessageBoxResult.No
            );

            if (result == MessageBoxResult.Yes)
            {
                LogStatus($"Attempting to delete profile: {nameToDelete}");
                serverManager.DeleteServer(nameToDelete, ServerList);
                LogStatus($"Profile '{nameToDelete}' deleted.");
            }
        }

        private async void ServerList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || serverManager == null)
            {
                if (e.AddedItems.Count > 0)
                    Debug.WriteLine($"ServerList_SelectionChanged ignored during initialization. Added: {e.AddedItems[0]}");
                else
                    Debug.WriteLine("ServerList_SelectionChanged ignored during initialization.");
                return;
            }

            string selectedServerName = ServerList.SelectedItem?.ToString();

            if (!string.IsNullOrEmpty(selectedServerName))
            {
                Debug.WriteLine($"ServerList_SelectionChanged: Handling selection of '{selectedServerName}'");
                LogStatus($"Selecting server: {selectedServerName}");
                serverManager.SelectServer(selectedServerName);
                UpdateUIFromSelection();
                await FetchPublicIpAndSetIfNeeded();
                LogStatus($"UI updated for server: {selectedServerName}");
            }
            else if (e.RemovedItems.Count > 0)
            {
                Debug.WriteLine("ServerList_SelectionChanged: Selection cleared.");
                LogStatus("Server selection cleared.");
                serverManager.SelectServer(null);
                UpdateUIFromSelection();
            }
        }

        private void AddExistingServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager == null) return;

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

        private void SettingsPanel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var sv = FindParentScrollViewer(sender as DependencyObject);
            if (sv != null)
            {
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }

        private ScrollViewer FindParentScrollViewer(DependencyObject start)
        {
            DependencyObject cur = start;
            while (cur != null)
            {
                if (cur is ScrollViewer sv) return sv;
                cur = System.Windows.Media.VisualTreeHelper.GetParent(cur);
            }
            return null;
        }

        #region Core UI Update Logic
        public void UpdateUIFromSelection()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (serverManager == null || settingsManager == null)
                {
                    Debug.WriteLine("UpdateUIFromSelection skipped: Managers not ready.");
                    return;
                }

                bool serverIsSelected = serverManager.CurrentServer != null;
                ArkServer selectedServer = serverManager.CurrentServer;
                string initStatus = isInitializing ? "YES" : "NO";
                Debug.WriteLine($"UpdateUIFromSelection executing. Server selected: {serverIsSelected} (Name: {selectedServer?.Name ?? "None"}), isInitializing: {initStatus}");

                try
                {
                    ServerNameTextBox.Text = serverIsSelected ? selectedServer.Name : "";
                    PlayerLimitTextBox.Text = serverIsSelected ? selectedServer.PlayerLimit.ToString() : "";
                    QueryPortTextBox.Text = serverIsSelected ? selectedServer.QueryPort.ToString() : "";
                    GamePortTextBox.Text = serverIsSelected ? selectedServer.GamePort.ToString() : "";
                    RconPortTextBox.Text = serverIsSelected ? selectedServer.RconPort.ToString() : "";
                    if (!serverIsSelected) IpAddressTextBox.Text = "N/A";

                    AdminPasswordTextBox.Text = serverIsSelected ? selectedServer.ServerSettings?.ServerAdminPassword ?? "" : "";
                    ServerPasswordTextBox.Text = serverIsSelected ? selectedServer.ServerSettings?.ServerPassword ?? "" : "";
                    RconEnabledCheckBox.IsChecked = serverIsSelected ? selectedServer.ServerSettings?.RCONEnabled ?? false : false;

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

                    serverManager.UpdateModListUI(ModListBox);

                    Debug.WriteLine($"UpdateUI: Calling settingsManager.UpdateUIFromSelection for {selectedServer?.Name ?? "null"}");
                    settingsManager.UpdateUIFromSelection();

                    bool controlsEnabled = serverIsSelected;
                    bool serverIsRunning = serverIsSelected && selectedServer.RunningProcess != null && !selectedServer.RunningProcess.HasExited;

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

                    ModListBox.IsEnabled = controlsEnabled;
                    AddModButton.IsEnabled = controlsEnabled;
                    RemoveModButton.IsEnabled = controlsEnabled && ModListBox.SelectedItem != null;

                    LaunchServerButton.IsEnabled = controlsEnabled && !serverIsRunning;
                    ValidateUpdateServerButton.IsEnabled = controlsEnabled && !serverIsRunning;
                    TerminateServerButton.IsEnabled = controlsEnabled && serverIsRunning;

                    bool rconPossible = controlsEnabled && serverIsRunning && selectedServer?.ServerSettings?.RCONEnabled == true;
                    CommandInput.IsEnabled = rconPossible;
                    SendCommandButton.IsEnabled = rconPossible;

                    foreach (var item in MainTabControl.Items)
                    {
                        if (item is TabItem tab)
                        {
                            bool enableTab = serverIsSelected || tab.Header?.ToString() == "Console" || tab.Header?.ToString() == "Themes";
                            tab.IsEnabled = enableTab;
                        }
                    }

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
            });
        }
        #endregion

        #region Basic Settings Event Handlers
        private void BasicSetting_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing || serverManager?.CurrentServer == null) return;
            var textBox = sender as TextBox;
            if (textBox == null) return;
            bool changed = false;
            string newValue = textBox.Text;
            var currentServer = serverManager.CurrentServer;
            try
            {
                switch (textBox.Name)
                {
                    case nameof(ServerNameTextBox):
                        if (string.IsNullOrWhiteSpace(newValue)) break;
                        int caret = textBox.SelectionStart;
                        bool hadFocus = textBox.IsFocused;
                        string oldName = currentServer.Name;
                        if (!string.Equals(oldName, newValue, StringComparison.Ordinal))
                        {
                            currentServer.Name = newValue;
                            changed = true;
                            _ = Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    int index = ServerList.SelectedIndex;
                                    if (index < 0) index = ServerList.Items.IndexOf(oldName);
                                    if (index >= 0 && index < ServerList.Items.Count)
                                    {
                                        ServerList.Items[index] = newValue;
                                        ServerList.SelectedIndex = index;
                                    }
                                    else if (ServerList.Items.Contains(newValue))
                                    {
                                        ServerList.SelectedItem = newValue;
                                    }
                                }
                                catch (Exception ex) { Debug.WriteLine($"Error updating ServerList item: {ex.Message}"); }
                                finally
                                {
                                    if (hadFocus)
                                    {
                                        Dispatcher.BeginInvoke(new Action(() =>
                                        {
                                            try { textBox.Focus(); textBox.SelectionStart = Math.Min(caret, textBox.Text.Length); }
                                            catch { /* swallow */ }
                                        }), System.Windows.Threading.DispatcherPriority.Input);
                                    }
                                }
                            }, System.Windows.Threading.DispatcherPriority.Background);
                        }
                        break;
                    case nameof(PlayerLimitTextBox): if (int.TryParse(newValue, out int pl) && pl > 0 && currentServer.PlayerLimit != pl) { currentServer.PlayerLimit = pl; changed = true; } break;
                    case nameof(QueryPortTextBox): if (ushort.TryParse(newValue, out ushort qp) && qp > 0 && currentServer.QueryPort != qp) { currentServer.QueryPort = qp; changed = true; } break;
                    case nameof(GamePortTextBox): if (ushort.TryParse(newValue, out ushort gp) && gp > 0 && currentServer.GamePort != gp) { currentServer.GamePort = gp; changed = true; } break;
                    case nameof(RconPortTextBox): if (ushort.TryParse(newValue, out ushort rp) && rp > 0 && currentServer.RconPort != rp) { currentServer.RconPort = rp; changed = true; } break;
                    case nameof(IpAddressTextBox): if (currentServer.IpAddress != newValue) { currentServer.IpAddress = newValue; changed = true; } break;
                    case nameof(AdminPasswordTextBox): if (currentServer.ServerSettings != null && currentServer.ServerSettings.ServerAdminPassword != newValue) { currentServer.ServerSettings.ServerAdminPassword = newValue; changed = true; } break;
                    case nameof(ServerPasswordTextBox): if (currentServer.ServerSettings != null && currentServer.ServerSettings.ServerPassword != newValue) { currentServer.ServerSettings.ServerPassword = newValue; changed = true; } break;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error handling TextChanged for {textBox.Name}: {ex.Message}"); }
            if (changed) serverManager.SaveCurrentServerSettings();
        }

        private void BasicSetting_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;
            var comboBox = sender as ComboBox;
            if (comboBox?.SelectedItem == null) return;
            bool changed = false;
            var currentServer = serverManager.CurrentServer;
            try
            {
                if (comboBox.Name == nameof(MapComboBox))
                {
                    string selectedMap = (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                    if (selectedMap != null && currentServer.Map != selectedMap)
                    {
                        currentServer.Map = selectedMap;
                        changed = true;
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error handling SelectionChanged for {comboBox.Name}: {ex.Message}"); }
            if (changed) serverManager.SaveCurrentServerSettings();
        }

        private void BasicSetting_CheckBoxChanged(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;
            bool changed = false;
            bool isChecked = checkBox.IsChecked ?? false;
            var currentServer = serverManager.CurrentServer;
            try
            {
                if (checkBox.Name == nameof(RconEnabledCheckBox))
                {
                    if (currentServer.ServerSettings != null && currentServer.ServerSettings.RCONEnabled != isChecked)
                    {
                        currentServer.ServerSettings.RCONEnabled = isChecked;
                        changed = true;
                        bool serverIsRunning = currentServer.RunningProcess != null && !currentServer.RunningProcess.HasExited;
                        CommandInput.IsEnabled = isChecked && serverIsRunning;
                        SendCommandButton.IsEnabled = isChecked && serverIsRunning;
                    }
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Error handling CheckBoxChanged for {checkBox.Name}: {ex.Message}"); }
            if (changed) serverManager.SaveCurrentServerSettings();
        }
        #endregion

        #region Mod Management Events
        private void AddMod_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null)
            {
                MessageBox.Show(this, "Please select a server profile first.", "No Server Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var dialog = new Window { Title = "Add Mod ID", SizeToContent = SizeToContent.WidthAndHeight, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this, Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)), Foreground = Brushes.White, WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize };
            var mainPanel = new StackPanel { Margin = new Thickness(15) };
            mainPanel.Children.Add(new Label { Content = "Enter Numeric Mod ID:", Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 5) });
            var modInputTextBox = new TextBox { Name = "ModInput", MinWidth = 200, Background = new SolidColorBrush(Color.FromRgb(97, 97, 97)), Foreground = Brushes.White };
            mainPanel.Children.Add(modInputTextBox);
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
            var addButton = new Button { Content = "Add", MinWidth = 60, Margin = new Thickness(0, 0, 10, 0), IsDefault = true };
            var cancelButton = new Button { Content = "Cancel", MinWidth = 60, IsCancel = true };
            addButton.Click += (s, args) =>
            {
                string inputText = modInputTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(inputText)) { MessageBox.Show(dialog, "Mod ID cannot be empty.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Warning); modInputTextBox.Focus(); return; }
                if (long.TryParse(inputText, out _)) { serverManager.AddMod(inputText); dialog.DialogResult = true; dialog.Close(); }
                else { MessageBox.Show(dialog, $"Invalid Mod ID: '{inputText}'.\nMod IDs must be numeric.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Error); modInputTextBox.Focus(); modInputTextBox.SelectAll(); }
            };
            buttonPanel.Children.Add(addButton); buttonPanel.Children.Add(cancelButton); mainPanel.Children.Add(buttonPanel); dialog.Content = mainPanel;
            dialog.Loaded += (s, args) => modInputTextBox.Focus();
            dialog.ShowDialog();
        }
        private void RemoveMod_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null || ModListBox.SelectedItem == null) return;
            string modToRemove = ModListBox.SelectedItem.ToString();
            LogStatus($"Removing mod ID: {modToRemove}");
            serverManager.RemoveMod(modToRemove);
        }
        private void ModListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RemoveModButton.IsEnabled = serverManager.CurrentServer != null && ModListBox.SelectedItem != null;
            UpdateModMoveButtons();
        }
        #endregion

        #region Server Action Events
        private async void LaunchServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;
            LogStatus($"Attempting to launch server: {serverManager.CurrentServer.Name}");
            await serverManager.LaunchServer();
        }
        private async void ValidateUpdateServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;
            LogStatus($"Starting validation/update for: {serverManager.CurrentServer.Name}");
            ValidateUpdateServerButton.IsEnabled = false;
            await serverManager.ValidateUpdateServer(serverManager.CurrentServer);
            ValidateUpdateServerButton.IsEnabled = true;
            LogStatus($"Validation/update process finished for: {serverManager.CurrentServer.Name}");
        }
        private void TerminateServer_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null) return;
            LogStatus($"Attempting to terminate server: {serverManager.CurrentServer.Name}");
            serverManager.TerminateServer();
        }
        #endregion

        #region RCON Events
        private void SendCommand_Click(object sender, RoutedEventArgs e)
        {
            if (isInitializing || serverManager.CurrentServer == null || string.IsNullOrWhiteSpace(CommandInput.Text)) return;
            string command = CommandInput.Text.Trim();
            LogStatus($"Sending RCON command: {command}");
            serverManager.SendCommand(command);
            CommandInput.Text = "";
            CommandInput.Focus();
        }
        private void CommandInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SendCommandButton.IsEnabled)
            {
                SendCommand_Click(sender, e);
                e.Handled = true;
            }
        }
        #endregion

        #region Window Closing
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("MainWindow Closing event triggered.");
            LogStatus("Shutting down...");
            bool serverIsRunning = serverManager.CurrentServer != null && serverManager.CurrentServer.RunningProcess != null && !serverManager.CurrentServer.RunningProcess.HasExited;
            if (serverIsRunning)
            {
                var result = MessageBox.Show($"The server '{serverManager.CurrentServer.Name}' appears to be running.\n\n" + "Closing the manager will NOT automatically stop the server process.\n\n" + "Do you want to close the manager anyway?", "Server Running", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
                if (result == MessageBoxResult.No) { e.Cancel = true; LogStatus("Shutdown cancelled by user."); Debug.WriteLine("Window closing cancelled because server is running."); return; }
            }
            LogStatus("Saving profiles before exit...");
            serverManager.SaveServerProfiles();
            LogStatus("Shutdown complete.");
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
            if (direction == "Up" && currentIndex > 0) { newIndex = currentIndex - 1; }
            else if (direction == "Down" && currentIndex < modsList.Count - 1) { newIndex = currentIndex + 1; }
            if (newIndex != -1)
            {
                modsList.RemoveAt(currentIndex); modsList.Insert(newIndex, selectedMod);
                serverManager.UpdateModListUI(ModListBox); ModListBox.SelectedIndex = newIndex;
                serverManager.SaveCurrentServerSettings(); LogStatus($"Moved Mod ID {selectedMod} {direction.ToLower()}."); UpdateModMoveButtons();
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

        #region Context Menu Handlers
        private void DuplicateServerContext_Click(object sender, RoutedEventArgs e)
        {
            if (serverManager == null || ServerList.SelectedItem == null) return;
            string origName = ServerList.SelectedItem.ToString();
            ArkServer sourceServer = null;
            try
            {
                var serversField = typeof(ServerManager).GetField("_servers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var list = serversField?.GetValue(serverManager) as System.Collections.IList;
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item is ArkServer asrv && asrv.Name.Equals(origName, StringComparison.OrdinalIgnoreCase)) { sourceServer = asrv; break; }
                    }
                }
            }
            catch { }
            if (sourceServer == null) { MessageBox.Show(this, "Could not locate source server to duplicate.", "Duplicate Failed", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var copy = new ArkServer
            {
                Name = GetUniqueServerName(sourceServer.Name),
                InstallPath = sourceServer.InstallPath + "_copy",
                GamePort = sourceServer.GamePort,
                QueryPort = sourceServer.QueryPort,
                RconPort = sourceServer.RconPort,
                PlayerLimit = sourceServer.PlayerLimit,
                Map = sourceServer.Map,
                Mods = sourceServer.Mods != null ? new System.Collections.Generic.List<string>(sourceServer.Mods) : new System.Collections.Generic.List<string>(),
                GameSettings = sourceServer.GameSettings,
                UserSettings = sourceServer.UserSettings,
                ServerSettings = sourceServer.ServerSettings
            };
            try
            {
                var serversField = typeof(ServerManager).GetField("_servers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var list = serversField?.GetValue(serverManager) as System.Collections.IList;
                list?.Add(copy);
                serverManager.SaveServerProfiles();
                ServerList.Items.Add(copy.Name);
            }
            catch (Exception ex) { MessageBox.Show(this, $"Failed to duplicate server: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); return; }
        }

        private string GetUniqueServerName(string baseName)
        {
            string name = baseName + " Copy";
            int i = 1;
            while (ServerList.Items.Contains(name))
            {
                i++;
                name = baseName + " Copy " + i;
            }
            return name;
        }

        private void RemoveServerContext_Click(object sender, RoutedEventArgs e)
        {
            if (serverManager == null || ServerList.SelectedItem == null) return;
            string nameToDelete = ServerList.SelectedItem.ToString();
            var result = MessageBox.Show(this, $"Are you sure you want to delete the profile '{nameToDelete}'?\nThis will NOT delete server files.", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                serverManager.DeleteServer(nameToDelete, ServerList);
            }
        }

        private void OpenServerFolderContext_Click(object sender, RoutedEventArgs e)
        {
            if (serverManager == null || ServerList.SelectedItem == null) return;
            string name = ServerList.SelectedItem.ToString();
            ArkServer server = null;
            try
            {
                var serversField = typeof(ServerManager).GetField("_servers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var list = serversField?.GetValue(serverManager) as System.Collections.IList;
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item is ArkServer asrv && asrv.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { server = asrv; break; }
                    }
                }
            }
            catch { }
            if (server == null || string.IsNullOrWhiteSpace(server.InstallPath) || !Directory.Exists(server.InstallPath))
            {
                MessageBox.Show(this, "Install folder not found for selected server.", "Open Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = server.InstallPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to open folder: {ex.Message}", "Open Folder Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

    }
}