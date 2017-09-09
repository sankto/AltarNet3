using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace AltarNet {
	/// <summary>
	/// Represent a TCP client, to connect to a server.
	/// </summary>
	public class TcpClientHandler : ITcpHandler {
		private TcpClient Client;
		private bool Disposed;

		/// <summary>
		/// The server the client is set to connect to.
		/// </summary>
		public IPAddress ServerAddress { get; private set; }
		/// <summary>
		/// The port the client is set to connect to.
		/// </summary>
		public int Port { get; private set; }
		/// <summary>
		/// The informations handler of the client.
		/// </summary>
		public TcpClientInfo InfoHandler { get; private set; }
		/// <summary>
		/// Get the exception generated if a connection fail.
		/// </summary>
		public Exception LastConnectError { get; private set; }
		/// <summary>
		/// Get the buffer size for reading messages. Default : TcpClientInfo.BUFFERSIZE
		/// </summary>
		public int BufferSize { get; private set; }
		/// <summary>
		/// Get or set the target host used with an SSL connection. Disabled if null.
		/// </summary>
		public string SSLTargetHost { get; set; }
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
		/// Called when a packet is completed and is desired to be whole.
		/// </summary>
		public event EventHandler<TcpReceivedEventArgs> ReceivedFull;
		/// <summary>
		/// Called when the client is disconnected.
		/// </summary>
		public event EventHandler<TcpEventArgs> Disconnected;
		/// <summary>
		/// Called when an error occur while receiving messages.
		/// </summary>
		public event EventHandler<TcpErrorEventArgs> ReceiveError;
		/// <summary>
		/// Called when an error occur while starting the SSL connection.
		/// </summary>
		public event EventHandler<TcpErrorEventArgs> SslError;
		/// <summary>
		/// Called when an SSL connection is being validated.
		/// </summary>
		public event EventHandler SslValidationRequested;

		/// <summary>
		/// Create the client.
		/// </summary>
		/// <param name="addr">The address to connect to</param>
		/// <param name="port">The port to connect to</param>
		/// <param name="buffersize">The buffersize for reading messages</param>
		public TcpClientHandler(IPAddress addr, int port, int buffersize = TcpClientInfo.BUFFERSIZE) {
			ServerAddress = addr;
			Port = port;
			BufferSize = buffersize;
			RestartSocket();
		}

		private void RestartSocket() {
			Client = new TcpClient();
			InfoHandler = new TcpClientInfo(this, Client, HandlerType.Client, 0, BufferSize);
			Disposed = false;
		}

		/// <summary>
		/// Attempt a connection.
		/// </summary>
		/// <returns>True if successful, false otherwise</returns>
		/// <remarks>If it fail, check the LastConnectError property to see the details of the failure.</remarks>
		public bool Connect() {
			try {
				if (Disposed)
					RestartSocket();
				Client.Connect(ServerAddress, Port);
				LastConnectError = null;
				InfoHandler.EnableSsl = SSLTargetHost != null;
				InfoHandler.SSLTargetHost = SSLTargetHost;
				InfoHandler.IsLengthInOneFrame = IsLengthInOneFrame;
				InfoHandler.StartReceive();
				return true;
			} catch (Exception e) {
				LastConnectError = e;
				return false;
			}
		}

		/// <summary>
		/// Attempt a connection.
		/// </summary>
		/// <returns>True if successful, false otherwise</returns>
		/// <remarks>If it fail, check the LastConnectError property to see the details of the failure.</remarks>
		public async Task<bool> ConnectAsync() {
			Task task = null;
			try {
				if (Disposed)
					RestartSocket();
				task = Client.ConnectAsync(ServerAddress, Port);
				await task;
				LastConnectError = null;
				InfoHandler.EnableSsl = SSLTargetHost != null;
				InfoHandler.SSLTargetHost = SSLTargetHost;
				InfoHandler.IsLengthInOneFrame = IsLengthInOneFrame;
				InfoHandler.StartReceive();
				return true;
			} catch {
				LastConnectError = task != null ? task.Exception : null;
				return false;
			}
		}

		/// <summary>
		/// Disconnect the client, if connected. Do nothing otherwise.
		/// </summary>
		public void Disconnect() {
			InfoHandler.Disconnect(); 
		}

		/// <summary>
		/// Send an array of bytes, given an offset, length and if the 4 bytes length is sent before the real message.
		/// </summary>
		/// <param name="data">The data to send</param>
		/// <param name="offset">The offset of the data</param>
		/// <param name="count">The length to read from the data (if null, will take full length)</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		public void Send(byte[] data, int offset = 0, int? count = null, bool withLengthPrefixed = true) {
			InfoHandler.Send(data, offset, count, withLengthPrefixed);
		}

		/// <summary>
		/// Send an array of bytes, given an offset, length and if the 4 bytes length is sent before the real message.
		/// </summary>
		/// <param name="data">The data to send</param>
		/// <param name="offset">The offset of the data</param>
		/// <param name="count">The length to read from the data (if null, will take full length)</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <returns>A Task</returns>
		public async Task SendAsync(byte[] data, int offset = 0, int? count = null, bool withLengthPrefixed = true) {
			await InfoHandler.SendAsync(data, offset, count, withLengthPrefixed);
		}

		/// <summary>
		/// Send a whole file, given a prebuffer and a postbuffer, and if the 8 bytes length is sent before the real message.
		/// </summary>
		/// <param name="filepath">The path to the file</param>
		/// <param name="preBuffer">A prefixed buffer</param>
		/// <param name="postBuffer">A suffixed buffer</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <param name="preBufferIsBeforeLength">Weither the prebuffer is placed before the length prefix (if applicable)</param>
		/// <remarks>If withLengthPrefixed is true, it's important for the receiving end to know that he is receiving a longer a 8 bytes length prefix. For the receiving end and within this class, you can set 'InfoHandler.ReadNextAsLong' to true to do so.</remarks>
		public void SendFile(string filepath, byte[] preBuffer = null, byte[] postBuffer = null, bool withLengthPrefixed = true, bool preBufferIsBeforeLength = false) {
			InfoHandler.SendFile(filepath, preBuffer, postBuffer, withLengthPrefixed, preBufferIsBeforeLength);
		}

		/// <summary>
		/// Send a whole file, given a prebuffer and a postbuffer, and if the 8 bytes length is sent before the real message.
		/// </summary>
		/// <param name="filepath">The path to the file</param>
		/// <param name="preBuffer">A prefixed buffer</param>
		/// <param name="postBuffer">A suffixed buffer</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		/// <param name="preBufferIsBeforeLength">Weither the prebuffer is placed before the length prefix (if applicable)</param>
		/// <returns>A Task</returns>
		/// <remarks>If withLengthPrefixed is true, it's important for the receiving end to know that he is receiving a longer a 8 bytes length prefix. For the receiving end and within this class, you can set 'InfoHandler.ReadNextAsLong' to true to do so.</remarks>
		public async Task SendFileAsync(string filepath, byte[] preBuffer = null, byte[] postBuffer = null, bool withLengthPrefixed = true, bool preBufferIsBeforeLength = false) {
			await InfoHandler.SendFileAsync(filepath, preBuffer, postBuffer, withLengthPrefixed, preBufferIsBeforeLength);
		}

		#region On..Events

		/// <summary>
		/// Overrideable. Called when the client is disconnected.
		/// </summary>
		protected virtual void OnDisconnect() {
			if (Disconnected != null)
				Disconnected(this, new TcpEventArgs(InfoHandler));
		}

		/// <summary>
		/// Overrideable. Called when an error occur while receiving messages.
		/// </summary>
		/// <param name="e">The error</param>
		protected virtual void OnReceiveError(Exception e) {
			if (ReceiveError != null)
				ReceiveError(this, new TcpErrorEventArgs(InfoHandler, e));
		}

		/// <summary>
		/// Overrideable. Called when an error occur while starting the SSL connection.
		/// </summary>
		/// <param name="e">The error</param>
		protected virtual void OnSslError(Exception e) {
			if (SslError != null)
				SslError(this, new TcpErrorEventArgs(InfoHandler, e));
		}

		/// <summary>
		/// Overrideable. Called when a fragment of a packet is received.
		/// </summary>
		/// <param name="packet">The packet</param>
		protected virtual void OnReceiveFragment(TcpFragment packet) {
			var ignoreFull = false;
			if (packet.Completed && InfoHandler.ReadNextNotBuffered) {
				InfoHandler.ReadNextNotBuffered = false;
				ignoreFull = true;
			}
			if (ReceivedFragment != null)
				ReceivedFragment(this, new TcpFragmentReceivedEventArgs(InfoHandler, packet));
			if (ignoreFull)
				return;
			if ((ReceivedFull != null || ObtainFullPackets) && InfoHandler.ReadNextNotBuffered == false) {
				if (packet.MemStream == null)
					packet.MemStream = new MemoryStream();
				packet.WriteToStream(packet.MemStream);
				if (packet.Completed) {
					var data = packet.MemStream.ToArray();
					OnReceiveFull(data);
					packet.MemStream.Dispose();
				}
			}
		}

		/// <summary>
		/// Overrideable. Called when a packet is completed and is desired to be whole.
		/// </summary>
		/// <param name="data">The whole packet</param>
		protected virtual void OnReceiveFull(byte[] data) {
			if (ReceivedFull != null)
				ReceivedFull(this, new TcpReceivedEventArgs(InfoHandler, data));
		}

		/// <summary>
		/// Overrideable. Called when an SSL connection is being validated.
		/// </summary>
		/// <param name="sender">The sender</param>
		/// <param name="certificate">The certificate</param>
		/// <param name="chain">The chain</param>
		/// <param name="sslPolicyErrors">The policy errors</param>
		/// <returns></returns>
		protected virtual bool? OnSslValidationRequest(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
			if (SslValidationRequested != null) {
				var e = new TcpSslValidateEventArgs(InfoHandler);
				SslValidationRequested(this, e);
				return e.Accepted;
			}
			return null;
		}

		#endregion

		#region ITcpHandler Members

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="info">The disconnected client</param>
		/// <param name="innerEx">the inner exception</param>
		public void ReportDisconnection(TcpClientInfo info, ref Exception innerEx) {
			try {
				Disposed = true;
				OnDisconnect();
			} catch (Exception e) {
				innerEx = e;
				throw e;
			}
		}

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="packet">The packet</param>
		/// <param name="innerEx">the inner exception</param>
		public void ReportPacketFragment(TcpClientInfo info, TcpFragment packet, ref Exception innerEx) {
			try {
				OnReceiveFragment(packet);
			} catch (Exception e) {
				innerEx = e;
				throw e;
			}
		}

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="e">The error</param>
		public void ReportReceiveError(TcpClientInfo info, Exception e) {
			OnReceiveError(e);
		}

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="e">The error</param>
		public void ReportSslError(TcpClientInfo info, Exception e) {
			OnSslError(e);
		}

		/// <summary>
		/// Not much useful outside internal callings.
		/// </summary>
		/// <param name="sender">The sender</param>
		/// <param name="certificate">The certificate</param>
		/// <param name="chain">The chain</param>
		/// <param name="sslPolicyErrors">The policy errors</param>
		public bool? ReportSslValidate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
			return OnSslValidationRequest(sender, certificate, chain, sslPolicyErrors);
		}

		#endregion
	}
}
