using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArkServerManager
{
    public partial class ColorPickerPopup : Window
    {
        /// <summary>
        /// Gets the color chosen by the user (null if cancelled).
        /// </summary>
        public Color? ChosenColor { get; private set; } = null;

        public ColorPickerPopup(Color initialColor)
        {
            InitializeComponent();
            PopupColorPicker.SelectedColor = initialColor; // Set initial color
            //(PopupColorPicker.FindName("EyedropperButton") as Button).Background =  Brushes.Transparent; // Set eyedropper button background
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.ChosenColor = PopupColorPicker.SelectedColor;
            this.DialogResult = true; // Set result before closing
            this.Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}