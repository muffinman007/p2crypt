using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

using Network;
using P2CCore;
using P2CCommon;
using System.IO;

namespace NetworkTest
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow:Window {

		#region Fields

		int publicPort = 6886;

		bool hasServerStarted;
		bool isWindowClosing;

		NetworkServer networkServer; 
		UserAccount userAccount;

		int messageSentCounter = 0;	

		#endregion Fields

		public MainWindow() {
			InitializeComponent();
			
			for(int i = 0; i < 256; ++i){
				cbFirstIP.Items.Add(i);
				cbSecondIP.Items.Add(i);
				cbThirdIP.Items.Add(i);
				cbFourthIP.Items.Add(i);
			}

			cbFirstIP.SelectedIndex		= 0;
			cbSecondIP.SelectedIndex	= 0;
			cbThirdIP.SelectedIndex		= 0;
			cbFourthIP.SelectedIndex	= 0;

			hasServerStarted = false;
			isWindowClosing = false;
		}

		private async void btnStart_Click(object sender, RoutedEventArgs e) {
			if(userAccount == null)
				userAccount = new UserAccount(){UserNick = txtNick.Text};
			
			if(networkServer == null){
				if(String.IsNullOrEmpty(txtCustomPort.Text))
					networkServer = new NetworkServer(userAccount.PublicProfile, lbHack, publicPort); 
				else
					networkServer = new NetworkServer(userAccount.PublicProfile, lbHack, int.Parse(txtCustomPort.Text));

				networkServer.P2CDS += new NetworkServer.P2CDeliveryService(PackageHandler);
			}

			
			await networkServer.StartAsync();			

			btnStart.IsEnabled = false;
			hasServerStarted = true;

			btnStop.IsEnabled = true;
			btnRemoteConnect.IsEnabled = true;
			btnSend.IsEnabled = true;
			btnChangeNick.IsEnabled = true;

			txtStatus.Text = "Server running";
		}

		private void btnStop_Click(object sender, RoutedEventArgs e) {
			
			if(isWindowClosing)
				networkServer.Disconnect(true);
			else
				networkServer.Disconnect(false);

			hasServerStarted = false;

			if(!isWindowClosing){
				btnStart.IsEnabled = true;

				btnStop.IsEnabled = false;
				btnRemoteConnect.IsEnabled = false;
				btnSend.IsEnabled = false;
				btnChangeNick.IsEnabled = false;

				txtStatus.Text = "Server stopped";
			}			
		}


		private void txtNick_TextChanged(object sender, TextChangedEventArgs e) {
			// Require a nick before server can be started for the first time
			if(String.IsNullOrEmpty(txtNick.Text) || String.IsNullOrWhiteSpace(txtNick.Text)){
				if(!hasServerStarted)
					btnSend.IsEnabled = false;
				else
					btnChangeNick.IsEnabled = false;
			}
			else{
				if(!hasServerStarted)
					btnStart.IsEnabled = true;
				else
					btnChangeNick.IsEnabled = true;
			}
		}

		private async void btnSend_Click(object sender, RoutedEventArgs e) {
			if(String.IsNullOrWhiteSpace(txtMessage.Text) || String.IsNullOrEmpty(txtMessage.Text))
				return;

			String message = txtMessage.Text;

			await Task.Run(()=>{ 
				networkServer.Send(PackageStatus.Message, message); 
			}).ConfigureAwait(false);

			txtChatWindow.InvokeIfRequired(()=>{
				txtChatWindow.AppendText(userAccount.UserNick + ":  " + txtMessage.Text + Environment.NewLine);
				txtMessage.Clear();
				txtMessage.Focus();
				txtStatus.Text = ++messageSentCounter + " Message(s) sent";
			});					
		}


		async void PackageHandler(Package package){
			string str = string.Empty;

			if(package.PackageStatus == PackageStatus.LogOff || package.PackageStatus == PackageStatus.NickUpdate)
				str = txtFriendsList.Text;

			// some user may have thousands of nick on their list so it may be better to do the split on a different thread
			Task.Run(()=>{
				string[] nickArray = null;
				if(!string.IsNullOrEmpty(str))
					nickArray = str.Split(new string[]{Environment.NewLine}, StringSplitOptions.RemoveEmptyEntries);
				
				switch(package.PackageStatus){
					case PackageStatus.Connect:
						txtChatWindow.InvokeIfRequired(()=>{
							txtChatWindow.AppendText(package.PublicProfile.UserNick + " joined." + Environment.NewLine);
							txtFriendsList.AppendText(package.PublicProfile.UserNick + Environment.NewLine);
						});
						break;

					case PackageStatus.LogOff:
						IEnumerable<string> listOfNicks = from nick in nickArray
														  where !String.Equals(nick, package.Information.Item2)
														  select nick;
						txtChatWindow.InvokeIfRequired(()=>{
							txtChatWindow.AppendText(package.Information.Item2 + " logged out." + Environment.NewLine);
							txtFriendsList.Clear();
							foreach(var nick in listOfNicks)
								txtFriendsList.AppendText(nick + Environment.NewLine);
						});
						break;

					case PackageStatus.NickUpdate:
						// item2 = old nick
						// item3 = new nick
						for(int i = 0; i < nickArray.Length; ++i){
							if(string.Equals(nickArray[i], package.Information.Item2)){
								nickArray[i] = package.Information.Item3;
								break;
							}
						}
						txtChatWindow.InvokeIfRequired(()=>{
							txtChatWindow.AppendText(package.Information.Item2 + " changed to " + package.Information.Item3 + Environment.NewLine);
							txtFriendsList.Clear();
							foreach(var nick in nickArray)
								txtFriendsList.AppendText(nick + Environment.NewLine);
						});
						break;

					case PackageStatus.Message:
						string message = Encoding.UTF8.GetString(userAccount.Decrypt(package.Data));
						txtChatWindow.InvokeIfRequired(()=>{
							txtChatWindow.AppendText(package.Information.Item2 + ":  " + message + Environment.NewLine);
						});
						break;
				}				
			});
		}

		private void btnRemoteConnect_Click(object sender, RoutedEventArgs e) {
			string ip = cbFirstIP.Text + "." + cbSecondIP.Text + "." + cbThirdIP.Text + "." + cbFourthIP.Text + ":" + (String.IsNullOrEmpty(txtCustomPort.Text)? publicPort.ToString() : txtPort.Text);
			
			Task.Run(()=>{
				networkServer.ConnectToRemote(ip);
			});
		}

		private void btnChangeNick_Click(object sender, RoutedEventArgs e) {
			string oldNick = userAccount.UserNick;
			userAccount.UserNick = txtNick.Text;

			// send nick data old nick + new nick
			Task.Run(()=>{ networkServer.Send(PackageStatus.NickUpdate, oldNick + " " + txtNick.Text); });
			txtStatus.Text = "Nick change: " + oldNick + " to " + txtNick.Text;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			// hide the window giving it time to cleaning release resources in networkServer
			this.Hide();
			isWindowClosing = true;
			btnStop_Click(this, null);

			networkServer.P2CDS -= PackageHandler;
			networkServer.Exit();
			networkServer = null;
		}

	}

}
