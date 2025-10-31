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

        /// <summary>
        /// This method now dynamically reads the default brushes from your ThemeDictionary.xaml
        /// instead of using hard-coded values.
        /// </summary>
        private void CreateDefaultTheme()
        {
            // Find the base ThemeDictionary.xaml from the application's currently loaded resources.
            var baseThemeDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("ThemeDictionary.xaml"));

            if (baseThemeDict == null)
            {
                Debug.WriteLine("[ThemeManager] CRITICAL ERROR: Could not find 'ThemeDictionary.xaml' in application resources to create the default theme.");
                return;
            }

            // Create a new dictionary that will become the 'default.theme' file.
            var defaultThemeDict = new ResourceDictionary();

            // Iterate through all resources in the base theme dictionary.
            foreach (var key in baseThemeDict.Keys)
            {
                // We only care about SolidColorBrushes, as these are what we want to be themeable.
                if (key is string keyString && baseThemeDict[key] is SolidColorBrush brush)
                {
                    // Add the brush to our new dictionary.
                    defaultThemeDict[keyString] = brush;
                }
            }

            Debug.WriteLine($"[ThemeManager] Dynamically found {defaultThemeDict.Count} brushes in ThemeDictionary.xaml to create the default theme.");
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
            var newThemeDict = new ResourceDictionary();
            foreach (var kvp in colors)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(kvp.Value);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    newThemeDict[kvp.Key] = brush;
                }
                catch { /* Ignore invalid formats */ }
            }

            if (!SaveThemeDictionary(themeName, newThemeDict)) return false;

            return ApplyTheme(themeName);
        }

        public bool ApplyTheme(string themeName)
        {
            var newThemeDict = LoadThemeDictionary(themeName);
            if (newThemeDict == null) return false;

            var app = Application.Current;
            if (app == null) return false;

            var oldDict = app.Resources.MergedDictionaries.FirstOrDefault(d => d.Contains(ThemeOverrideMarker));
            if (oldDict != null)
            {
                app.Resources.MergedDictionaries.Remove(oldDict);
            }

            newThemeDict[ThemeOverrideMarker] = "true";
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

            // Find the base ThemeDictionary.xaml to know which keys to look for.
            var baseThemeDict = Application.Current.Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source != null && d.Source.OriginalString.Contains("ThemeDictionary.xaml"));

            if (baseThemeDict == null) return false;

            // For each brush key in the base theme, find its current live value.
            foreach (var key in baseThemeDict.Keys)
            {
                if (key is string keyString && keyString.EndsWith("Brush"))
                {
                    // TryFindResource gets the current, potentially overridden, brush.
                    if (app.TryFindResource(keyString) is SolidColorBrush currentBrush)
                    {
                        newThemeDict[keyString] = currentBrush;
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

            var keys = rd.Keys.OfType<string>().Where(k => !k.Equals(ThemeOverrideMarker));
            var sortedKeys = keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase);

            foreach (var key in sortedKeys)
            {
                if (rd[key] is SolidColorBrush brush)
                {
                    result[key] = brush.Color.ToString();
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