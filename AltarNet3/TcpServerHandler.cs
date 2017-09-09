using System;
using System.Linq;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Net.Security;

namespace AltarNet {
	/// <summary>
	/// Represent a TCP server, accepting clients.
	/// </summary>
	public class TcpServerHandler : ITcpHandler {
		private TcpListener Listener;
		private volatile bool isListening;
		private ThreadSafeHelper<int> StateSync;

		/// <summary>
		/// Is true if the server is listening for new connections.
		/// </summary>
		public bool IsListening { get { return isListening; } }
		/// <summary>
		/// The amount of simultaneous connection attempts the server may have.
		/// </summary>
		public int Backlog { get; private set; }
		/// <summary>
		/// The default amount of time in milliseconds before a client is disconnected if nothing is received from it.
		/// </summary>
		public int ReadTimeout { get; private set; }
		/// <summary>
		/// Get or set the maximum amount of clients the server will unconditionally accept.
		/// </summary>
		public int MaxClients { get; set; }
		/// <summary>
		/// Get the buffer size for reading messages. Default : TcpClientInfo.BUFFERSIZE
		/// </summary>
		public int BufferSize { get; private set; }
		/// <summary>
		/// A collection of currently connected clients.
		/// </summary>
		public ConcurrentDictionary<TcpClient, TcpClientInfo> Clients { get; private set; }
		/// <summary>
		/// The certificate to use for SSL connections. Setting this to non-null shall activate SSL.
		/// </summary>
		public X509Certificate SSLServerCertificate { get; set; }
		/// <summary>
		/// Get or set weither packets are buffered in memory. If the event ReceivedFull is non-null, this is ignored. Only useful in the case that you are overriding OnReceiveFull.
		/// </summary>
		public bool ObtainFullPackets { get; set; }
		/// <summary>
		/// Get or set weither the length of a packet is combined into one with the data.
		/// </summary>
		public bool IsLengthInOneFrame { get; set; }

		/// <summary>
		/// Called when a fragment of a packet is received.
		/// </summary>
		public event EventHandler<TcpFragmentReceivedEventArgs> ReceivedFragment;
		/// <summary>
		/// Called when a packet is completed and is desired to be received whole.
		/// </summary>
		public event EventHandler<TcpReceivedEventArgs> ReceivedFull;
		/// <summary>
		/// Called when a new connection is made.
		/// </summary>
		public event EventHandler<TcpEventArgs> Connected;
		/// <summary>
		/// Called when a client is disconnected.
		/// </summary>
		public event EventHandler<TcpEventArgs> Disconnected;
		/// <summary>
		/// Called when an error occur while receiving messages.
		/// </summary>
		public event EventHandler<TcpErrorEventArgs> ReceiveError;
		/// <summary>
		/// Called when an error occur while starting an SSL connection.
		/// </summary>
		public event EventHandler<TcpErrorEventArgs> SslError;
		/// <summary>
		/// Called when the maximum amount of clients is reached.
		/// </summary>
		public event EventHandler<TcpEventArgs> MaxClientsReached;

		/// <summary>
		/// Create a TCP server, with the given address, port, read timeout and backlog limit.
		/// </summary>
		/// <param name="address">The address to listen</param>
		/// <param name="port">The port to listen</param>
		/// <param name="readTimeout">The default read timeout</param>
		/// <param name="backlog">The backlog</param>
		/// <param name="maxClients">The maximum amount of clients. Default to Int.MaxValue</param>
		/// <param name="buffersize">The buffer size when reading messages</param>
		public TcpServerHandler(IPAddress address, int port, int readTimeout = 0, int backlog = 100, int maxClients = int.MaxValue, int buffersize = TcpClientInfo.BUFFERSIZE) {
			Listener = new TcpListener(new IPEndPoint(address, port));
			Clients = new ConcurrentDictionary<TcpClient, TcpClientInfo>();
			Backlog = backlog;
			ReadTimeout = readTimeout;
			isListening = false;
			MaxClients = maxClients;
			BufferSize = buffersize;
			StateSync = new ThreadSafeHelper<int>();
		}

		/// <summary>
		/// Start the server so that it accept connections, if it's not already started.
		/// </summary>
		public async void Start() {
			try {
				await StateSync.WaitAsync(1);
				if (isListening)
					return;
				isListening = true;
			} finally {
				StateSync.Release(1);
			}
			try {
				Listener.Start(Backlog);
				while (true)
					Welcome(await Listener.AcceptTcpClientAsync());
			} catch { }
			finally {
				isListening = false;
			}
		}

