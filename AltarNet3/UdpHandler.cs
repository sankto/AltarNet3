using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace AltarNet {
	/// <summary>
	/// Create an UDP client, for listening and sending packets.
	/// </summary>
	public class UdpHandler : IDisposable {
		private UdpClient Soc;
		private volatile bool isListening;

		/// <summary>
		/// Get the endpoint of which we listening from.
		/// </summary>
		public IPEndPoint ListenEndPoint { get; private set; }
		/// <summary>
		/// Get the disposed state of the object.
		/// </summary>
		public bool IsDisposed { get; private set; }
		/// <summary>
		/// Get the listening state of the client.
		/// </summary>
		public bool IsListening { get { return isListening; } }
		/// <summary>
		/// Get the socket.
		/// </summary>
		public UdpClient Client { get { return Soc; } }

		/// <summary>
		/// Called when a packet is received.
		/// </summary>
		public event EventHandler<UdpPacketReceivedEventArgs> Received;

		/// <summary>
		/// Construct an UDP client, listening on an endpoint and starting to listen now (or not).
		/// </summary>
		/// <param name="listenIPendp">The endpoint to listen to</param>
		/// <param name="startListening">If true, will call Listen(true)</param>
		public UdpHandler(IPEndPoint listenIPendp, bool startListening = false) {
			ListenEndPoint = listenIPendp;
			Listen(startListening);
		}

		/// <summary>
		/// Send a packet of informations to the specified endpoint.
		/// </summary>
		/// <param name="data">The information to send</param>
		/// <param name="to">The endpoint who's the packet is sent to</param>
		/// <param name="length">Optional, specify the portion of the data that is sent</param>
		public void Send(byte[] data, IPEndPoint to, int length = -1) {
			Soc.Send(data, length == -1 ? data.Length : length, to);
		}

		/// <summary>
		/// Send a packet of informations to the specified endpoint.
		/// </summary>
		/// <param name="data">The information to send</param>
		/// <param name="to">The endpoint who's the packet is sent to</param>
		/// <param name="length">Optional, specify the portion of the data that is sent</param>
		/// <returns>A Task</returns>
		public async Task SendAsync(byte[] data, IPEndPoint to, int length = -1) {
			await Soc.SendAsync(data, length == -1 ? data.Length : length, to);
		}

		/// <summary>
		/// Start or stop listening for new packets.
		/// </summary>
		/// <param name="state">True = Start, False = stop</param>
		public void Listen(bool state = true) {
			if (isListening == state)
				return;
			if (isListening)
				Dispose();
			isListening = state;
			if (isListening) {
				Soc = new UdpClient(ListenEndPoint);
				ReceiveAsync();
			}
		}

		private async void ReceiveAsync() {
			try {
				while (isListening)
					OnReceive(await Soc.ReceiveAsync());
			} catch (ObjectDisposedException) { } 
			catch {
				Dispose();
			}
		}

		/// <summary>
		/// Overrideable. Called when a packet is received.
		/// </summary>
		/// <param name="response">The packet</param>
		protected virtual void OnReceive(UdpReceiveResult response) {
			if (Received != null)
				Received(this, new UdpPacketReceivedEventArgs(response));
		}

		/// <summary>
		/// Dispose of the client, stopping it if it's listening.
		/// </summary>
		public void Dispose() {
			if (IsDisposed)
				return;
			Soc.Close();
			isListening = false;
			IsDisposed = true;
		}
	}

	/// <summary>
	/// This object is used to represent the message received from an UDP client.
	/// </summary>
	public class UdpPacketReceivedEventArgs : EventArgs {
		/// <summary>
		/// The UDP response.
		/// </summary>
		public UdpReceiveResult Response { get; private set; }
		/// <summary>
		/// Create an UDP response.
		/// </summary>
		/// <param name="resp">The UDP response</param>
		public UdpPacketReceivedEventArgs(UdpReceiveResult resp) {
			Response = resp;
		}
	}
}
