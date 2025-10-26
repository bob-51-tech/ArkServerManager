// ThemeEditorWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.ComponentModel;
using System.Linq;

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

            // Critical: Ensure the theme being edited is active so we see changes live.
            _themeManager.ApplyTheme(_themeName);

            LoadColors();
            ColorList.SelectionChanged += ColorList_SelectionChanged;
            ValueBox.TextChanged += ValueBox_TextChanged;
        }

        private void LoadColors()
        {
            _colors = _themeManager.GetThemeColors(_themeName);
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
        }

        private void ValueBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdatePreview(ValueBox.Text);
        }

        private void UpdatePreview(string hex)
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                PreviewRect.Fill = new SolidColorBrush(color);
            }
            catch
            {
                PreviewRect.Fill = Brushes.Transparent; // Invalid color format
            }
        }

        private void PickColor_Click(object sender, RoutedEventArgs e)
        {
            // (Your existing PickColor_Click code is fine)
            try
            {
                Color currentColor = Colors.Blue;
                if (PreviewRect.Fill is SolidColorBrush currentBrush)
                {
                    currentColor = currentBrush.Color;
                }

                ColorPickerPopup popup = new ColorPickerPopup(currentColor) { Owner = this };
                if (popup.ShowDialog() == true && popup.ChosenColor.HasValue)
                {
                    ValueBox.Text = popup.ChosenColor.Value.ToString();
                }
            }
            catch { }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(KeyBox.Text) || !_colors.ContainsKey(KeyBox.Text))
            {
                return;
            }

            try
            {
                // Validate the color format before saving
                var color = (Color)ColorConverter.ConvertFromString(ValueBox.Text);

                _colors[KeyBox.Text] = ValueBox.Text.Trim();

                // This now calls the corrected ThemeManager logic, which will trigger the live update
                if (_themeManager.SaveThemeColors(_themeName, _colors))
                {
                    // Refresh our local listbox to show the new color square
                    int selectedIndex = ColorList.SelectedIndex;
                    LoadColors();
                    if (selectedIndex >= 0)
                    {
                        ColorList.SelectedIndex = selectedIndex;
                    }
                }
                else
                {
                    MessageBox.Show(this, "Failed to save colors.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch
            {
                MessageBox.Show(this, "Invalid color format. Use #AARRGGBB.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ColorList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ColorList.SelectedItem is KeyValuePair<string, string> kv)
            {
                PickColor_Click(sender, null);
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}