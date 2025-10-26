using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using System.Windows.Media;

namespace ArkServerManager
{
    public class ThemeManager
    {
        private readonly Dictionary<string, string> _lastKeyFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly string _themesDir;
        private readonly string _configPath;
        private const string ConfigFileName = "themeconfig.json";

        public ThemeManager()
        {
            _themesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Themes");
            if (!Directory.Exists(_themesDir)) Directory.CreateDirectory(_themesDir);
            _configPath = Path.Combine(_themesDir, ConfigFileName);
        }

        public IEnumerable<string> ListThemes()
        {
            return Directory.EnumerateFiles(_themesDir, "*.xaml", SearchOption.TopDirectoryOnly)
                           .Select(Path.GetFileNameWithoutExtension)
                           .Where(n => !string.Equals(n, Path.GetFileNameWithoutExtension(ConfigFileName), StringComparison.OrdinalIgnoreCase))
                           .OrderBy(n => n);
        }

        public ResourceDictionary LoadThemeDictionary(string themeName)
        {
            string path = GetThemePath(themeName);
            if (!File.Exists(path)) return null;
            using var fs = File.OpenRead(path);
            try
            {
                var obj = XamlReader.Load(fs);
                if (obj is ResourceDictionary rd) return rd;
            }
            catch
            {
                // Ignore parse errors
            }
            return null;
        }

        public bool ApplyTheme(string themeName)
        {
            var rd = LoadThemeDictionary(themeName);
            if (rd == null) return false;

            var app = Application.Current;
            if (app == null) return false;

            var toRemove = app.Resources.MergedDictionaries
                             .Where(d => d.Contains("__ThemeManagerMarker")).ToList();
            foreach (var d in toRemove) app.Resources.MergedDictionaries.Remove(d);

            rd["__ThemeManagerMarker"] = themeName;
            app.Resources.MergedDictionaries.Add(rd);

            SetActiveThemeName(themeName);
            return true;
        }

