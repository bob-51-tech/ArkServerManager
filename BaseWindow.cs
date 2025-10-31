using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArkServerManager
{
    public class BaseWindow : Window
    {
        public BaseWindow()
        {
            // Apply the custom style to any window that inherits from this class
            // This needs to be done before the window is shown.
            this.Style = (Style)Application.Current.FindResource("CustomWindowStyle");
        }

        /// <summary>
        /// This method is called by the WPF framework after the window's template has been
        /// applied. This is the correct and most reliable place to find template parts
        /// and attach event handlers to them.
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (this.Template.FindName("MinimizeButton", this) is Button minimizeButton)
            {
                minimizeButton.Click += (s, args) => WindowState = WindowState.Minimized;
            }

            if (this.Template.FindName("MaximizeButton", this) is Button maximizeButton)
            {
                maximizeButton.Click += (s, args) =>
                {
                    WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                };
            }

            if (this.Template.FindName("CloseButton", this) is Button closeButton)
            {
                closeButton.Click += (s, args) => Close();
            }

            if (this.Template.FindName("TitleBar", this) is Grid titleBar)
            {
                titleBar.MouseDown += (s, args) =>
                {
                    // This allows the user to double-click the title bar to maximize/restore
                    if (args.ChangedButton == MouseButton.Left && args.ClickCount == 2)
                    {
                        WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
                        return;
                    }

                    // This allows the user to drag the window
                    if (args.ChangedButton == MouseButton.Left)
                    {
                        this.DragMove();
                    }
                };
            }
        }
    }
}