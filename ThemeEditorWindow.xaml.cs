using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Forms = System.Windows.Forms;


namespace ArkServerManager
{
    public partial class ThemeEditorWindow : Window
    {
        private readonly ThemeManager _themeManager;
        private readonly string _themeName;
        private Dictionary<string, string> _colors;

        public ThemeEditorWindow(ThemeManager mgr, string themeName)
        {
            InitializeComponent();
            _themeManager = mgr;
            _themeName = themeName;
            Title = "Theme Editor - " + themeName;
            _colors = new Dictionary<string, string>();
            _themeManager.ApplyTheme(_themeName); // Ensure theme is applied
            LoadColors();
            ColorList.SelectionChanged += ColorList_SelectionChanged;
            ValueBox.TextChanged += ValueBox_TextChanged;
        }

        private void LoadColors()
        {
            _colors = _themeManager.GetThemeColors(_themeName);
            Console.WriteLine($"Loaded colors count: {_colors.Count}");
            foreach (var kv in _colors)
            {
                Console.WriteLine($"Key: {kv.Key}, Value: {kv.Value}");
            }
            ColorList.ItemsSource = new List<KeyValuePair<string, string>>(_colors);
        }

        private void ColorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ColorList.SelectedItem is KeyValuePair<string, string> kv)
            {
                KeyBox.Text = kv.Key;
                ValueBox.Text = kv.Value;
                UpdatePreview(kv.Value);
            }
            else
            {
                KeyBox.Text = string.Empty;
                ValueBox.Text = string.Empty;
                PreviewRect.Fill = Brushes.Transparent;
            }
        }

        private void ColorList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ColorList.SelectedItem is KeyValuePair<string, string> kv)
            {
                KeyBox.Text = kv.Key;
                PickColor_Click(this, null);
            }
        }

        private void ValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview(ValueBox.Text);
        }

        private void UpdatePreview(string hex)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var color = (Color)ColorConverter.ConvertFromString(hex);
                    PreviewRect.Fill = new SolidColorBrush(color);
                }
                else
                {
                    PreviewRect.Fill = Brushes.Transparent;
                }
            }
            catch
            {
                PreviewRect.Fill = Brushes.Transparent;
            }
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get current color if needed for initial value
                Color currentColor = Colors.Blue; // Example initial color
                                                  // if (ColorDisplayRectangle.Fill is SolidColorBrush currentBrush)
                {
                    // currentColor = currentBrush.Color;
                }

                // Create and show the popup window
                ColorPickerPopup popup = new ColorPickerPopup(currentColor);
                popup.Owner = this; // Make it owned by the main window

                bool? Cresult = popup.ShowDialog(); // Show modally and wait

                // Check if the user clicked OK and get the chosen color
                if (Cresult == true && popup.ChosenColor.HasValue)
                {
                    Color selected = popup.ChosenColor.Value;
                    // Use the selected color
                    //ColorDisplayRectangle.Fill = new SolidColorBrush(selected);
                   // UpdatePreview($"#{selected.A:X2}{selected.R:X2}{selected.G:X2}{selected.B:X2}");
                   ValueBox.Text = selected.ToString();
                    ValueBox_TextChanged(this, null);
                    // UpdatePreview(selected.ToString());
                    Debug.WriteLine($"Color chosen from popup: {selected}");
                }
                else
                {
                    Debug.WriteLine("Color selection cancelled.");
                }
            }
            catch { }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(KeyBox.Text)) return;
            try
            {
                if (!string.IsNullOrEmpty(ValueBox.Text))
                {
                    var color = (Color)ColorConverter.ConvertFromString(ValueBox.Text);
                    _colors[KeyBox.Text] = ValueBox.Text.Trim();
                    if (_themeManager.SaveThemeColors(_themeName, _colors))
                    {
                        MessageBox.Show(this, "Saved colors.", "OK", MessageBoxButton.OK, MessageBoxImage.Information);
                        LoadColors(); // Refresh the list
                    }
                    else
                    {
                        MessageBox.Show(this, "Failed to save colors.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch
            {
                MessageBox.Show(this, "Invalid color format. Use #AARRGGBB or #RRGGBB.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}