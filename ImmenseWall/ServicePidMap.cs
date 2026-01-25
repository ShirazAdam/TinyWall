using pylorak.Windows.Services;
using System.Collections.Generic;
using System.ServiceProcess;

namespace pylorak.TinyWall
{
    public class ServicePidMap
    {
        private readonly Dictionary<uint, HashSet<string>> _cache = new Dictionary<uint, HashSet<string>>();

        public ServicePidMap()
        {
            using var scm = new ServiceControlManager();
            var services = ServiceController.GetServices();
            foreach (var service in services)
            {
                if (service.Status != ServiceControllerStatus.Running)
                    continue;

                uint pid = scm.GetServicePid(service.ServiceName) ?? 0;
                if (pid == 0) continue;

                if (!_cache.ContainsKey(pid))
                    _cache.Add(pid, new HashSet<string>());
                _cache[pid].Add(service.ServiceName);
            }
        }

        public HashSet<string> GetServicesInPid(uint pid)
        {
            return !_cache.ContainsKey(pid) ? new HashSet<string>() : new HashSet<string>(_cache[pid]);
        }
    }
}