﻿using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace LunaCommon
{
    public class LunaNetUtils
    {
        private static NetworkInterface GetNetworkInterface()
        {
            var nics = NetworkInterface.GetAllNetworkInterfaces();
            if (nics.Length < 1)
                return null;

            NetworkInterface best = null;
            foreach (var adapter in nics)
            {
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback || adapter.NetworkInterfaceType == NetworkInterfaceType.Unknown)
                    continue;
                if (!adapter.Supports(NetworkInterfaceComponent.IPv4))
                    continue;
                if (best == null)
                    best = adapter;
                if (adapter.OperationalStatus != OperationalStatus.Up)
                    continue;

                // make sure this adapter has any ipv4 addresses
                var properties = adapter.GetIPProperties();
                foreach (var unicastAddress in properties.UnicastAddresses)
                {
                    if (unicastAddress?.Address != null && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        // Yes it does, return this network interface.
                        return adapter;
                    }
                }
            }
            return best;
        }

        public static IPAddress GetMyAddress()
        {
            var ni = GetNetworkInterface();
            if (ni == null)
            {
                return IPAddress.Loopback;
            }

            var properties = ni.GetIPProperties();
            foreach (var unicastAddress in properties.UnicastAddresses)
            {
                if (unicastAddress?.Address != null && unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return unicastAddress.Address;
                }
            }
            
            return IPAddress.Loopback;
        }

        public static string GetOwnExternalIpAddress()
        {
            var currentIpAddress = TryGetIpAddress("http://ip.42.pl/raw");

            if (string.IsNullOrEmpty(currentIpAddress))
                currentIpAddress = TryGetIpAddress("https://api.ipify.org/");
            if (string.IsNullOrEmpty(currentIpAddress))
                currentIpAddress = TryGetIpAddress("http://httpbin.org/ip");
            if (string.IsNullOrEmpty(currentIpAddress))
                currentIpAddress = TryGetIpAddress("http://checkip.dyndns.org");

            return currentIpAddress;
        }

        private static string TryGetIpAddress(string url)
        {
            try
            {
                using (var client = new WebClient())
                using (var stream = client.OpenRead(url))
                {
                    if (stream == null) return null;
                    using (var reader = new StreamReader(stream))
                    {
                        var ipRegEx = new Regex(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b");
                        var result = ipRegEx.Matches(reader.ReadToEnd());

                        if (IPAddress.TryParse(result[0].Value, out var ip))
                            return ip.ToString();
                    }
                }
            }
            catch
            {
                // ignored
            }

            return null;
        }
    }
}