		private void Welcome(TcpClient client) {
			if (client == null)
				return;
			Task.Run(() => {
				try {
					var cinfo = new TcpClientInfo(this, client, HandlerType.Server, ReadTimeout, BufferSize);
					if (Clients.Count >= MaxClients) {
						OnMaxClientsReach(cinfo);
						client.Close();
					} else if (Clients.TryAdd(client, cinfo)) {	
						cinfo.EnableSsl = SSLServerCertificate != null;
						cinfo.SSLServerCertificate = SSLServerCertificate;
						cinfo.IsLengthInOneFrame = IsLengthInOneFrame;
						cinfo.StartReceive();
						OnConnect(cinfo);
					} else client.Close();
				} catch {
					client.Close();
					throw;
				}
			});
		}

		/// <summary>
		/// Stop listening for new connections.
		/// </summary>
		public void Stop() {
			Listener.Stop();
		}

		/// <summary>
		/// Send an array of bytes to the chosen client, given an offset, length and if the 4 bytes length is sent before the real message.
		/// </summary>
		/// <param name="client">The receiving client</param>
		/// <param name="data">The data to send</param>
		/// <param name="offset">The offset of the data</param>
		/// <param name="count">The length to read from the data (if null, will take full length)</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		public void Send(TcpClientInfo client, byte[] data, int offset = 0, int? count = null, bool withLengthPrefixed = true) {
			client.Send(data, offset, count, withLengthPrefixed);
		}
		/// <summary>
		/// Send an array of bytes to the chosen client, given an offset, length and if the 4 bytes length is sent before the real message.
		/// </summary>
		/// <param name="client">The receiving client</param>
		/// <param name="data">The data to send</param>
		/// <param name="offset">The offset of the data</param>
		/// <param name="count">The length to read from the data (if null, will take full length)</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <returns>A Task</returns>
		public async Task SendAsync(TcpClientInfo client, byte[] data, int offset = 0, int? count = null, bool withLengthPrefixed = true) {
			await client.SendAsync(data, offset, count, withLengthPrefixed);
		}

		/// <summary>
		/// Send a whole file to a chosen client, given a prebuffer and a postbuffer, and if the 8 bytes length is sent before the real message.
		/// </summary>
		/// <param name="client">The receiving client</param>
		/// <param name="filepath">The path to the file</param>
		/// <param name="preBuffer">A prefixed buffer</param>
		/// <param name="postBuffer">A suffixed buffer</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <param name="preBufferIsBeforeLength">Weither the prebuffer is placed before the length prefix (if applicable)</param>
		/// <remarks>If withLengthPrefixed is true, it's important for the receiving end to know that he is receiving a longer a 8 bytes length prefix. For the receiving end and within this class, you can set 'InfoHandler.ReadNextAsLong' to true to do so.</remarks>
		public void SendFile(TcpClientInfo client, string filepath, byte[] preBuffer = null, byte[] postBuffer = null, bool withLengthPrefixed = true, bool preBufferIsBeforeLength = false) {
			client.SendFile(filepath, preBuffer, postBuffer, withLengthPrefixed, preBufferIsBeforeLength);
		}

		/// <summary>
		/// Send a whole file, given a prebuffer and a postbuffer, and if the 8 bytes length is sent before the real message.
		/// </summary>
		/// <param name="client">The receiving client</param>
		/// <param name="filepath">The path to the file</param>
		/// <param name="preBuffer">A prefixed buffer</param>
		/// <param name="postBuffer">A suffixed buffer</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <param name="preBufferIsBeforeLength">Weither the prebuffer is placed before the length prefix (if applicable)</param>
		/// <returns>A Task</returns>
		/// <remarks>If withLengthPrefixed is true, it's important for the receiving end to know that he is receiving a longer a 8 bytes length prefix. For the receiving end and within this class, you can set 'InfoHandler.ReadNextAsLong' to true to do so.</remarks>
		public async Task SendFileAsync(TcpClientInfo client, string filepath, byte[] preBuffer = null, byte[] postBuffer = null, bool withLengthPrefixed = true, bool preBufferIsBeforeLength = false) {
			await client.SendFileAsync(filepath, preBuffer, postBuffer, withLengthPrefixed, preBufferIsBeforeLength);
		}

