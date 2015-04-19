#region Header

/**
 * 
 * Controls outgoing/incoming data for the program.
 * 
 * NOTE: on disconnect or exiting the main window . The lisenting Socket will try an exception. That exception we can ignore because we want the listening socket to close.
 *       I have the exception pop up just for our own information.
 * 
 * TO DO: 
 *		- on send error to a specific ip , remove the owner of the specific ip and the ip 
 *		- I break the aysnc naming convention on some of the methods. It's so we can decided wheather or not to keep the mehtod as async or not.
 *		
 * 
 * Credit:
 *		UPnP : http://www.codeproject.com/Articles/807861/Open-NAT-A-NAT-Traversal-library-for-NET-and-Mono
 * 
 **/

#endregion Header

using System;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using System.Collections.Concurrent;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Controls;

using P2CCore;
using System.IO;
using P2CCommon;
using System.Net.NetworkInformation;

using Open.Nat;

namespace Network 
{
	public class NetworkServer 
	{

		#region Fields
		// this is how the app will know if there's incoming package waiting to be deliver. 
		public delegate void P2CDeliveryService(Package package);
		public event P2CDeliveryService P2CDS;

		//switch
		bool hasStartedOnce;						// allow for loging back in when user disconnect.
		bool hasPackage;
		bool isUpnpFeatureOn;

		Package arrivedPackage;

		ConcurrentDictionary<Guid, IPublicProfile>		friendsProfileDict;
		ConcurrentDictionary<Guid, IPEndPoint>			friendsIPaddressDict;
		
		Socket server;

		CancellationTokenSource tokenSource;
		Task serverTask;

		IPublicProfile userPublicProfile;
		
		const int recommendedPort = 15;					// this value has to be below 1024
		const int NATPublicPort = 15;					// Assuming the average user is not using software that is listening on 80 (web server, IIS, skype, ect.)
		int defaultPort;
		int backlog;

		System.Windows.Controls.Control crossCommuniationHack;

		// allow UPnP feature
		NatDevice natDevice;

		#endregion Fields


		#region Constructors

		public NetworkServer(IPublicProfile userPublicProfile, System.Windows.Controls.Control control, int port = 15, int backlog = 100)
		{	
			this.userPublicProfile = userPublicProfile;
			crossCommuniationHack = control;

			hasStartedOnce = false;
			hasPackage = false;
			isUpnpFeatureOn = false;
	
			friendsProfileDict		= new ConcurrentDictionary<Guid,IPublicProfile>();
			friendsIPaddressDict	= new ConcurrentDictionary<Guid,IPEndPoint>();

			defaultPort = port;	
			this.backlog = backlog;	
		}

		#endregion Constructors


		#region Properties


		/// <summary>
		/// Allow user to get the most recent arrived package. Could removed this in the future?
		/// </summary>
		public Package Package
		{
			get
			{
				if(hasPackage)					
					return arrivedPackage;
				else
					return null;						// should we return an empty package?
			}
		}

		#endregion Properties


		#region Methods

