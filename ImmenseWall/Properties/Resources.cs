using System.Drawing;

namespace ImmenseWall.Properties
{
    public static class Resources
    {
        public static class Messages
        {
            public static string UnblockApp = "Unblock {0}?";
            public static string UnblockAppShowRelated = "Show related processes";
            public static string UnblockAppUnblockAllRecommended = "Unblock all (Recommended)";
            public static string UnblockAppUnblockOnlySelected = "Unblock only selected";
            public static string UnblockAppCancel = "Cancel";
            public static string TinyWall = "ImmenseWall";
        }

        public static class Icons
        {
            public static Icon firewall = SystemIcons.Shield; // Placeholder
        }
        
        public static class Exceptions
        {
             public static System.Resources.ResourceManager ResourceManager { get; } = new System.Resources.ResourceManager("ImmenseWall.Properties.Resources", typeof(Resources).Assembly);
        }
    }
}