        public bool SaveCurrentApplicationResourcesAsTheme(string themeName)
        {
            if (string.IsNullOrWhiteSpace(themeName)) return false;
            string path = GetThemePath(themeName);

            try
            {
                var merged = new ResourceDictionary();
                foreach (var md in Application.Current.Resources.MergedDictionaries)
                {
                    merged.MergedDictionaries.Add(md);
                }
                foreach (var key in Application.Current.Resources.Keys)
                {
                    if (key is string k && !merged.Contains(k))
                    {
                        merged[k] = Application.Current.Resources[k];
                    }
                }

                string xaml = XamlWriter.Save(merged);
                File.WriteAllText(path, xaml);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool DeleteTheme(string themeName)
        {
            string path = GetThemePath(themeName);
            try
            {
                if (File.Exists(path)) File.Delete(path);
                if (GetActiveThemeName() == themeName) SetActiveThemeName(null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetThemePath(string themeName) => Path.Combine(_themesDir, themeName + ".xaml");

        public Dictionary<string, string> GetThemeColors(string themeName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _lastKeyFile.Clear();
            try
            {
                // First, check the currently applied resources
                var app = Application.Current;
                if (app != null)
                {
                    var mergedDicts = app.Resources.MergedDictionaries;
                    foreach (var dict in mergedDicts)
                    {
                        foreach (var key in dict.Keys)
                        {
                            try
                            {
                                string skey = key?.ToString();
                                if (string.IsNullOrEmpty(skey)) continue;
                                var val = dict[key];
                                if (val is SolidColorBrush scb)
                                {
                                    var c = scb.Color;
                                    result[skey] = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                                    _lastKeyFile[skey] = "MergedResource";
                                }
                                else if (val is Color c2)
                                {
                                    result[skey] = $"#{c2.A:X2}{c2.R:X2}{c2.G:X2}{c2.B:X2}";
                                    _lastKeyFile[skey] = "MergedResource";
                                }
                            }
                            catch { }
                        }
                    }
                }

                // Fallback to file-based parsing if needed
                string mainPath = GetThemePath(themeName);
                if (File.Exists(mainPath))
                {
                    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    void ParseFile(string path)
                    {
                        if (string.IsNullOrEmpty(path) || visited.Contains(path)) return;
                        visited.Add(path);
                        if (!File.Exists(path)) return;
                        try
                        {
                            var doc = XDocument.Load(path);
                            var colors = doc.Descendants().Where(d => d.Name.LocalName == "Color");
                            foreach (var c in colors)
                            {
                                var keyAttr = c.Attributes().FirstOrDefault(a => a.Name.LocalName == "Key");
                                var key = keyAttr?.Value;
                                var val = c.Value?.Trim();
                                if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val) && val.StartsWith("#"))
                                {
                                    if (!result.ContainsKey(key)) // Avoid overwriting from merged resources
                                    {
                                        result[key] = val;
                                        _lastKeyFile[key] = path;
                                    }
                                }
                            }

                            var brushes = doc.Descendants().Where(d => d.Name.LocalName == "SolidColorBrush");
                            foreach (var b in brushes)
                            {
                                var keyAttr = b.Attributes().FirstOrDefault(a => a.Name.LocalName == "Key");
                                var key = keyAttr?.Value;
                                if (string.IsNullOrEmpty(key)) continue;
                                var colorAttr = b.Attributes().FirstOrDefault(a => a.Name.LocalName == "Color")?.Value;
                                if (!string.IsNullOrEmpty(colorAttr) && colorAttr.StartsWith("#"))
                                {
                                    if (!result.ContainsKey(key))
                                    {
                                        result[key] = colorAttr.Trim();
                                        _lastKeyFile[key] = path;
                                    }
                                }
                                var colorChild = b.Descendants().FirstOrDefault(d => d.Name.LocalName == "Color");
                                if (colorChild != null)
                                {
                                    var val = colorChild.Value?.Trim();
                                    if (!string.IsNullOrEmpty(val) && val.StartsWith("#"))
                                    {
                                        if (!result.ContainsKey(key))
                                        {
                                            result[key] = val;
                                            _lastKeyFile[key] = path;
                                        }
                                    }
                                }
                            }

                            var mergedSources = doc.Descendants()
                                                  .Where(d => d.Name.LocalName == "ResourceDictionary")
                                                  .SelectMany(d => d.Attributes()
                                                                    .Where(a => a.Name.LocalName == "Source")
                                                                    .Select(a => a.Value));
                            foreach (var src in mergedSources)
                            {
                                if (string.IsNullOrEmpty(src)) continue;
                                if (src.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        var uri = new Uri(src, UriKind.Absolute);
                                        var obj = Application.LoadComponent(uri);
                                        if (obj is ResourceDictionary rdp)
                                        {
                                            foreach (var key in rdp.Keys)
                                            {
                                                try
                                                {
                                                    string sk = key?.ToString();
                                                    if (string.IsNullOrEmpty(sk)) continue;
                                                    var val = rdp[sk];
                                                    if (val is SolidColorBrush scb)
                                                    {
                                                        var c = scb.Color;
                                                        if (!result.ContainsKey(sk))
                                                        {
                                                            result[sk] = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
                                                            _lastKeyFile[sk] = src;
                                                        }
                                                    }
                                                    else if (val is Color c2)
                                                    {
                                                        if (!result.ContainsKey(sk))
                                                        {
                                                            result[sk] = $"#{c2.A:X2}{c2.R:X2}{c2.G:X2}{c2.B:X2}";
                                                            _lastKeyFile[sk] = src;
                                                        }
                                                    }
                                                }
                                                catch { }
                                            }
                                        }
                                    }
                                    catch { }
                                    continue;
                                }
                                string resolved = Path.IsPathRooted(src) ? src : Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, src);
                                ParseFile(resolved);
                            }
                        }
                        catch { }
                    }

                    ParseFile(mainPath);
                }
            }
            catch (Exception ex)
            {
                // Log or handle exception as needed
                Console.WriteLine($"Error in GetThemeColors: {ex.Message}");
            }
            return result;
        }

        public bool SaveThemeColors(string themeName, Dictionary<string, string> colors)
        {
            var path = GetThemePath(themeName);
            if (!File.Exists(path)) return false;
            try
            {
                foreach (var kv in colors)
                {
                    string targetFile = _lastKeyFile.TryGetValue(kv.Key, out var file) ? file : path;
                    if (targetFile.StartsWith("pack://", StringComparison.OrdinalIgnoreCase) || targetFile == "MergedResource") continue;
                    if (!File.Exists(targetFile)) continue;
                    try
                    {
                        var doc = XDocument.Load(targetFile);
                        XNamespace xns = "http://schemas.microsoft.com/winfx/2006/xaml";
                        bool updated = false;
                        var el = doc.Descendants().FirstOrDefault(d => d.Attributes().Any(a => a.Name.LocalName == "Key" && a.Value == kv.Key));
                        if (el != null)
                        {
                            if (el.Name.LocalName == "Color")
                            {
                                el.Value = kv.Value;
                                updated = true;
                            }
                            else if (el.Name.LocalName == "SolidColorBrush")
                            {
                                var colorAttr = el.Attributes().FirstOrDefault(a => a.Name.LocalName == "Color");
                                if (colorAttr != null)
                                {
                                    colorAttr.Value = kv.Value;
                                    updated = true;
                                }
                                else
                                {
                                    var colorChild = el.Descendants().FirstOrDefault(d => d.Name.LocalName == "Color");
                                    if (colorChild != null)
                                    {
                                        colorChild.Value = kv.Value;
                                        updated = true;
                                    }
                                }
                            }
                        }
                        else
                        {
                            var rd = doc.Descendants().FirstOrDefault(d => d.Name.LocalName == "ResourceDictionary");
                            if (rd != null)
                            {
                                rd.Add(new XElement(XName.Get("Color", xns.NamespaceName),
                                    new XAttribute(XName.Get("Key", xns.NamespaceName), kv.Key),
                                    kv.Value));
                                updated = true;
                            }
                        }

                        if (updated) doc.Save(targetFile);
                    }
                    catch { }
                }

                ApplyTheme(themeName); // Re-apply theme to update UI
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetActiveThemeName()
        {
            try
            {
                if (!File.Exists(_configPath)) return null;
                var json = File.ReadAllText(_configPath);
                var doc = JsonSerializer.Deserialize<ThemeConfig>(json);
                return doc?.ActiveTheme;
            }
            catch { return null; }
        }

        public void SetActiveThemeName(string themeName)
        {
            try
            {
                var doc = new ThemeConfig { ActiveTheme = themeName };
                File.WriteAllText(_configPath, JsonSerializer.Serialize(doc));
            }
            catch { }
        }

        private class ThemeConfig { public string ActiveTheme { get; set; } }
    }
}