		/// <summary>
		/// Start the process in which NetworkServer listen for incoming connection. Runs on a Separate Thread.
		/// </summary>
		public async void StartAsync()
		{
			if(serverTask != null)
				return;			

			if(!hasStartedOnce){
				Task t1 = Task.Run(async ()=>
							{
								IPAddress localIP = null;
								foreach(var ip in Dns.GetHostAddresses(Dns.GetHostName())){
									if(ip.AddressFamily == AddressFamily.InterNetwork){
										localIP = ip;
										break;
									}
								}

								// first check if default port is not the recommendedPort
								if(!(defaultPort == recommendedPort))
								{									
									// make sure user is behind a NAT device
									// in the future maybe use the Open.NAT built in NatDeviceNotFoundException ?
									if(IsLanIP(localIP))
									{
										// set up UPnP
										var discoverer = new NatDiscoverer();

										// using SSDP protocol, it discovers NAT device
										natDevice = await discoverer.DiscoverDeviceAsync();

										// uncomment for logging. recode the line properly
										//Console.WriteLine("The external IP Address is: {0} ", await device.GetExternalIPAsync());

										// create a new mapping in the router, external_ip:80 -> host_machine:defaultPort
										// We are assuming advance user know what they are doing
										// maybe in the future catch MappingException : http://www.codeproject.com/Articles/807861/Open-NAT-A-NAT-Traversal-library-for-NET-and-Mono
										await natDevice.CreatePortMapAsync(new Mapping(Protocol.Tcp, defaultPort, NATPublicPort));										
										isUpnpFeatureOn = true;
									}
								}								

								server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
								server.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
								server.Bind(new IPEndPoint(IPAddress.Any, defaultPort));
								server.LingerState = new LingerOption(false, 0);
								server.Listen(backlog);	
							});

				await t1;
			}		

			tokenSource = new CancellationTokenSource();
			
			serverTask = Task.Factory.StartNew(()=>
			{
				CancellationToken cancelToken = tokenSource.Token;
				try{
					while(!cancelToken.IsCancellationRequested)
					{
						Socket client = server.Accept();

						Task.Factory.StartNew(()=>{ ProcessIncomingData(client); });
					}
				}
				catch(SocketException ex)
				{
					Task.Factory.StartNew(()=>
					{ 
						MessageBox.Show("Inside Server Task: " + Environment.NewLine +
										"Exception: " + ex.Message + Environment.NewLine +
										"Stack Track: " + ex.StackTrace + Environment.NewLine +
										"Exception type: " + ex.GetType().ToString() + Environment.NewLine +
										"Inner Exception: " + ex.InnerException);
					});
				}

				Task.Factory.StartNew(()=>{ MessageBox.Show("Debug inside serverTask. exiting."); });
			});

			if(hasStartedOnce)
			{
				// let friends know user reconnected
				Task.Factory.StartNew(()=>
				{
					foreach(var friendInfo in friendsIPaddressDict){
						DeliverConnectRequest(friendInfo.Value);
					}
				});
			}

			hasStartedOnce = true;			
		}


		/// <summary>
		/// Gracefully disconnect the NetworkServer. Disconnect is instant. Disconnect stop you from receiving data.
		/// </summary>
		public void Disconnect(bool isWindowExiting)
		{
			// prevent calling Disconnect() before serverTask started
			if(serverTask == null)
				return;

			if(isWindowExiting)
				SendOther(PackageStatus.LogOff, null); // exiting so doesn't matter if ui becomes non-responsive, UI is already hidden
			else
				Task.Factory.StartNew(()=>{ SendOther(PackageStatus.LogOff, null); });

			//server.Shutdown(SocketShutdown.Both);
			server.Close();
			//server = null;

			tokenSource.Cancel();

			serverTask = null;
		}

		/// <summary>
		/// Send the package to all connected node. 
		/// </summary>
		/// <param name="status">Allow the program to know the content of the package</param>
		/// <param name="strData">Either it's the message to be sent out or user's old profile nick</param>
		public async void Send(PackageStatus status, string strData)
		{
			if(status == PackageStatus.Message)
				SendMessage(strData);
			else
				SendOther(status, strData);
		}

		async void SendMessage(string strData){
			 // There's a better way to do this so we can catch all the socketException and handle it correctly.
			// When we catch an exception most likely user had disconnected and we need to update that change
			// with the program.			
			IPEndPoint friendIP;
			Socket remoteSocket = null;
			byte[] outgoingData = null;

			try{									
				foreach(var friendInfo in friendsIPaddressDict)
				{
					using(MemoryStream ms = new MemoryStream())   // might need to handle memory error in the future
					{            
						BinaryFormatter bf = new BinaryFormatter();
						friendIP = friendInfo.Value;

						IPublicProfile outgoingProfile;
						friendsProfileDict.TryGetValue(friendInfo.Key, out outgoingProfile);

						Package deliveryPackage = new Package(
							null,
							new Tuple<Guid,string,string>(Guid.Empty, userPublicProfile.UserNick, string.Empty),
							PackageStatus.Message,
							outgoingProfile.Encrypt(Encoding.UTF8.GetBytes(strData)),
							0
						);					

						bf.Serialize(ms, deliveryPackage);
						ms.Seek(0, SeekOrigin.Begin);
						outgoingData = ms.ToArray();
						
						remoteSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
						remoteSocket.Connect(friendIP);
						remoteSocket.Send(outgoingData, 0, outgoingData.Length, SocketFlags.None);
						remoteSocket.Close();
						remoteSocket = null;
						outgoingData = null;
					}
				}
			}
			catch(SocketException se)
			{
				Task.Factory.StartNew(()=>
				{
					string str;
					if(remoteSocket == null)
						str = "IP not available";
					else
						str = remoteSocket.RemoteEndPoint.ToString();

					MessageBox.Show("Error while sending data" + Environment.NewLine +
									"Remote ip: " + str + Environment.NewLine +
									"Socket Exception: " + Environment.NewLine +
									se.Message + Environment.NewLine +
									"Error Code: " + se.NativeErrorCode + Environment.NewLine);
				});
			
				remoteSocket.Dispose();
				remoteSocket = null;
				outgoingData = null;
			}
			catch(Exception ex)
			{
				Task.Factory.StartNew(()=>
				{
					MessageBox.Show("Error while sending data" + Environment.NewLine +
									ex.Message + Environment.NewLine);
				});
			}
		}

