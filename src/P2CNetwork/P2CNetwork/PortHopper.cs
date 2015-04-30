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
        /// TODO: Asyncify this mess; implement methods
        // Remove these if/when implementing autoproperties - some of these will probably need to be public
        private int portSwitchTime;
        private int currentPort;
        private int lastUsedPort;
        private NatDevice natDevice;

        public PortHopper(NatDevice natDevice, int portSwitchTime) {
            this.portSwitchTime = portSwitchTime;
            this.natDevice = natDevice;
        }

        public void ChangePort()
        {

        }

        public void GeneratePort()
        {

        }
     }
}
