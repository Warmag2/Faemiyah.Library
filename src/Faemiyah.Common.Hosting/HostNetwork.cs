using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Faemiyah.Common.Hosting;

/// <summary>
/// Network information about the current host.
/// </summary>
public static class HostNetwork
{
    /// <summary>
    /// The first IPv4 address of the current host, for example to advertise an Orleans silo. Works in Docker and
    /// Kubernetes, where the host name resolves to the container address.
    /// </summary>
    /// <returns>The IPv4 address.</returns>
    /// <exception cref="InvalidOperationException">The host has no IPv4 address.</exception>
    public static IPAddress GetHostIp()
    {
        return Dns.GetHostEntry(Dns.GetHostName()).AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? throw new InvalidOperationException("Could not resolve an IPv4 address for this host.");
    }
}
