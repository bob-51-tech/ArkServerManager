using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;

namespace ArkServerManager
{
    public class ThemeManager
    {
        private readonly string _themesDir;
        private readonly string _configPath;
        private const string ConfigFileName = "themeconfig.json";
        // A unique key to find the dictionary we are responsible for managing.
        private const string ThemeOverrideMarker = "__LiveThemeOverrideDictionary__";

        public ThemeManager()
        {
            _themesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");
            if (!Directory.Exists(_themesDir)) Directory.CreateDirectory(_themesDir);
            _configPath = Path.Combine(_themesDir, ConfigFileName);

            // Ensure a default theme exists on first launch
            if (!ListThemes().Any())
            {
                CreateDefaultTheme();
                SetActiveThemeName("default");
            }
        }

        private void CreateDefaultTheme()
        {
            // As requested, we are now defining BRUSHES directly, not Colors.
            var defaultThemeDict = new ResourceDictionary();
            defaultThemeDict["BackgroundBrush1"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2D2D2D"));
            defaultThemeDict["BackgroundBrush2"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4D4D4D"));
            defaultThemeDict["BackgroundBrush3"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8D8D8D"));
            defaultThemeDict["ForegroundBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFFFF"));
            defaultThemeDict["AccentBrush1"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF03DAC5"));
            defaultThemeDict["AccentBrush2"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFF5722"));
            defaultThemeDict["HoverBrush1"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7708DAC8"));
            defaultThemeDict["SelectBrush1"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4408DAC8"));

            // Freeze all brushes for performance
            foreach (var value in defaultThemeDict.Values)
            {
                if (value is Freezable f && f.CanFreeze) f.Freeze();
            }

            SaveThemeDictionary("default", defaultThemeDict);
        }

        public IEnumerable<string> ListThemes()
        {
            return Directory.EnumerateFiles(_themesDir, "*.theme", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n);
        }

        public ResourceDictionary LoadThemeDictionary(string themeName)
        {
            string path = GetThemePath(themeName);
            if (!File.Exists(path)) return null;
            try
            {
                using var fs = File.OpenRead(path);
                return XamlReader.Load(fs) as ResourceDictionary;
            }
            catch (Exception) { return null; }
        }

        public bool SaveThemeColors(string themeName, Dictionary<string, string> colors)
        {
            // Create a new dictionary to hold our BRUSHES
            var newThemeDict = new ResourceDictionary();
            foreach (var kvp in colors)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(kvp.Value);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    // We are saving the BRUSH directly to the file now.
                    newThemeDict[kvp.Key] = brush;
                }
                catch { /* Ignore invalid formats */ }
            }

            if (!SaveThemeDictionary(themeName, newThemeDict)) return false;

            // Force the live update
            return ApplyTheme(themeName);
        }

        public bool ApplyTheme(string themeName)
        {
            var newThemeDict = LoadThemeDictionary(themeName);
            if (newThemeDict == null) return false;

            var app = Application.Current;
            if (app == null) return false;

            // BRUTE FORCE METHOD:
            // 1. Find and completely remove any old theme override dictionary.
            var oldDict = app.Resources.MergedDictionaries.FirstOrDefault(d => d.Contains(ThemeOverrideMarker));
            if (oldDict != null)
            {
                app.Resources.MergedDictionaries.Remove(oldDict);
            }

            // 2. Tag the new dictionary so we can find it next time.
            newThemeDict[ThemeOverrideMarker] = "true";

            // 3. Add the new dictionary to the END of the list.
            // This ensures it overrides everything else before it.
            app.Resources.MergedDictionaries.Add(newThemeDict);

            SetActiveThemeName(themeName);
            return true;
        }

        public bool DeleteTheme(string themeName)
        {
            if (themeName.Equals("default", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("The default theme cannot be deleted.", "Action Denied", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (GetActiveThemeName() == themeName)
            {
                ApplyTheme("default");
            }

            string path = GetThemePath(themeName);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch { return false; }
        }

        public bool SaveCurrentColorsAsTheme(string newThemeName)
        {
            if (string.IsNullOrWhiteSpace(newThemeName)) return false;
            var app = Application.Current;
            if (app == null) return false;

            var newThemeDict = new ResourceDictionary();
            // Scan all application resources for SolidColorBrushes and save them to the new theme.
            // This captures the current state perfectly.
            foreach (var dict in app.Resources.MergedDictionaries)
            {
                foreach (var key in dict.Keys)
                {
                    if (key is string keyStr && dict[key] is SolidColorBrush brush)
                    {
                        newThemeDict[keyStr] = brush;
                    }
                }
            }

            return SaveThemeDictionary(newThemeName, newThemeDict);
        }

        public Dictionary<string, string> GetThemeColors(string themeName)
        {
            var result = new Dictionary<string, string>();
            var rd = LoadThemeDictionary(themeName);
            if (rd == null) return result;

            // 1. Get all keys that are strings
            var keys = rd.Keys.OfType<string>();

            // 2. Sort them alphabetically
            var sortedKeys = keys.OrderBy(k => k);

            // 3. Extract colors from the brushes
            foreach (var key in sortedKeys)
            {
                if (rd[key] is SolidColorBrush brush)
                {
                    result[key] = brush.Color.ToString();
                }
                else if (rd[key] is Color color)
                {
                    // Fallback in case some old theme files still have Colors
                    result[key] = color.ToString();
                }
            }
            return result;
        }

        public string GetThemePath(string themeName) => Path.Combine(_themesDir, $"{themeName}.theme");

        private bool SaveThemeDictionary(string themeName, ResourceDictionary dictionary)
        {
            if (string.IsNullOrWhiteSpace(themeName)) return false;
            string path = GetThemePath(themeName);
            try
            {
                var settings = new XmlWriterSettings { Indent = true, IndentChars = "    " };
                using var writer = XmlWriter.Create(path, settings);
                XamlWriter.Save(dictionary, writer);
                return true;
            }
            catch (Exception) { return false; }
        }

        public string GetActiveThemeName()
        {
            try
            {
                if (!File.Exists(_configPath)) return null;
                var json = File.ReadAllText(_configPath);
                return JsonSerializer.Deserialize<ThemeConfig>(json)?.ActiveTheme;
            }
            catch { return null; }
        }

        public void SetActiveThemeName(string themeName)
        {
            try
            {
                var doc = new ThemeConfig { ActiveTheme = themeName };
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(_configPath, JsonSerializer.Serialize(doc, options));
            }
            catch { }
        }

        private class ThemeConfig { public string ActiveTheme { get; set; } }
    }
}