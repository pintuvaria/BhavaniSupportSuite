using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace BhavaniSupportSuite.Services;

public class ScanResult
{
    public string IpAddress { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public bool IsAlive { get; set; }
    public long ResponseTimeMs { get; set; }
    public List<PortScanResult> OpenPorts { get; set; } = new();
}

public class PortScanResult
{
    public int Port { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public string State { get; set; } = string.Empty;
}

public class IpConflictResult
{
    public string IpAddress { get; set; } = string.Empty;
    public string Mac1 { get; set; } = string.Empty;
    public string Mac2 { get; set; } = string.Empty;
    public bool HasConflict { get; set; }
}

public class NetworkScanner
{
    private const int MaxConcurrentPings = 100;
    private const int MaxConcurrentPortScans = 50;
    private const int PingTimeoutMs = 1000;

    public event Action<ScanResult>? HostDiscovered;
    public event Action<string>? ScanProgress;
    public event Action<int, int>? ProgressChanged;

    private static readonly ConcurrentDictionary<string, string> _arpTable = new();

    public async Task<List<ScanResult>> ScanSubnetAsync(string subnet, CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentBag<ScanResult>();
        var (baseIp, totalHosts) = ParseCidr(subnet);
        var completed = 0;

        ScanProgress?.Invoke($"Starting scan of {subnet} ({totalHosts} hosts)...");
        var semaphore = new SemaphoreSlim(MaxConcurrentPings);
        var tasks = new List<Task>();

        for (uint i = 1; i < totalHosts; i++)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var ipNum = ParseIpToUint(baseIp) + i;
            var ip = UintToIp(ipNum);
            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var ping = new Ping();
                    var reply = await ping.SendPingAsync(ip, PingTimeoutMs);
                    if (reply.Status == IPStatus.Success)
                    {
                        var hostName = await GetHostNameAsync(ip);
                        var mac = await GetMacAddressAsync(ip);
                        var result = new ScanResult
                        {
                            IpAddress = ip,
                            HostName = hostName,
                            MacAddress = mac,
                            IsAlive = true,
                            ResponseTimeMs = reply.RoundtripTime
                        };
                        results.Add(result);
                        HostDiscovered?.Invoke(result);
                        ScanProgress?.Invoke($"[ALIVE] {ip} ({hostName}) - {reply.RoundtripTime}ms");
                    }
                }
                catch { }
                finally
                {
                    Interlocked.Increment(ref completed);
                    ProgressChanged?.Invoke(completed, (int)totalHosts);
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        ScanProgress?.Invoke($"Scan complete. Found {results.Count} active hosts.");
        return results.OrderBy(r => ParseIp(r.IpAddress)).ToList();
    }

    public async Task<List<PortScanResult>> ScanPortsAsync(string ipAddress, int[] ports, CancellationToken cancellationToken = default)
    {
        var results = new ConcurrentBag<PortScanResult>();
        var semaphore = new SemaphoreSlim(MaxConcurrentPortScans);
        var tasks = new List<Task>();

        ScanProgress?.Invoke($"Scanning ports on {ipAddress}...");

        foreach (var port in ports)
        {
            if (cancellationToken.IsCancellationRequested) break;
            await semaphore.WaitAsync(cancellationToken);

            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var client = new TcpClient();
                    var connectTask = client.ConnectAsync(ipAddress, port);
                    var timeoutTask = Task.Delay(2000, cancellationToken);
                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    var isOpen = completedTask == connectTask && client.Connected;

                    results.Add(new PortScanResult
                    {
                        Port = port,
                        ServiceName = GetServiceName(port),
                        IsOpen = isOpen,
                        State = isOpen ? "Open" : "Closed/Filtered"
                    });

                    if (isOpen)
                        ScanProgress?.Invoke($"[OPEN] {ipAddress}:{port} ({GetServiceName(port)})");
                }
                catch
                {
                    results.Add(new PortScanResult { Port = port, ServiceName = GetServiceName(port), IsOpen = false, State = "Closed/Filtered" });
                }
                finally { semaphore.Release(); }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);
        return results.OrderBy(r => r.Port).ToList();
    }

    public async Task<List<IpConflictResult>> DetectIpConflictsAsync(string subnet, CancellationToken cancellationToken = default)
    {
        var conflicts = new List<IpConflictResult>();
        var (baseIp, totalHosts) = ParseCidr(subnet);
        var macMap = new ConcurrentDictionary<string, List<string>>();

        ScanProgress?.Invoke("Checking for IP conflicts...");

        var activeHosts = await ScanSubnetAsync(subnet, cancellationToken);

        foreach (var host in activeHosts)
        {
            if (host.MacAddress != "N/A" && host.MacAddress != "Unknown")
            {
                macMap.AddOrUpdate(host.MacAddress,
                    new List<string> { host.IpAddress },
                    (_, list) => { list.Add(host.IpAddress); return list; });
            }
        }

        foreach (var entry in macMap)
        {
            if (entry.Value.Count > 1)
            {
                conflicts.Add(new IpConflictResult
                {
                    IpAddress = string.Join(", ", entry.Value),
                    Mac1 = entry.Key,
                    HasConflict = true
                });
                ScanProgress?.Invoke($"[CONFLICT] MAC {entry.Key} mapped to IPs: {string.Join(", ", entry.Value)}");
            }
        }

        if (conflicts.Count == 0)
            ScanProgress?.Invoke("No IP conflicts detected.");

        return conflicts;
    }