		async void SendOther(PackageStatus status, string strData)
		{
			// There's a better way to do this so we can catch all the socketException and handle it correctly.
			// When we catch an exception most likely user had disconnected and we need to update that change
			// with the program.			
			IPEndPoint friendIP;	
			byte[] outgoingData = null;
			Socket remoteSocket = null;

			try
			{			
				using(MemoryStream ms = new MemoryStream())  // might need to handle memory error in the future
				{            
					BinaryFormatter bf = new BinaryFormatter();

					Package deliveryPackage = null;
					if(status == PackageStatus.NickUpdate)
					{
						deliveryPackage = new Package(
							null,
							new Tuple<Guid,string, string>(userPublicProfile.GlobalId, strData, userPublicProfile.UserNick),
							PackageStatus.NickUpdate,
							null,
							0
						);
					}				
					else
					{ // PackageStatus.LogOff
						  // PackageStatus.Connect is taken care of in ConnectToRemote()
						deliveryPackage = new Package(
							null,
							new Tuple<Guid,string,string>(userPublicProfile.GlobalId, userPublicProfile.UserNick, string.Empty),
							PackageStatus.LogOff,
							null,
							0
						);
					}

					bf.Serialize(ms, deliveryPackage);
					ms.Seek(0, SeekOrigin.Begin);
					outgoingData = ms.ToArray();					
				}				

				foreach(var friendInfo in friendsIPaddressDict)
				{
					friendIP = friendInfo.Value;

					IPublicProfile outgoingProfile;
					friendsProfileDict.TryGetValue(friendInfo.Key, out outgoingProfile);
						
					remoteSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					remoteSocket.Connect(friendIP);
					remoteSocket.Send(outgoingData, 0, outgoingData.Length, SocketFlags.None);
					remoteSocket.Close();
					remoteSocket = null;
				}
			}
			catch(SocketException se)
			{
				Task.Factory.StartNew(()=>
				{
					string str;
					if(remoteSocket == null)
						str = "IP not available";
					else
						str = remoteSocket.RemoteEndPoint.ToString();

					MessageBox.Show("Error while sending data" + Environment.NewLine +
									"Remote ip: " + str + Environment.NewLine +
									"Socket Exception: " + Environment.NewLine +
									se.Message + Environment.NewLine +
									"Error Code: " + se.NativeErrorCode + Environment.NewLine);
				});

				remoteSocket.Dispose();
				remoteSocket = null;
			}
			catch(Exception ex)
			{
				Task.Factory.StartNew(()=>
				{
					MessageBox.Show("Error while sending data" + Environment.NewLine +
									ex.Message + Environment.NewLine);
				});
			}	
		}



