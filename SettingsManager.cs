using System.Diagnostics;
using System.Globalization; // For CultureInfo and NumberStyles
using System.Reflection;
using System.Text; // For StringBuilder used in FormatDisplayName
using System.Windows;
using System.Windows.Controls;

namespace ArkServerManager
{
    /// <summary>
    /// Manages the dynamic creation and population of UI controls for server settings
    /// based on the properties of GameSettings, UserSettings, and ServerSettings classes.
    /// Handles updating the UI when the server selection changes and saving changes back
    /// to the selected server's profile.
    /// </summary>
    public class SettingsManager
    {
        private readonly ServerManager _serverManager;
        private readonly StackPanel _gameSettingsPanel;
        private readonly StackPanel _userSettingsPanel;
        private readonly StackPanel _serverSettingsPanel;

        // Cache default instances for resetting values efficiently
        private readonly GameSettings _defaultGameSettings = new GameSettings();
        private readonly ServerSettings _defaultServerSettings = new ServerSettings();
        private readonly UserSettings _defaultUserSettings = new UserSettings();

        // Style definitions for controls
      //  private static readonly SolidColorBrush TextBoxBackground = (SolidColorBrush)Application.Current.Resources["BackgroundBrush2"];
      //  private static readonly SolidColorBrush ForegroundBrush = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"];
      //  private static readonly SolidColorBrush LabelForeground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"];
      //  private static readonly SolidColorBrush SectionLabelForeground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"];
      //  private static readonly SolidColorBrush ResetButtonBackground = (SolidColorBrush)Application.Current.Resources["BackgroundBrush2"];
      //  private static readonly SolidColorBrush ResetButtonForeground = (SolidColorBrush)Application.Current.Resources["ForegroundBrush"];
      //  private static readonly SolidColorBrush SeparatorBrush = Brushes.Gray;
        private const double ControlMinWidth = 80;
        private const double TextControlMinWidth = 150;
        private static readonly Thickness ControlMargin = new Thickness(0, 0, 0, 5); // Margin below each setting row
        private static readonly Thickness ControlPropertyMargin = new Thickness(5, 0,5, 0); // Margin below each setting row
        private static readonly Thickness ArrayControlMargin = new Thickness(0, 0, 0, 2); // Tighter margin for array items
        private static readonly Thickness SectionLabelMargin = new Thickness(0, 15, 0, 5); // Margin around section labels
        private static readonly Thickness SeparatorMargin = new Thickness(0, 10, 0, 5); // Margin around separators


        /// <summary>
        /// Internal helper class to store information needed for the reset button functionality.
        /// </summary>
        private class ResetInfo
        {
            public string PropertyName { get; } // Can be "PropName" or "ArrayPropName_Index"
            public Type SettingsType { get; }    // GameSettings, ServerSettings, or UserSettings
            public FrameworkElement AssociatedControl { get; } // The UI control (TextBox, CheckBox)

            public ResetInfo(string propertyName, Type settingsType, FrameworkElement associatedControl)
            {
                PropertyName = propertyName;
                SettingsType = settingsType;
                AssociatedControl = associatedControl;
            }
        }

        public SettingsManager(ServerManager serverManager, StackPanel gameSettingsPanel, StackPanel userSettingsPanel, StackPanel serverSettingsPanel)
        {
            _serverManager = serverManager ?? throw new ArgumentNullException(nameof(serverManager));
            _gameSettingsPanel = gameSettingsPanel ?? throw new ArgumentNullException(nameof(gameSettingsPanel));
            _userSettingsPanel = userSettingsPanel ?? throw new ArgumentNullException(nameof(userSettingsPanel));
            _serverSettingsPanel = serverSettingsPanel ?? throw new ArgumentNullException(nameof(serverSettingsPanel));
        }

        #region UI Initialization

        /// <summary>
        /// Sets up the basic structure (ScrollViewer + inner StackPanel) for a settings panel.
        /// </summary>
        private ScrollViewer InitializePanelStructure(StackPanel panel)
        {
            panel.Children.Clear();
            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled // Usually not needed
            };
            var innerPanel = new StackPanel { Margin = new Thickness(5) }; // Add some padding inside the scroll area
            scrollViewer.Content = innerPanel;
            panel.Children.Add(scrollViewer);
            return scrollViewer;
        }

        /// <summary>
        /// Initializes the Game Settings panel structure and populates it with controls.
        /// </summary>
        public void InitializeGameSettingsPanel() // Removed SelectionChangedEventHandler param - not used here
        {
            var scrollViewer = InitializePanelStructure(_gameSettingsPanel);
            var innerPanel = (StackPanel)scrollViewer.Content;
            if (innerPanel == null) return; // Should not happen

            // Define stat names for clarity - must match array order in GameSettings
            var playerStatNames = new[] { "Health", "Stamina", "Torpidity", "Oxygen", "Food", "Water", "Temperature", "Weight", "MeleeDamage", "Speed", "Fortitude", "CraftingSpeed" };
            var dinoStatNames = new[] { "Health", "Stamina", "Torpidity", "Oxygen", "Food", "Water", "Temperature", "Weight", "MeleeDamage", "Speed", "Fortitude" }; // No CraftingSpeed

            // Add Array Settings first, grouped by section
            AddArraySettingsSection(innerPanel, nameof(GameSettings.PerLevelStatsMultiplierPlayer), "Player Per-Level Stats Multipliers", typeof(GameSettings), playerStatNames);
            AddArraySettingsSection(innerPanel, nameof(GameSettings.PerLevelStatsMultiplierDinoWild), "Wild Dino Per-Level Stats Multipliers", typeof(GameSettings), dinoStatNames);
            AddArraySettingsSection(innerPanel, nameof(GameSettings.PerLevelStatsMultiplierDinoTamed), "Tamed Dino Per-Level Stats Multipliers (Base Gain)", typeof(GameSettings), dinoStatNames);
            AddArraySettingsSection(innerPanel, nameof(GameSettings.PerLevelStatsMultiplierDinoTamed_Add), "Tamed Dino Per-Level Stats Multipliers (Additive)", typeof(GameSettings), dinoStatNames);
            AddArraySettingsSection(innerPanel, nameof(GameSettings.PerLevelStatsMultiplierDinoTamed_Affinity), "Tamed Dino Per-Level Stats Multipliers (Imprint Bonus)", typeof(GameSettings), dinoStatNames);

            AddSeparator(innerPanel);

            // Add remaining non-array settings
            AddSettingsFromType(innerPanel, typeof(GameSettings), "Other Game Settings");
        }

