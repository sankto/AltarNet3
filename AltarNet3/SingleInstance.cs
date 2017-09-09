using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace AltarNet {
	/// <summary>
	/// Is returned alongside an instance of the ArgumentsReceived event.
	/// </summary>
	public class ArgumentsEventArgs : EventArgs {
		/// <summary>
		/// The arguments sent from the non-unique instance.
		/// </summary>
		public readonly string[] Args;
		/// <summary>
		/// Create an ArgumentsEventArgs.
		/// </summary>
		/// <param name="args">The arguments</param>
		public ArgumentsEventArgs(string[] args) {
			Args = args;
		}
	}

	internal class InstanceInfo {
		public int ArgsLength = -1;
		public List<string> Args = new List<string>();
	}

	/// <summary>
	/// Allow the user to check if their application run on a single instance, on the local machine.
	/// </summary>
	public class SingleInstance : IDisposable {
		private TcpClientHandler Client { get; set; }
		private TcpServerHandler Server { get; set; }

		/// <summary>
		/// Get the state of the instance.
		/// </summary>
		public bool IsSingle { get; private set; }

		/// <summary>
		/// The event is called when a non-unique instance send it's arguments to the unique one.
		/// </summary>
		public event EventHandler<ArgumentsEventArgs> ArgumentsReceived;

		/// <summary>
		/// Create a single instance.
		/// </summary>
		/// <param name="port">The port to listen to locally</param>
		/// <param name="args">Optional, the arguments to send if the instance is non-unique</param>
		/// <param name="readTimeout">A timeout value in milliseconds until a non-unique client is automatically disconnected</param>
		/// <param name="tryTimeout">A timeout value in milliseconds that represent the amout of time it will try to find and connect to the unique instance, if any</param>
		public SingleInstance(int port, string[] args = null, int readTimeout = 5000, int tryTimeout = 500) {
			Client = new TcpClientHandler(IPAddress.Loopback, port);
			Server = new TcpServerHandler(IPAddress.Loopback, port, readTimeout);
			Server.ReceivedFull += Server_ReceivedFull;
			Server.Connected += Server_Connected;

			var timedOut = !TryConnect().Wait(tryTimeout);
			if (timedOut)
				IsSingle = true;
			if (IsSingle)
				Server.Start();
			else {
				if (args != null) {
					Client.Send(BitConverter.GetBytes(args.Length));
					foreach (var arg in args)
						Client.Send(Encoding.Unicode.GetBytes(arg));
				}
				Client.Disconnect();
			}
		}

		void Server_ReceivedFull(object sender, TcpReceivedEventArgs e) {
			var instInfo = e.Client.Tag as InstanceInfo;
			if (instInfo.ArgsLength == -1)
				BitConverter.ToInt32(e.Data, 0);
			else {
				instInfo.Args.Add(Encoding.Unicode.GetString(e.Data));
				if (instInfo.Args.Count == instInfo.ArgsLength) {
					if (ArgumentsReceived != null)
						ArgumentsReceived(this, new ArgumentsEventArgs(instInfo.Args.ToArray()));
					Server.DisconnectClient(e.Client);
				}
			}
		}

		void Server_Connected(object sender, TcpEventArgs e) {
			e.Client.Tag = new InstanceInfo();
		}

		private async Task TryConnect() {
			IsSingle = !await Client.ConnectAsync();
		}

		/// <summary>
		/// Dispose of the single instance.
		/// </summary>
		public void Dispose() {
			if (Client != null) {
				Client.Disconnect();
				Client = null;
			}
			if (Server != null) {
				Server.Stop();
				Server = null;
			}
		}
	}
}
