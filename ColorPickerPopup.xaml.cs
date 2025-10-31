using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ArkServerManager
{
    public partial class ColorPickerPopup : BaseWindow
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
            //this.DialogResult = true; // Set result before closing
            var tem = Owner as ThemeEditorWindow;
            tem.ValueBox.Text = this.ChosenColor.Value.ToString();

            this.Close();
        }

        private void PopupColorPicker_SelectedColorChanged(object sender, Color e)
        {
            this.ChosenColor = PopupColorPicker.SelectedColor;
            var tem = Owner as ThemeEditorWindow;
            if (tem != null)
            {
                tem.ValueBox.Text = this.ChosenColor.Value.ToString();
                tem.Apply_Live(this, null);
            }
        }
    }
}