        /// <summary>
        /// Initializes the Server Settings panel structure and populates it with controls.
        /// </summary>
        public void InitializeServerSettingsPanel() // Removed SelectionChangedEventHandler param
        {
            var scrollViewer = InitializePanelStructure(_serverSettingsPanel);
            var innerPanel = (StackPanel)scrollViewer.Content;
            if (innerPanel == null) return;
            AddSettingsFromType(innerPanel, typeof(ServerSettings), "Server Settings");
        }

        /// <summary>
        /// Initializes the User Settings panel structure and populates it with controls.
        /// </summary>
        public void InitializeUserSettingsPanel() // Removed SelectionChangedEventHandler param
        {
            var scrollViewer = InitializePanelStructure(_userSettingsPanel);
            var innerPanel = (StackPanel)scrollViewer.Content;
            if (innerPanel == null) return;
            AddSettingsFromType(innerPanel, typeof(UserSettings), "User Interface Settings");
        }

        #endregion

        #region Dynamic Control Creation Helpers

        /// <summary>
        /// Adds a visual separator to the panel.
        /// </summary>
        private void AddSeparator(StackPanel panel)
        {
            //panel.Children.Add(new Separator { Margin = SeparatorMargin, Background = SeparatorBrush });
            panel.Children.Add(new Separator { Margin = SeparatorMargin });
        }

        /// <summary>
        /// Adds a section header label to the panel.
        /// </summary>
        private void AddSectionLabel(StackPanel panel, string content)
        {
            var label = new Label
            {
                Content = content,
                //Foreground = SectionLabelForeground,
                FontWeight = FontWeights.Bold,
                Margin = SectionLabelMargin,
                Padding = new Thickness(0)
            };
            panel.Children.Add(label);
        }

        /// <summary>
        /// Adds a standard Label control to a parent Grid (typically column 0).
        /// </summary>
        private Label AddSettingLabel(Grid parentGrid, string content)
        {
            var label = new Label
            {
                Content = content + ":",
                //Foreground = LabelForeground,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0) // Right margin to separate from control
            };
            Grid.SetColumn(label, 0);
            parentGrid.Children.Add(label);
            return label;
        }

