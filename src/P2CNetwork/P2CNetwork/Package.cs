#region Header

/*
 * The delivery package. Signature required.
 * This class is the class that will be seralized before it get sent through the net.
 */

#endregion Header

using System;
using P2CCommon;

namespace Network
{
    [Serializable]
    public class Package : IPackage 
	{

		#region Fields

		IPublicProfile publicProfile;
		byte[] data;
		PackageStatus status;
		Tuple<Guid, string, string> information;
		int port;

		#endregion Fields


		#region Properties

		public IPublicProfile PublicProfile 
		{
			get{ return publicProfile; }
		}

		public byte[] Data 
		{
			get { return data; }
		}

		public PackageStatus PackageStatus
		{
			get{ return status; }
		}

		public Tuple<Guid, string, string> Information
		{
			get{ return information; }
		}

		public int Port
		{
			get{ return port; }
		}
		
        #endregion Properties

        #region Constructors

        public Package(IPublicProfile userProfile, Tuple<Guid, string, string> info, PackageStatus status, byte[] data, int port)
        {
            this.publicProfile = userProfile;
			this.information = info;
			this.status = status;
            this.data = data;
			this.port = port;
        }

        #endregion Constructors
		
	}
}