    public static async Task<List<string>> GetNetworkInfoAsync()
    {
        var info = new List<string>();
        try
        {
            var hostEntry = await Dns.GetHostEntryAsync(Dns.GetHostName());
            info.Add($"Hostname: {hostEntry.HostName}");

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                info.Add(string.Empty);
                info.Add($"--- {nic.Name} ({nic.NetworkInterfaceType}) ---");
                info.Add($"  MAC: {nic.GetPhysicalAddress()}");
                info.Add($"  Speed: {nic.Speed / 1_000_000} Mbps");

                var ipProps = nic.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                        info.Add($"  IPv4: {addr.Address}/{addr.PrefixLength}");

                foreach (var gw in ipProps.GatewayAddresses)
                    info.Add($"  Gateway: {gw.Address}");

                foreach (var dns in ipProps.DnsAddresses)
                    info.Add($"  DNS: {dns}");

                if (ipProps.DhcpServerAddresses.Count > 0)
                    info.Add($"  DHCP: {ipProps.DhcpServerAddresses[0]}");
            }

            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "arp", Arguments = "-a",
                        UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
                    }
                };
                process.Start();
                var arpOutput = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();
                info.Add(string.Empty);
                info.Add("--- ARP Table ---");
                info.Add(arpOutput);
            }
            catch { }
        }
        catch (Exception ex) { info.Add($"Error: {ex.Message}"); }
        return info;
    }

    private static (string baseIp, uint totalHosts) ParseCidr(string subnet)
    {
        var parts = subnet.Split('/', ' ');
        var ip = parts[0];
        var cidr = parts.Length > 1 ? int.Parse(parts[1]) : 24;
        var hostBits = 32 - cidr;
        var totalHosts = (uint)(1 << hostBits);
        return (ip, totalHosts);
    }

    private static uint ParseIpToUint(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4) return 0;
        return (uint)((int.Parse(parts[0]) << 24) | (int.Parse(parts[1]) << 16) |
                      (int.Parse(parts[2]) << 8) | int.Parse(parts[3]));
    }

    private static string UintToIp(uint val)
    {
        return $"{(val >> 24) & 0xFF}.{(val >> 16) & 0xFF}.{(val >> 8) & 0xFF}.{val & 0xFF}";
    }

    private static int ParseIp(string ip)
    {
        var parts = ip.Split('.');
        if (parts.Length != 4) return 0;
        return (int.Parse(parts[0]) << 24) | (int.Parse(parts[1]) << 16) |
               (int.Parse(parts[2]) << 8) | int.Parse(parts[3]);
    }

    private static async Task<string> GetHostNameAsync(string ip)
    {
        try { return (await Dns.GetHostEntryAsync(ip)).HostName; }
        catch { return "Unknown"; }
    }

    private static async Task<string> GetMacAddressAsync(string ip)
    {
        try
        {
            if (_arpTable.TryGetValue(ip, out var cached)) return cached;
            var process = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "arp", Arguments = $"-a {ip}",
                    UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true
                }
            };
            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            process.WaitForExit();

            foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Contains(ip))
                {
                    var p = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 3) { _arpTable.TryAdd(ip, p[1]); return p[1]; }
                }
            }
        }
        catch { }
        return "N/A";
    }

    public static string GetServiceName(int port) => port switch
    {
        21 => "FTP", 22 => "SSH", 23 => "Telnet", 25 => "SMTP", 53 => "DNS",
        80 => "HTTP", 110 => "POP3", 135 => "RPC/DCOM", 139 => "NetBIOS",
        143 => "IMAP", 443 => "HTTPS", 445 => "SMB", 993 => "IMAPS",
        995 => "POP3S", 1433 => "MSSQL", 3306 => "MySQL", 3389 => "RDP",
        5432 => "PostgreSQL", 5900 => "VNC", 8080 => "HTTP-Alt", 8443 => "HTTPS-Alt",
        _ => "Unknown"
    };

    public static int[] GetDefaultPorts() => new[]
    {
        21, 22, 23, 25, 53, 80, 110, 135, 139, 143,
        443, 445, 993, 995, 1433, 3306, 3389, 5432, 5900, 8080
    };
}