		// for now if the same user keep connecting, do nothing. Might need to change in the future
		public void ProcessIncomingData(Socket client)
		{			
			Package deliveryPackage = null;
			object testIncomingData = null;

			int buffer = 1024;
			int bytesRead = 0;
			byte[] data = new byte[buffer];

			using(MemoryStream ms = new MemoryStream())
			{
				while( (bytesRead = client.Receive(data, 0, buffer, SocketFlags.None)) > 0)
					ms.Write(data, 0, bytesRead);

				ms.Seek(0, SeekOrigin.Begin);

				BinaryFormatter bf = new BinaryFormatter();
				testIncomingData = (object)bf.Deserialize(ms);
			}			


			// test and reject unwanted data
			if(testIncomingData is Package)
				deliveryPackage = (Package)testIncomingData;
			else
			{
				// reject the data
				Task.Factory.StartNew(()=>
				{ 
					MessageBox.Show(
						"Unknown incoming raw data; Rejection." + Environment.NewLine +
						"Remote End Point: " + client.RemoteEndPoint.ToString() 
					); 
				});

				return;
			}



			switch(deliveryPackage.PackageStatus)
			{
				case PackageStatus.Connect:
					if(friendsProfileDict.ContainsKey(deliveryPackage.PublicProfile.GlobalId))
						return;

					// get the remote IPEndPoint
					IPEndPoint remoteAdd = new IPEndPoint( ((IPEndPoint)client.RemoteEndPoint).Address, deliveryPackage.Port );
					friendsIPaddressDict.TryAdd(deliveryPackage.PublicProfile.GlobalId, remoteAdd);

					friendsProfileDict.TryAdd(deliveryPackage.PublicProfile.GlobalId, deliveryPackage.PublicProfile);

					Package replyPackage = new Package(userPublicProfile, null, PackageStatus.Connect, null, (isUpnpFeatureOn? NATPublicPort : defaultPort) );
					using(MemoryStream ms = new MemoryStream()){
						BinaryFormatter bf = new BinaryFormatter();
						bf.Serialize(ms, replyPackage);
						ms.Seek(0, SeekOrigin.Begin);
						byte[] raw = ms.ToArray();
						
						Socket replySocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
						replySocket.Connect(remoteAdd);
						int byteSent = replySocket.Send(raw, 0, raw.Length, SocketFlags.None);
						replySocket.Close();
						replySocket = null;
					}
					break;

				case PackageStatus.Message:
					arrivedPackage = deliveryPackage;
					hasPackage = true;
					break;

				case PackageStatus.NickUpdate:
					friendsProfileDict[deliveryPackage.Information.Item1].UserNick = deliveryPackage.Information.Item2;
					break;

				case PackageStatus.LogOff:
					IPEndPoint dummyEndPoint;
					IPublicProfile dummyProfile;
					friendsIPaddressDict.TryRemove(deliveryPackage.Information.Item1, out dummyEndPoint);
					friendsProfileDict.TryRemove(deliveryPackage.Information.Item1, out dummyProfile);
					break;			
			}		

			crossCommuniationHack.InvokeIfRequired(()=>
			{
			   P2CDS(deliveryPackage);					// let subscriber know they have a package
			});
		
		}	


		// if user enter wrong ip , do nothing. Maybe IP error correction should be done at the main program?
		// if user enter the wrong IP return. need to implement a better way to let user know ip address is wrong
		public void ConnectToRemote(string ip)
		{
			string[] ipParts = ip.Split(new string[]{":"}, StringSplitOptions.None);

			IPAddress remoteIP;

			if(!IPAddress.TryParse(ipParts[0], out remoteIP))
				return;

			int port;

			if(!int.TryParse(ipParts[1], out port))
				return;
			else if(port < 1 || port > ushort.MaxValue)
				return;

			DeliverConnectRequest(new IPEndPoint(remoteIP, port));	
		}

		public void DeliverConnectRequest(IPEndPoint remoteEndPoint)
		{
			Package deliveryPackage = new Package(userPublicProfile, null, PackageStatus.Connect, null, (isUpnpFeatureOn? NATPublicPort : defaultPort) );
			
			try
			{
				using(MemoryStream ms = new MemoryStream())
				{
					BinaryFormatter bf = new BinaryFormatter();

					bf.Serialize(ms, deliveryPackage);
					ms.Seek(0, SeekOrigin.Begin);

					byte[] data = ms.ToArray();

					Socket remoteSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					remoteSocket.Connect(remoteEndPoint);
					remoteSocket.Send(data, 0, data.Length, SocketFlags.None);
					remoteSocket.Close();
					remoteSocket = null;
				}
			}
			catch(Exception ex)
			{
				Task.Factory.StartNew(()=>
				{
					MessageBox.Show("Inside DeliverConnectRequest." + Environment.NewLine +
									"Remote End Point: " + remoteEndPoint.ToString() + Environment.NewLine +
									"Exception Type: " + ex.GetType() + Environment.NewLine +
									"Message: " + ex.Message + Environment.NewLine);
				});
			}
		}


		// code from http://stackoverflow.com/questions/7232287/check-if-ip-is-in-lan-behind-firewalls-and-routers
		bool IsLanIP(IPAddress address)
		{
			var ping = new Ping();
			var rep = ping.Send(address, 100, new byte[]{1}, new PingOptions()
			{
				DontFragment = true,
				Ttl = 1
			});

			return rep.Status != IPStatus.TtlExpired && rep.Status != IPStatus.TimedOut && rep.Status != IPStatus.TimeExceeded;
		}


		public void Exit()
		{		
			arrivedPackage = null;
			friendsIPaddressDict.Clear();
			friendsProfileDict.Clear();
			crossCommuniationHack = null;
		}		

		#endregion Methods

	}
}
