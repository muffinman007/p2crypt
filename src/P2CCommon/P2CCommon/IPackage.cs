using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P2CCommon 
{
	public interface IPackage 
	{
		IPublicProfile PublicProfile{ get; }
 
		byte[] Data{ get; }

		PackageStatus PackageStatus{ get; }

		Tuple<Guid, string, string> Information{ get; }

		int Port{ get; }

	}


	public enum PackageStatus
	{
		Connect,
		LogOff,
		NickUpdate,
		Message
	}

}
