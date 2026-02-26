using System.ServiceProcess;
using Sentinel.Core.Interfaces;
using Sentinel.Core.Models;

namespace Sentinel.Platform.Collectors;

public sealed class ServicesCollector : IServicesCollector
{
    public Task<IReadOnlyList<ServiceInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var list = new List<ServiceInfo>();
        try
        {
            var services = ServiceController.GetServices();
            foreach (var sc in services)
            {
                try
                {
                    list.Add(new ServiceInfo
                    {
                        Name = sc.ServiceName,
                        DisplayName = sc.DisplayName,
                        Status = sc.Status.ToString(),
                        StartType = GetStartType(sc.ServiceName),
                        Description = null,
                        BinaryPath = null,
                        RequiresAdmin = false
                    });
                }
                catch (InvalidOperationException) { }
                finally { sc.Dispose(); }
            }
        }
        catch (Exception) { }
        return Task.FromResult((IReadOnlyList<ServiceInfo>)list);
    }

    public Task StartAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        using var sc = new ServiceController(serviceName);
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }

    public Task StopAsync(string serviceName, CancellationToken cancellationToken = default)
    {
        using var sc = new ServiceController(serviceName);
        sc.Stop();
        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
        return Task.CompletedTask;
    }

    public Task SetStartTypeAsync(string serviceName, string startType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName, writable: true);
            var val = startType switch
            {
                "Automatic" => 2,
                "Manual" => 3,
                "Disabled" => 4,
                _ => 3
            };
            key?.SetValue("Start", val);
        }
        catch (UnauthorizedAccessException)
        {
            throw new InvalidOperationException("Changing start type requires administrator rights. Use Settings → Relaunch as Admin, then try again.");
        }
        catch (System.Security.SecurityException)
        {
            throw new InvalidOperationException("Changing start type requires administrator rights. Use Settings → Relaunch as Admin, then try again.");
        }
        return Task.CompletedTask;
    }

    private static string GetStartType(string serviceName)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + serviceName);
            var start = key?.GetValue("Start");
            return start switch
            {
                2 => "Automatic",
                3 => "Manual",
                4 => "Disabled",
                _ => "Unknown"
            };
        }
        catch
        {
            return "Unknown";
        }
    }
}
