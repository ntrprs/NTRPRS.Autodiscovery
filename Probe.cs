using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace NTRPRS.Autodiscovery
{
    /// <summary>
    /// Counterpart of the beacon, searches for beacons
    /// </summary>
    /// <remarks>
    /// The beacon list event will not be raised on your main _thread!
    /// </remarks>
    public class Probe : IDisposable
    {
        /// <summary>
        /// Remove beacons older than this
        /// </summary>
        private static readonly TimeSpan BeaconTimeout = new TimeSpan(0, 0, 0, 5); // seconds

        public event Action<IEnumerable<BeaconLocation>> BeaconsUpdated;

        private readonly Thread _thread;
        private readonly EventWaitHandle _waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);
        private readonly UdpClient _udp = new UdpClient();
        private IEnumerable<BeaconLocation> _currentBeacons = Enumerable.Empty<BeaconLocation>();

        private bool _running = true;

        public Probe(string beaconType)
        {
            _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            BeaconType = beaconType;
            _thread = new Thread(BackgroundLoop) { IsBackground = true };

            _udp.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
            try 
            {
                _udp.AllowNatTraversal(true);
            }
            catch (SocketException ex)
            {
                Debug.WriteLine("Error switching on NAT traversal: " + ex.Message);
            }

            _udp.BeginReceive(ResponseReceived, null);
        }

        public void Start()
        {
            _thread.Start();
        }

        private void ResponseReceived(IAsyncResult ar)
        {
            var remote = new IPEndPoint(IPAddress.Any, 0);
            var bytes = _udp.EndReceive(ar, ref remote);

            var typeBytes = Beacon.Encode(BeaconType).ToList();
            Debug.WriteLine(string.Join(", ", typeBytes.Select(_ => (char)_)));
            if (Beacon.HasPrefix(bytes, typeBytes))
            {
                try
                {
                    var portBytes = bytes.Skip(typeBytes.Count).Take(2).ToArray();
                    var port      = (ushort)IPAddress.NetworkToHostOrder((short)BitConverter.ToUInt16(portBytes, 0));
                    var payload   = Beacon.Decode(bytes.Skip(typeBytes.Count + 2));
                    NewBeacon(new BeaconLocation(new IPEndPoint(remote.Address, port), payload, DateTime.Now));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }

            _udp.BeginReceive(ResponseReceived, null);
        }

        public string BeaconType { get; }

        private void BackgroundLoop()
        {
            while (_running)
            {
                BroadcastProbe();
                _waitHandle.WaitOne(2000);
                PruneBeacons();
            }
        }

        private void BroadcastProbe()
        {
            var probe = Beacon.Encode(BeaconType).ToArray();
            _udp.Send(probe, probe.Length, new IPEndPoint(IPAddress.Broadcast, Beacon.DiscoveryPort));
        }

        private void PruneBeacons()
        {
            var cutOff = DateTime.Now - BeaconTimeout;
            var oldBeacons = _currentBeacons.ToList();
            var newBeacons = oldBeacons.Where(_ => _.LastAdvertised >= cutOff).ToList();
            if (EnumsEqual(oldBeacons, newBeacons)) return;

            var u = BeaconsUpdated;
            u?.Invoke(newBeacons);
            _currentBeacons = newBeacons;
        }

        private void NewBeacon(BeaconLocation newBeacon)
        {
            var newBeacons = _currentBeacons
                .Where(_ => !_.Equals(newBeacon))
                .Concat(new [] { newBeacon })
                .OrderBy(_ => _.Data)
                .ThenBy(_ => _.Address, IpEndPointComparer.Instance)
                .ToList();
            var u = BeaconsUpdated;
            u?.Invoke(newBeacons);
            _currentBeacons = newBeacons;
        }

        private static bool EnumsEqual<T>(IEnumerable<T> xs, IEnumerable<T> ys)
        {
            var enumerable = xs as IList<T> ?? xs.ToList();
            return enumerable.Zip(ys, (x, y) => x.Equals(y)).Count() == enumerable.Count;
        }

        public void Stop()
        {
            _running = false;
            _waitHandle.Set();
            _thread.Join();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
