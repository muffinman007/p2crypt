using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using Open.Nat;

namespace P2CNetwork
{
    public class PortHopper
    {
        /// TODO: Raise event to be consumed outside of class (?) Upon successful change. Caller must notify peers of change
        /// TODO: Use Timer to implement 'hopping' behavior (i.e. call ChangePortAsync every x seconds)
        /// TODO: Implement other criteria on when to hop to a new port
        private Random rngInstance = new Random();
        private NatDevice natDevice;

        public PortHopper(NatDevice natDevice, int portSwitchTime) {
            this.PortSwitchTime = portSwitchTime;
            this.natDevice = natDevice;
        }

        #region Public Properties
        public int PortSwitchTime { get; set; }
        public int CurrentPort { get; set; }
        public int LastUsedPort { get; set; }
        #endregion

        #region Methods

        public async void ChangePortAsync(int newPort = 0)
        {
            if (newPort == 0) {
                newPort = rngInstance.Next(1023,65535);
            }
            await natDevice.CreatePortMapAsync(new Mapping(Protocol.Tcp, newPort, newPort, PortSwitchTime, "P2Crypt Portmapping"));
            if (CurrentPort != null) {
                LastUsedPort = CurrentPort;
            }
            CurrentPort = newPort;
        }

        #endregion

        
    }
}
