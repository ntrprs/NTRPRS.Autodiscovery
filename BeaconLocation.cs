using System;
using System.Net;

namespace NTRPRS.Autodiscovery
{
    /// <summary>
    /// Class that represents a discovered beacon
    /// </summary>
    public class BeaconLocation 
    {
        public BeaconLocation(IPEndPoint address, string data, DateTime lastAdvertised)
        {
            Address = address;
            Data    = data;
            LastAdvertised = lastAdvertised;
        }

        public IPEndPoint Address { get; }
        public string Data { get; }
        public DateTime LastAdvertised { get; private set; }

        public override string ToString()
        {
            return Data;
        }

        protected bool Equals(BeaconLocation other)
        {
            return Equals(Address, other.Address);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BeaconLocation) obj);
        }

        public override int GetHashCode()
        {
            return Address?.GetHashCode() ?? 0;
        }
    }
}
