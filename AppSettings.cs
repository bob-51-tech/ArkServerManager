using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArkServerManager
{
    public class AppSettings
    {
        // Default values for a first-time run
        public bool AutoUpdate { get; set; } = true;
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimized { get; set; } = false;
    }
}