        /// <summary>
        /// Creates a standard 3-column grid for holding a setting's label, control, and reset button.
        /// </summary>
        private Grid CreateSettingItemGrid()
        {
            var itemGrid = new Grid { Margin = ControlMargin };
            // Column 0: Label (auto width)
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 100 }); // Ensure some minimum space for labels
                                                                                                              // Column 1: Control (takes remaining space)
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = ControlMinWidth });
            // Column 2: Reset Button (auto width)
            itemGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            return itemGrid;
        }

        /// <summary>
        /// Adds controls for all non-array public properties of a given settings type.
        /// </summary>
        private void AddSettingsFromType(StackPanel panel, Type settingsType, string sectionTitle)
        {
            AddSectionLabel(panel, sectionTitle);

            var properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Where(p => p.CanRead && p.CanWrite) // Only get properties we can interact with
                                      .OrderBy(p => p.Name); // Alphabetical order

            foreach (var prop in properties)
            {
                // Skip array properties (handled separately)
                if (prop.PropertyType.IsArray) continue;

                // Skip properties managed elsewhere (e.g., basic settings tab or launch args)
                if (settingsType == typeof(ServerSettings) &&
                   (prop.Name == nameof(ServerSettings.ServerAdminPassword) ||
                    prop.Name == nameof(ServerSettings.ServerPassword) ||
                    prop.Name == nameof(ServerSettings.RCONEnabled) ||
                    prop.Name == nameof(ServerSettings.ActiveMods))) // ActiveMods is handled via launch args
                {
                    continue;
                }

                string displayName = FormatDisplayName(prop.Name);
                string controlName = prop.Name; // Use property name for control name/linking

                var itemGrid = CreateSettingItemGrid();
                AddSettingLabel(itemGrid, displayName);

                FrameworkElement settingControl = null;

                // Create the appropriate control based on property type
                if (prop.PropertyType == typeof(float))
                {
                    settingControl = CreateFloatSettingControl(itemGrid, prop, controlName, settingsType);
                }
                else if (prop.PropertyType == typeof(int))
                {
                    settingControl = CreateIntSettingControl(itemGrid, prop, controlName, settingsType);
                }
                else if (prop.PropertyType == typeof(bool))
                {
                    settingControl = CreateBoolSettingControl(itemGrid, prop, controlName, settingsType);
                }
                else if (prop.PropertyType == typeof(string))
                {
                    settingControl = CreateTextSettingControl(itemGrid, prop, controlName, settingsType);
                }
                // Add more types (enums -> ComboBox?) if needed later

                // If a control was created, add the reset button and add the grid to the panel
                if (settingControl != null)
                {
                    AddResetButton(itemGrid, controlName, settingsType, settingControl);
                    panel.Children.Add(itemGrid);
                }
            }
        }


        /// <summary>
        /// Adds a section with controls for an array property (e.g., PerLevelStatsMultiplier).
        /// </summary>
        private void AddArraySettingsSection(StackPanel panel, string propertyName, string groupDisplayName, Type settingsType, string[] itemNames)
        {
            var property = settingsType.GetProperty(propertyName);
            if (property == null || !property.PropertyType.IsArray)
            {
                Debug.WriteLine($"WARN: Array property '{propertyName}' not found or not an array in type '{settingsType.Name}'. Cannot add array section.");
                return;
            }
            Type elementType = property.PropertyType.GetElementType();
            if (elementType != typeof(float)) // Currently only supports float arrays
            {
                Debug.WriteLine($"WARN: Array property '{propertyName}' has unsupported element type '{elementType.Name}'. Only float arrays supported currently.");
                return;
            }


            AddSectionLabel(panel, groupDisplayName);

            // Ensure itemNames length matches expected array size (use default instance to check)
            var defaultInstance = GetDefaultSettingsObject(settingsType);
            int expectedLength = (property.GetValue(defaultInstance) as Array)?.Length ?? 0;

            if (itemNames.Length != expectedLength)
            {
                Debug.WriteLine($"WARN: Mismatch between itemNames provided ({itemNames.Length}) and expected array length ({expectedLength}) for '{propertyName}'. UI might be incomplete or incorrect.");
                // Use the smaller count to avoid index out of bounds errors
                expectedLength = Math.Min(itemNames.Length, expectedLength);
            }


            for (int i = 0; i < expectedLength; i++)
            {
                int index = i; // Capture index for lambda
                string itemName = itemNames[i]; // Name for the label (e.g., "Health", "Stamina")
                string controlName = $"{propertyName}_{index}"; // Unique name like "PerLevelStatsMultiplierPlayer_0"

                var itemGrid = CreateSettingItemGrid();
                itemGrid.Margin = ArrayControlMargin; // Use tighter margin

                // Adjust column widths for array items if needed (e.g., smaller labels)
                // itemGrid.ColumnDefinitions[0].MinWidth = 80;

                AddSettingLabel(itemGrid, itemName); // Use the specific item name

                //align textBox to the right within its cell ********
                // Create control (assuming float for now)
                var textBox = new TextBox
                {

                    Name = controlName,
                    MinWidth = ControlMinWidth, // Standard width for consistency
                    MaxWidth = 100, // Prevent excessive width
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = ControlPropertyMargin,
                    // Background = TextBoxBackground,
                    //Foreground = ForegroundBrush
                };

                // --- Event Handler for Value Change ---
                textBox.TextChanged += (s, e) =>
                {
                    // Only process if a server is selected
                    if (_serverManager.CurrentServer == null) return;

                    // Try parsing the new value using invariant culture
                    if (float.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out float newValue))
                    {
                        object settingsObj = GetCurrentSettingsObject(settingsType);
                        if (settingsObj == null) return; // Should not happen if CurrentServer is not null

                        try
                        {
                            var array = property.GetValue(settingsObj) as float[]; // Assuming float array
                            if (array != null && index >= 0 && index < array.Length)
                            {
                                // Only update and save if the value actually changed (avoids redundant saves)
                                if (Math.Abs(array[index] - newValue) > float.Epsilon) // Use Epsilon for float comparison
                                {
                                    array[index] = newValue;
                                    _serverManager.SaveCurrentServerSettings(); // Save changes immediately
                                                                                // Optionally provide visual feedback (e.g., brief background flash)
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"ERROR setting array value for {controlName}: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Handle invalid input (e.g., set background red, show tooltip)
                        // For now, just ignore non-float input, it won't be saved.
                    }
                };
                // --- End Event Handler ---

                // Set initial value (handled by UpdateUIFromSelection)
                Grid.SetColumn(textBox, 1);
                itemGrid.Children.Add(textBox);

                // Add reset button for this array item
                AddResetButton(itemGrid, controlName, settingsType, textBox);

                panel.Children.Add(itemGrid);
            }
        }

        /// <summary>
        /// Creates a TextBox control for float settings.
        /// </summary>
        private TextBox CreateFloatSettingControl(Grid parentGrid, PropertyInfo property, string controlName, Type settingsType)
        {
            //align textBox to the right within its cell ********
            var textBox = new TextBox
            {
                Name = controlName,
                MinWidth = ControlMinWidth,
                HorizontalAlignment = HorizontalAlignment.Right, // Align left within the grid cell
                VerticalAlignment = VerticalAlignment.Center,
                Margin = ControlPropertyMargin,
                //Background = TextBoxBackground,
                // Foreground = ForegroundBrush
            };

            textBox.TextChanged += (s, e) =>
            {
                if (_serverManager.CurrentServer != null &&
                    float.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out float newValue))
                {
                    object currentValueObj = GetCurrentPropertyValue(settingsType, property);
                    if (currentValueObj is float currentFloatValue)
                    {
                        if (Math.Abs(currentFloatValue - newValue) > float.Epsilon)
                        {
                            SetCurrentPropertyValue(settingsType, property, newValue);
                        }
                    }
                    else
                    {
                        // Handle case where current value couldn't be retrieved or wasn't float
                        SetCurrentPropertyValue(settingsType, property, newValue);
                    }
                }
            };

            Grid.SetColumn(textBox, 1);
            parentGrid.Children.Add(textBox);
            return textBox;
        }

        /// <summary>
        /// Creates a TextBox control for integer settings.
        /// </summary>
        private TextBox CreateIntSettingControl(Grid parentGrid, PropertyInfo property, string controlName, Type settingsType)
        {
            //align textBox to the right within its cell ********
            var textBox = new TextBox
            {
                Name = controlName,
                MinWidth = ControlMinWidth,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = ControlPropertyMargin,
                //Background = TextBoxBackground,
                // Foreground = ForegroundBrush
            };

            // Add PreviewTextInput handler to allow only digits (and potentially negative sign)
            textBox.PreviewTextInput += (s, e) =>
            {
                // Allow digits and potentially a leading '-' if not already present
                bool isDigit = e.Text.All(char.IsDigit);
                bool isNegativeSign = e.Text == "-" && textBox.SelectionStart == 0 && !textBox.Text.Contains('-');
                e.Handled = !(isDigit || isNegativeSign);
            };
            // Also handle pasting non-numeric values (less critical but good practice)
            DataObject.AddPastingHandler(textBox, (s, e) => {
                if (e.DataObject.GetDataPresent(typeof(string)))
                {
                    string text = (string)e.DataObject.GetData(typeof(string));
                    if (!int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
                    {
                        e.CancelCommand();
                    }
                }
                else
                {
                    e.CancelCommand();
                }
            });


            textBox.TextChanged += (s, e) =>
            {
                if (_serverManager.CurrentServer != null &&
                    int.TryParse(textBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out int newValue))
                {
                    object currentValueObj = GetCurrentPropertyValue(settingsType, property);
                    if (currentValueObj is int currentIntValue)
                    {
                        if (currentIntValue != newValue)
                        {
                            SetCurrentPropertyValue(settingsType, property, newValue);
                        }
                    }
                    else
                    {
                        SetCurrentPropertyValue(settingsType, property, newValue);
                    }
                }
            };

            Grid.SetColumn(textBox, 1);
            parentGrid.Children.Add(textBox);
            return textBox;
        }

        /// <summary>
        /// Creates a CheckBox control for boolean settings.
        /// </summary>
        private CheckBox CreateBoolSettingControl(Grid parentGrid, PropertyInfo property, string controlName, Type settingsType)
        {

            //align checkbox to the right within its cell ********
            var checkBox = new CheckBox
            {
                Name = controlName,
                Content = "", // Label is in column 0
                //Foreground = ForegroundBrush,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right, // Align checkbox itself
                Margin = ControlPropertyMargin // Indent slightly from left edge
            };

            // Use a single handler for both Checked and Unchecked events
            RoutedEventHandler handler = (s, e) =>
            {
                // Check if the application is shutting down or server is null
                if (Application.Current == null || Application.Current.MainWindow == null || _serverManager.CurrentServer == null) return;

                // Also check if the window itself is initializing (using the public flag from MainWindow)
                if (Application.Current.MainWindow is MainWindow mw && mw.isInitializing)
                {
                    Debug.WriteLine($"CheckBox '{controlName}' change ignored during initialization.");
                    return;
                }


                // Get current value before setting
                object currentValueObj = GetCurrentPropertyValue(settingsType, property);
                bool newValue = checkBox.IsChecked ?? false; // Get new value from checkbox

                if (currentValueObj is bool currentBoolValue)
                {
                    if (currentBoolValue != newValue) // Only save if changed
                    {
                        Debug.WriteLine($"CheckBox '{controlName}' changed. Old: {currentBoolValue}, New: {newValue}. Saving...");
                        SetCurrentPropertyValue(settingsType, property, newValue);
                    }
                }
                else
                { // If current value wasn't bool (e.g., null), set it
                    Debug.WriteLine($"CheckBox '{controlName}' changed (initial?). New: {newValue}. Saving...");
                    SetCurrentPropertyValue(settingsType, property, newValue);
                }
            };

            checkBox.Checked += handler;
            checkBox.Unchecked += handler;

            Grid.SetColumn(checkBox, 1);
            parentGrid.Children.Add(checkBox);
            return checkBox;
        }

        /// <summary>
        /// Creates a TextBox control for string settings.
        /// </summary>
        private TextBox CreateTextSettingControl(Grid parentGrid, PropertyInfo property, string controlName, Type settingsType)
        {
            var textBox = new TextBox
            {
                Name = controlName,
                MinWidth = TextControlMinWidth,
                HorizontalAlignment = HorizontalAlignment.Stretch, // Allow text box to fill space
                VerticalAlignment = VerticalAlignment.Center,
                Margin = ControlPropertyMargin,
                // Background = TextBoxBackground,
                // Foreground = ForegroundBrush,
                AcceptsReturn = false, // Single line input typically
                MaxLength = 256 // Reasonable limit? Adjust as needed
            };

            textBox.TextChanged += (s, e) =>
            {
                if (_serverManager.CurrentServer != null)
                {
                    object currentValueObj = GetCurrentPropertyValue(settingsType, property);
                    string currentStringValue = (currentValueObj as string) ?? string.Empty;
                    string newStringValue = textBox.Text;

                    if (currentStringValue != newStringValue) // Simple string comparison
                    {
                        SetCurrentPropertyValue(settingsType, property, newStringValue);
                    }
                }
            };

            Grid.SetColumn(textBox, 1);
            parentGrid.Children.Add(textBox);
            return textBox;
        }

        /// <summary>
        /// Adds a reset button to the parent grid (column 2).
        /// </summary>
        private void AddResetButton(Grid parentGrid, string propertyOrControlName, Type settingsType, FrameworkElement associatedControl)
        {
            var resetButton = new Button
            {
                Content = "↺", // Reset symbol
                ToolTip = "Reset value to default",
                FontWeight = FontWeights.Bold,
                Width = 25,
                Height = 22, // Slightly larger hit target
                Margin = new Thickness(5, 0, 5, 0), // Left margin
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                //Background = ResetButtonBackground,
               // Foreground = ResetButtonForeground,
                BorderThickness = new Thickness(1),
               // BorderBrush = SeparatorBrush, // Match separator color
                Focusable = false, // Don't take focus from main control
                Tag = new ResetInfo(propertyOrControlName, settingsType, associatedControl) // Store reset info
            };

            resetButton.Click += ResetButton_Click; // Attach click handler

            Grid.SetColumn(resetButton, 2);
            parentGrid.Children.Add(resetButton);
        }

        #endregion

        #region Value Handling and UI Updates

        /// <summary>
        /// Handles the click event for all reset buttons.
        /// </summary>
        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_serverManager.CurrentServer == null) return; // No server selected

            var button = sender as Button;
            var resetInfo = button?.Tag as ResetInfo;
            if (resetInfo == null)
            {
                Debug.WriteLine("ERROR: Reset button clicked but ResetInfo tag was missing or invalid.");
                return;
            }

            // Get the current settings object instance for the selected server
            object currentSettingsObj = GetCurrentSettingsObject(resetInfo.SettingsType);
            // Get the default settings object instance (cached)
            object defaultSettingsObj = GetDefaultSettingsObject(resetInfo.SettingsType);

            if (currentSettingsObj == null || defaultSettingsObj == null)
            {
                Debug.WriteLine($"ERROR: Could not retrieve current or default settings object for type '{resetInfo.SettingsType.Name}' during reset.");
                return;
            }

            bool isArrayItem = resetInfo.PropertyName.Contains("_");
            PropertyInfo property = null;
            int arrayIndex = -1;
            string basePropertyName = resetInfo.PropertyName;

            try
            {
                // Determine if it's an array item and parse index if so
                if (isArrayItem)
                {
                    var parts = resetInfo.PropertyName.Split('_');
                    if (parts.Length >= 2 && !string.IsNullOrEmpty(parts[0]) && int.TryParse(parts[parts.Length - 1], out arrayIndex))
                    {
                        // Handle potential property names with underscores (less common)
                        basePropertyName = string.Join("_", parts.Take(parts.Length - 1));
                    }
                    else
                    {
                        Debug.WriteLine($"ERROR: Invalid array property name format for reset: '{resetInfo.PropertyName}'");
                        return;
                    }
                }

                // Get the PropertyInfo object
                property = resetInfo.SettingsType.GetProperty(basePropertyName);
                if (property == null)
                {
                    Debug.WriteLine($"ERROR: Property '{basePropertyName}' not found in type '{resetInfo.SettingsType.Name}' for reset.");
                    return;
                }

                // Get the default value
                object defaultValue = null;
                if (isArrayItem && arrayIndex >= 0)
                {
                    var defaultArray = property.GetValue(defaultSettingsObj) as Array;
                    if (defaultArray != null && arrayIndex < defaultArray.Length)
                    {
                        defaultValue = defaultArray.GetValue(arrayIndex);
                    }
                }
                else if (!isArrayItem)
                {
                    defaultValue = property.GetValue(defaultSettingsObj);
                }

                if (defaultValue == null && property.PropertyType != typeof(string)) // Allow null default for strings
                {
                    Debug.WriteLine($"WARN: Could not retrieve default value for '{resetInfo.PropertyName}'. Reset aborted.");
                    return;
                }

                // Get the current value
                object currentValue = null;
                if (isArrayItem && arrayIndex >= 0)
                {
                    currentValue = GetCurrentArrayPropertyValue(resetInfo.SettingsType, property, arrayIndex);
                }
                else if (!isArrayItem)
                {
                    currentValue = GetCurrentPropertyValue(resetInfo.SettingsType, property);
                }


                // --- Compare and Set Value ---
                bool valueChanged = false;
                if (!Equals(currentValue, defaultValue)) // Use object.Equals for safe comparison
                {
                    if (isArrayItem && arrayIndex >= 0)
                    {
                        var currentArray = property.GetValue(currentSettingsObj) as Array;
                        if (currentArray != null && arrayIndex < currentArray.Length)
                        {
                            currentArray.SetValue(defaultValue, arrayIndex); // Set the value in the array
                            valueChanged = true;
                        }
                    }
                    else if (!isArrayItem && property.CanWrite)
                    {
                        property.SetValue(currentSettingsObj, defaultValue); // Set the value of the property
                        valueChanged = true;
                    }
                }


                // If the value was changed, update the UI control and save settings
                if (valueChanged)
                {
                    UpdateAssociatedControl(resetInfo.AssociatedControl, defaultValue);
                    _serverManager.SaveCurrentServerSettings(); // Persist the change
                    Debug.WriteLine($"Reset '{resetInfo.PropertyName}' to default value: {defaultValue ?? "null"}");
                }
                else
                {
                    Debug.WriteLine($"Value for '{resetInfo.PropertyName}' is already at default. No reset needed.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR resetting property '{resetInfo.PropertyName}': {ex.Message}");
                MessageBox.Show($"Failed to reset '{FormatDisplayName(basePropertyName)}':\n{ex.Message}", "Reset Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// Gets the relevant settings object (Game, User, Server) for the CURRENTLY selected server.
        /// </summary>
        private object GetCurrentSettingsObject(Type settingsType)
        {
            if (_serverManager.CurrentServer == null) return null;
            if (settingsType == typeof(GameSettings)) return _serverManager.CurrentServer.GameSettings;
            if (settingsType == typeof(ServerSettings)) return _serverManager.CurrentServer.ServerSettings;
            if (settingsType == typeof(UserSettings)) return _serverManager.CurrentServer.UserSettings;

            Debug.WriteLine($"WARN: Unknown settings type requested in GetCurrentSettingsObject: {settingsType.Name}");
            return null;
        }

        /// <summary>
        /// Gets the cached default settings object instance.
        /// </summary>
        private object GetDefaultSettingsObject(Type settingsType)
        {
            if (settingsType == typeof(GameSettings)) return _defaultGameSettings;
            if (settingsType == typeof(ServerSettings)) return _defaultServerSettings;
            if (settingsType == typeof(UserSettings)) return _defaultUserSettings;

            Debug.WriteLine($"WARN: Unknown default settings type requested in GetDefaultSettingsObject: {settingsType.Name}");
            return null;
        }

        /// <summary>
        /// Gets the value of a specific property from the current server's settings object.
        /// </summary>
        private object GetCurrentPropertyValue(Type settingsType, PropertyInfo property)
        {
            object settingsObj = GetCurrentSettingsObject(settingsType);
            if (settingsObj != null && property != null && property.CanRead)
            {
                try { return property.GetValue(settingsObj); }
                catch (Exception ex) { Debug.WriteLine($"ERROR getting property value for '{property.Name}': {ex.Message}"); }
            }
            return null; // Return null if object or property is invalid, or read fails
        }

        /// <summary>
        /// Sets the value of a specific property on the current server's settings object and saves.
        /// </summary>
        private void SetCurrentPropertyValue(Type settingsType, PropertyInfo property, object value)
        {
            object settingsObj = GetCurrentSettingsObject(settingsType);
            if (settingsObj != null && property != null && property.CanWrite)
            {
                try
                {
                    // Basic type conversion safety (can be expanded if more types are needed)
                    object convertedValue = value;
                    if (value is string strValue && property.PropertyType != typeof(string))
                    {
                        if (property.PropertyType == typeof(int))
                        {
                            if (int.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out int intResult))
                                convertedValue = intResult;
                            else return; // Don't set if parse fails
                        }
                        else if (property.PropertyType == typeof(float))
                        {
                            if (float.TryParse(strValue, NumberStyles.Any, CultureInfo.InvariantCulture, out float floatResult))
                                convertedValue = floatResult;
                            else return; // Don't set if parse fails
                        }
                        else if (property.PropertyType == typeof(bool))
                        {
                            if (bool.TryParse(strValue, out bool boolResult)) // Handle string "True"/"False"
                                convertedValue = boolResult;
                            else return; // Don't set if parse fails
                        }
                    }

                    // Check if the converted value is assignable to the property type
                    if (convertedValue == null && !property.PropertyType.IsValueType || // Allow null for reference types
                       (convertedValue != null && property.PropertyType.IsAssignableFrom(convertedValue.GetType())))
                    {
                        property.SetValue(settingsObj, convertedValue);
                        _serverManager.SaveCurrentServerSettings(); // Save after successful set
                    }
                    else if (convertedValue != null)
                    {
                        Debug.WriteLine($"WARN: Type mismatch trying to set property '{property.Name}' ({property.PropertyType.Name}) with value of type {convertedValue.GetType().Name}");
                    }
                }
                catch (FormatException formatEx) { Debug.WriteLine($"FORMAT ERROR setting property '{property.Name}' with value '{value}': {formatEx.Message}"); }
                catch (TargetInvocationException tie) { Debug.WriteLine($"INVOCATION ERROR setting property '{property.Name}': {tie.InnerException?.Message ?? tie.Message}"); }
                catch (Exception ex) { Debug.WriteLine($"ERROR setting property '{property.Name}': {ex.Message}"); }
            }
            else if (settingsObj != null && property != null && !property.CanWrite)
            {
                Debug.WriteLine($"WARN: Attempted to set read-only property '{property.Name}'.");
            }
        }


        /// <summary>
        /// Gets the value of an element within an array property from the current server's settings object.
        /// </summary>
        private object GetCurrentArrayPropertyValue(Type settingsType, PropertyInfo arrayProperty, int index)
        {
            object settingsObj = GetCurrentSettingsObject(settingsType);
            if (settingsObj != null && arrayProperty != null && arrayProperty.PropertyType.IsArray && index >= 0)
            {
                try
                {
                    var array = arrayProperty.GetValue(settingsObj) as Array;
                    if (array != null && index < array.Length)
                    {
                        return array.GetValue(index);
                    }
                    else if (array != null)
                    {
                        Debug.WriteLine($"WARN: Index {index} out of bounds for array '{arrayProperty.Name}' (Length: {array.Length}).");
                    }
                }
                catch (Exception ex) { Debug.WriteLine($"ERROR getting array property value for {arrayProperty.Name}[{index}]: {ex.Message}"); }
            }

            // Return default value for the element type if retrieval fails
            Type elementType = arrayProperty?.PropertyType.GetElementType();
            if (elementType == typeof(float)) return 0f;
            if (elementType == typeof(int)) return 0;
            if (elementType == typeof(bool)) return false;
            return null; // Default for reference types or unknown value types
        }


        /// <summary>
        /// Updates the displayed value in the associated UI control.
        /// </summary>
        private void UpdateAssociatedControl(FrameworkElement control, object value)
        {
            if (control == null) return;

            try
            {
                if (control is TextBox tb)
                {
                    string formattedValue;
                    // Format floats precisely using invariant culture
                    if (value is float f)
                        formattedValue = f.ToString("0.###############", CultureInfo.InvariantCulture);
                    else // Format other types (int, string, bool) using default ToString
                        formattedValue = value?.ToString() ?? string.Empty;

                    // Only update if text actually differs to prevent event loops/cursor jumps
                    if (tb.Text != formattedValue)
                    {
                        Debug.WriteLine($"      Updating TextBox '{control.Name}' from '{tb.Text}' to '{formattedValue}'");
                        tb.Text = formattedValue;
                    }
                    else
                    {
                        // Debug.WriteLine($"      TextBox '{control.Name}' value '{formattedValue}' already matches. Skipping update.");
                    }
                }
                else if (control is CheckBox cb)
                {
                    // Safely convert value to bool (defaulting to false if null or wrong type)
                    bool boolValue = (value is bool b) ? b : false;
                    // Only update if state differs
                    if (cb.IsChecked != boolValue)
                    {
                        Debug.WriteLine($"      Updating CheckBox '{control.Name}' from '{cb.IsChecked}' to '{boolValue}'");
                        cb.IsChecked = boolValue;
                    }
                    else
                    {
                        // Debug.WriteLine($"      CheckBox '{control.Name}' value '{boolValue}' already matches. Skipping update.");
                    }
                }
                // Add other control types (ComboBox?) if needed
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ERROR updating UI control '{control.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Formats a property name (e.g., "HarvestAmountMultiplier") into a more user-friendly display name ("Harvest Amount Multiplier").
        /// Handles common prefixes like "b" for booleans.
        /// </summary>
        private string FormatDisplayName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return string.Empty;

            // Handle common boolean prefix 'b' (e.g., "bUseCorpseLocator" -> "Use Corpse Locator")
            // Ensure the second character is uppercase to avoid changing names like "buffer"
            if (propertyName.Length > 1 && propertyName.StartsWith("b") && char.IsUpper(propertyName[1]))
            {
                propertyName = propertyName.Substring(1);
            }

            var sb = new StringBuilder();
            sb.Append(propertyName[0]); // Start with the first character

            // Iterate through the rest of the name
            for (int i = 1; i < propertyName.Length; i++)
            {
                char currentChar = propertyName[i];
                char prevChar = propertyName[i - 1];

                // Add a space before uppercase letters if:
                // 1. The current char is uppercase.
                // 2. The previous char is lowercase OR the char after current is lowercase (handles acronyms like "XP" correctly).
                if (char.IsUpper(currentChar) &&
                   (char.IsLower(prevChar) || (i + 1 < propertyName.Length && char.IsLower(propertyName[i + 1]))))
                {
                    // Add space only if previous char wasn't already a space
                    if (prevChar != ' ')
                    {
                        sb.Append(' ');
                    }
                }
                // Append the current character
                sb.Append(currentChar);
            }

            // Replace underscores with spaces for names like "PerLevelStatsMultiplier_Player" (though we usually use item names for arrays)
            // sb.Replace('_', ' '); // Optional: If property names themselves contain underscores

            return sb.ToString();
        }

        /// <summary>
        /// Updates all settings panels based on the currently selected server.
        /// Should be called when the server selection changes.
        /// </summary>
        // Inside SettingsManager.cs
        public void UpdateUIFromSelection()
        {
            // ***** Logging setup *****
            bool isLikelyInitialCall = false;
            string callType = "SUBSEQUENT"; // Default
            // Check main window state IF it exists and is of the correct type
            if (Application.Current?.MainWindow is MainWindow mw && mw.isInitializing)
            {
                // If the flag in MainWindow is still true, consider it initial
                callType = "INITIAL (During Init)";
                isLikelyInitialCall = true; // Flag it
            }
            else
            {
                // Otherwise, assume subsequent (after init flag is false or window is unavailable)
                callType = "SUBSEQUENT";
            }
            Debug.WriteLine($"-- SM.UpdateUIFromSelection ({callType}) - START --");
            // ***** End Logging setup *****


            if (_serverManager.CurrentServer == null)
            {
                ClearPanel(_gameSettingsPanel);
                ClearPanel(_serverSettingsPanel);
                ClearPanel(_userSettingsPanel);
                Debug.WriteLine($"-- SM.UpdateUIFromSelection ({callType}) - Cleared panels (no server). END --");
                return;
            }

            Debug.WriteLine($"-- SM.UpdateUIFromSelection ({callType}) - Updating for server: '{_serverManager.CurrentServer.Name}'...");

            // Pass callType to the helper method
            UpdatePanelControls(_gameSettingsPanel, typeof(GameSettings), callType);
            UpdatePanelControls(_serverSettingsPanel, typeof(ServerSettings), callType);
            UpdatePanelControls(_userSettingsPanel, typeof(UserSettings), callType);

            Debug.WriteLine($"-- SM.UpdateUIFromSelection ({callType}) - FINISHED updating panels. END --");
        }


        /// <summary>
        /// Clears the content of a settings panel (specifically, the inner StackPanel within the ScrollViewer).
        /// </summary>
        private void ClearPanel(StackPanel panel)
        {
            // Find the ScrollViewer and its inner StackPanel content
            if (panel.Children.Count > 0 && panel.Children[0] is ScrollViewer sv && sv.Content is StackPanel innerPanel)
            {
                // Clear the controls within the inner panel
                innerPanel.Children.Clear();
            }
            else
            {
                // Fallback: Clear the main panel directly if structure is unexpected
                panel.Children.Clear();
                Debug.WriteLine($"WARN: Could not find expected ScrollViewer/StackPanel structure in panel '{panel.Name}'. Cleared panel directly.");
            }
        }

        /// <summary>
        /// Updates the values of controls within a specific panel based on the current server's settings.
        /// </summary>
        // Inside SettingsManager.cs
        /// <summary>
        /// Updates the values of controls within a specific panel based on the current server's settings.
        /// </summary>
        // Inside SettingsManager.cs
        private void UpdatePanelControls(StackPanel panel, Type settingsType, string callType) // Added callType parameter
        {
            Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - START updating panel '{panel.Name}' for type '{settingsType.Name}' ----");

            if (_serverManager.CurrentServer == null) return; // Should not happen if called correctly

            object settingsObj = GetCurrentSettingsObject(settingsType);
            if (settingsObj == null)
            {
                Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - ERROR: Settings object NULL for {settingsType.Name}. Aborting panel update. ----");
                return;
            }

            // Ensure the panel has the expected ScrollViewer -> StackPanel structure
            if (!(panel.Children.Count > 0 && panel.Children[0] is ScrollViewer scrollViewer && scrollViewer.Content is StackPanel innerPanel))
            {
                Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - ERROR: Panel structure invalid for '{panel.Name}'. Cannot find inner StackPanel. Aborting panel update. ----");
                return;
            }

            int gridCount = innerPanel.Children.OfType<Grid>().Count();
            int controlsUpdated = 0;
            int controlsProcessed = 0; // Track processed controls
            Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - Found innerPanel for '{panel.Name}'. Expecting to process {gridCount} Grid elements. ----");

            // Iterate through the Grid elements which contain the Label/Control/ResetButton
            foreach (var gridElement in innerPanel.Children.OfType<Grid>())
            {
                controlsProcessed++; // Increment for every grid found

                // Find the actual setting control (TextBox, CheckBox) - expected in Column 1
                FrameworkElement settingControl = gridElement.Children
                                                            .OfType<FrameworkElement>()
                                                            .FirstOrDefault(fe => Grid.GetColumn(fe) == 1);

                // If no control found in column 1, or it has no Name, skip this Grid (might be header/separator)
                if (settingControl == null || string.IsNullOrEmpty(settingControl.Name))
                {
                    // This is expected for SectionLabel grids etc.
                    // Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - Grid {controlsProcessed}/{gridCount}: Skipping (no control found or no name). ----");
                    continue;
                }

                string controlName = settingControl.Name;
                PropertyInfo property = null;
                object retrievedValue = null;
                string propertyIdentifier = "N/A"; // For logging clarity

                // --- MODIFIED CHECK for Array vs Scalar ---
                bool isArrayItem = false;
                int arrayIndex = -1;
                string propertyName = controlName; // Assume scalar initially, using the full control name

                if (controlName.Contains("_"))
                {
                    var parts = controlName.Split('_');
                    // Ensure there's at least one part before the potential index
                    if (parts.Length > 1)
                    {
                        string potentialPropertyName = string.Join("_", parts.Take(parts.Length - 1));
                        // Check if the last part is a valid integer index
                        if (int.TryParse(parts.Last(), out arrayIndex) && arrayIndex >= 0)
                        {
                            // Now, crucially check if the property *before* the index actually exists and IS an array
                            PropertyInfo potentialArrayProp = settingsType.GetProperty(potentialPropertyName);
                            if (potentialArrayProp != null && potentialArrayProp.PropertyType.IsArray)
                            {
                                // Confirmed: It matches the pattern AND the property is an array
                                isArrayItem = true;
                                propertyName = potentialPropertyName; // Use the name *before* the index for property lookup
                                propertyIdentifier = $"{propertyName}[{arrayIndex}]"; // Set identifier for logging
                                                                                      // Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - Grid {controlsProcessed}: Detected Array Item '{controlName}' -> Property '{propertyName}', Index {arrayIndex} ----");
                            }
                            // else: Pattern matches _Number, but the base property isn't an array. Treat as scalar.
                            // propertyName remains controlName.
                        }
                        // else: Contains underscore, but last part isn't a number. Treat as scalar.
                        // propertyName remains controlName.
                    }
                    // else: Contains underscore but is only one part (e.g., "_something"). Treat as scalar.
                    // propertyName remains controlName.
                }
                // else: No underscore, definitely treat as scalar.
                // propertyName remains controlName.

                // If not detected as array item, set scalar identifier for logging
                if (!isArrayItem)
                {
                    propertyIdentifier = propertyName; // Use the full name
                    // Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - Grid {controlsProcessed}: Detected Scalar Item '{controlName}' -> Property '{propertyName}' ----");
                }
                // --- END MODIFIED CHECK ---


                try
                {
                    // Lookup the property based on the final determined 'propertyName'
                    property = settingsType.GetProperty(propertyName);

                    if (property == null)
                    {
                        Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - WARN Grid {controlsProcessed}: Property '{propertyName}' (derived from control '{controlName}') not found in type '{settingsType.Name}'. Skipping update. ----");
                        continue;
                    }

                    // Get the value based on whether it's an array item or scalar
                    if (isArrayItem)
                    {
                        retrievedValue = GetCurrentArrayPropertyValue(settingsType, property, arrayIndex);
                        Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) -    ArrayItem | Prop: {propertyIdentifier} | Retrieved Value: '{retrievedValue ?? "NULL"}' ----");
                    }
                    else // Scalar Item
                    {
                        retrievedValue = GetCurrentPropertyValue(settingsType, property);
                        Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) -    Scalar | Prop: {propertyIdentifier} | Retrieved Value: '{retrievedValue ?? "NULL"}' ----");
                    }

                    // Update the associated UI control
                    // Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) -    Calling UpdateAssociatedControl for '{controlName}' with value '{retrievedValue ?? "NULL"}' ----");
                    UpdateAssociatedControl(settingControl, retrievedValue);
                    controlsUpdated++; // Increment only if UpdateAssociatedControl was called
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - ERROR Grid {controlsProcessed} updating control '{controlName}' for property '{propertyIdentifier}': {ex.Message} ----");
                }
            } // End foreach gridElement

            Debug.WriteLine($"---- SM.UpdatePanelControls ({callType}) - FINISHED updating panel '{panel.Name}'. Processed {controlsProcessed} grids, attempted update on {controlsUpdated} controls. ----");
        }


        #endregion
    }
}