
using System.Windows;

namespace ArkServerManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. Instantiate the ThemeManager. Its constructor now ensures the theme slot exists.
            ThemeManager themeManager = new ThemeManager();

            // 2. Get the last active theme name.
            string activeTheme = themeManager.GetActiveThemeName();

            if (!string.IsNullOrEmpty(activeTheme))
            {
                // 3. Apply the theme.
                themeManager.ApplyTheme(activeTheme);
            }
            else
            {
                // Fallback to default if no theme was active.
                themeManager.ApplyTheme("default");
            }
        }
    }
}