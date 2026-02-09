using System.Net;

namespace ImmenseWall.Models
{
    public class ConnectionItem
    {
        public string ProcessName { get; set; } = string.Empty;
        public uint ProcessId { get; set; }
        public string Protocol { get; set; } = string.Empty;
        public string LocalAddress { get; set; } = string.Empty;
        public string RemoteAddress { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public System.Windows.Media.ImageSource? Icon { get; set; }
    }
}