		/// <summary>
		/// Send an array of bytes to ALL clients, given an offset, length and if the 4 bytes length is sent before the real message.
		/// </summary>
		/// <param name="data">The data to send</param>
		/// <param name="offset">The offset of the data</param>
		/// <param name="count">The length to read from the data (if null, will take full length)</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		public void SendAll(byte[] data, int offset = 0, int? count = null, bool withLengthPrefixed = true) {
			var size = count ?? data.Length;
			var lengthRaw = withLengthPrefixed ? BitConverter.GetBytes(IPAddress.HostToNetworkOrder(size)) : null;
			foreach (var cl in Clients.Values)
				cl.InternalSend(data, offset, size, lengthRaw);
		}

		/// <summary>
		/// Send a whole file to all clients, given a prebuffer and a postbuffer, and if the 8 bytes length is sent before the real message.
		/// </summary>
		/// <param name="filepath">The path to the file</param>
		/// <param name="preBuffer">A prefixed buffer</param>
		/// <param name="postBuffer">A suffixed buffer</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <param name="preBufferIsBeforeLength">Weither the prebuffer is placed before the length prefix (if applicable)</param>
		/// <remarks>If withLengthPrefixed is true, it's important for the receiving end to know that he is receiving a longer a 8 bytes length prefix. For the receiving end and within this class, you can set 'InfoHandler.ReadNextAsLong' to true to do so.</remarks>
		public void SendAllFile(string filepath, byte[] preBuffer = null, byte[] postBuffer = null, bool withLengthPrefixed = true, bool preBufferIsBeforeLength = false) {
			foreach (var client in Clients.Values)
				client.SendFile(filepath, preBuffer, postBuffer, withLengthPrefixed, preBufferIsBeforeLength);
		}

		/// <summary>
		/// Send a whole file to all clients, given a prebuffer and a postbuffer, and if the 8 bytes length is sent before the real message.
		/// </summary>
		/// <param name="filepath">The path to the file</param>
		/// <param name="preBuffer">A prefixed buffer</param>
		/// <param name="postBuffer">A suffixed buffer</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <param name="preBufferIsBeforeLength">Weither the prebuffer is placed before the length prefix (if applicable)</param>
		/// <returns>A Task</returns>
		/// <remarks>If withLengthPrefixed is true, it's important for the receiving end to know that he is receiving a longer a 8 bytes length prefix. For the receiving end and within this class, you can set 'InfoHandler.ReadNextAsLong' to true to do so.</remarks>
		public async Task SendAllFileAsync(string filepath, byte[] preBuffer = null, byte[] postBuffer = null, bool withLengthPrefixed = true, bool preBufferIsBeforeLength = false) {
			await Task.WhenAll(from cl in Clients.Values select cl.SendFileAsync(filepath, preBuffer, postBuffer, withLengthPrefixed, preBufferIsBeforeLength));
		}

		/// <summary>
		/// Send an array of bytes to ALL clients, given an offset, length and if the 4 bytes length is sent before the real message.
		/// </summary>
		/// <param name="data">The data to send</param>
		/// <param name="offset">The offset of the data</param>
		/// <param name="count">The length to read from the data (if null, will take full length)</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <returns>A Task</returns>
		public async Task SendAllAsync(byte[] data, int offset = 0, int? count = null, bool withLengthPrefixed = true) {
			var size = count ?? data.Length;
			var lengthRaw = withLengthPrefixed ? BitConverter.GetBytes(IPAddress.HostToNetworkOrder(size)) : null;
			await Task.WhenAll(from cl in Clients.Values select cl.InternalSendAsync(data, offset, size, lengthRaw));
		}

		/// <summary>
		/// Disconnect the given client.
		/// </summary>
		/// <param name="client">The client</param>
		public void DisconnectClient(TcpClientInfo client) {
			client.Disconnect();
		}

		/// <summary>
		/// Disconnect all connected clients.
		/// </summary>
		public void DisconnectAll() {
			foreach (var client in Clients)
				DisconnectClient(client.Value);
		}

		#region On..Events

		/// <summary>
		/// Overrideable. Called when a new connection is made.
		/// </summary>
		/// <param name="client">The client</param>
		protected virtual void OnConnect(TcpClientInfo client) {
			if (Connected != null)
				Connected(this, new TcpEventArgs(client));
		}

		/// <summary>
		/// Overrideable. Called when a client is disconnected.
		/// </summary>
		/// <param name="client">The client</param>
		protected virtual void OnDisconnect(TcpClientInfo client) {
			if (Disconnected != null)
				Disconnected(this, new TcpEventArgs(client));
		}

		/// <summary>
		/// Overrideable. Called when an error occur while receiving messages.
		/// </summary>
		/// <param name="client">The client</param>
		/// <param name="e">The error</param>
		protected virtual void OnReceiveError(TcpClientInfo client, Exception e) {
			if (ReceiveError != null)
				ReceiveError(this, new TcpErrorEventArgs(client, e));
		}

		/// <summary>
		/// Overrideable. Called when an error occur while starting an SSL connection.
		/// </summary>
		/// <param name="client">The client</param>
		/// <param name="e">The error</param>
		protected virtual void OnSslError(TcpClientInfo client, Exception e) {
			if (SslError != null)
				SslError(this, new TcpErrorEventArgs(client, e));
		}

		/// <summary>
		/// Overrideable. Called when a fragment of a packet is received.
		/// </summary>
		/// <param name="client">The client</param>
		/// <param name="packet">The received packet</param>
		protected virtual void OnReceiveFragment(TcpClientInfo client, TcpFragment packet) {
			var ignoreFull = false;
			if (packet.Completed && client.ReadNextNotBuffered) {
				client.ReadNextNotBuffered = false;
				ignoreFull = true;
			}
			if (ReceivedFragment != null)
				ReceivedFragment(this, new TcpFragmentReceivedEventArgs(client, packet));
			if (ignoreFull)
				return;
			else if ((ReceivedFull != null || ObtainFullPackets) && client.ReadNextNotBuffered == false) {
				if (packet.MemStream == null)
					packet.MemStream = new MemoryStream();
				packet.WriteToStream(packet.MemStream);
				if (packet.Completed) {
					var data = packet.MemStream.ToArray();
					OnReceiveFull(client, data);
					packet.MemStream.Dispose();
				}
			}
		}

		/// <summary>
		/// Overrideable. Called when a packet is completed and is desired to be received whole.
		/// </summary>
		/// <param name="client">The client</param>
		/// <param name="data">The whole packet</param>
		protected virtual void OnReceiveFull(TcpClientInfo client, byte[] data) {
			if (ReceivedFull != null)
				ReceivedFull(this, new TcpReceivedEventArgs(client, data));
		}

		/// <summary>
		/// Overrideable. Called when the maximum amount of clients is reached.
		/// </summary>
		/// <param name="client">The client</param>
		protected virtual void OnMaxClientsReach(TcpClientInfo client) {
			if (MaxClientsReached != null)
				MaxClientsReached(this, new TcpEventArgs(client));
		}

		#endregion

		#region ITcpHandler Members

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="client">The disconnected client</param>
		/// <param name="innerEx">the inner exception</param>
		public void ReportDisconnection(TcpClientInfo client, ref Exception innerEx) {
			try {
				Clients.TryRemove(client.Client, out client);
				if (client != null)
					OnDisconnect(client);
			} catch (Exception e) {
				innerEx = e;
				throw e;
			}
		}

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="client">The client</param>
		/// <param name="packet">The packet</param>
		/// <param name="innerEx">the inner exception</param>
		public void ReportPacketFragment(TcpClientInfo client, TcpFragment packet, ref Exception innerEx) {
			try {
				OnReceiveFragment(client, packet);
			} catch (Exception e) {
				innerEx = e;
				throw e;
			}
		}

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="client">The client</param>
		/// <param name="e">The error</param>
		public void ReportReceiveError(TcpClientInfo client, Exception e) {
			OnReceiveError(client, e);
		}

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="client">The client</param>
		/// <param name="e">The error</param>
		public void ReportSslError(TcpClientInfo client, Exception e) {
			OnSslError(client, e);
		}

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="sender">The sender</param>
		/// <param name="certificate">The certificate</param>
		/// <param name="chain">The chain</param>
		/// <param name="sslPolicyErrors">The policy errors</param>
		public bool? ReportSslValidate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
			return null;
		}

		#endregion
	}
}
