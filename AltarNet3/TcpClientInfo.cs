using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Timers;

namespace AltarNet {
	/// <summary>
	/// Represent and manage the informations of a client, such as receiving, parsing and sending data.
	/// </summary>
	public class TcpClientInfo {
		/// <summary>
		/// The default reading buffer size. Default is 4*1024.
		/// </summary>
		public const int BUFFERSIZE = 16 * 1024;
		/// <summary>
		/// The default reading buffer size for file transfer. Default is 8*1024.
		/// </summary>
		public const int FILEBUFFERSIZE = 32 * 1024;

		private System.Timers.Timer TimeoutTimer = null;
		private int timeout = 0;
		private ThreadSafeHelper<int> WriteSync = null;
		private HandlerType HndlType = HandlerType.Server;
		private Stream FinalStream = null;

		/// <summary>
		/// Get the parent handler.
		/// </summary>
		public ITcpHandler Parent { get; private set; }
		/// <summary>
		/// Get the socket.
		/// </summary>
		public TcpClient Client { get; private set; }
		/// <summary>
		/// Get the buffer size for reading messages. Default : BUFFERSIZE
		/// </summary>
		public int BufferSize { get; private set; }
		/// <summary>
		/// If set to true, the next time a packet begin, it will read the length of it as 8 bytes (long) instead of 4 (int).
		/// </summary>
		public bool ReadNextAsLong { get; set; }
		/// <summary>
		/// If set to true, the next time a packet begin, it will not call the ReceivedFull event until the next packet.
		/// </summary>
		public bool ReadNextNotBuffered { get; set; }
		/// <summary>
		/// Get or set a convenient object which can contains anything you want.
		/// </summary>
		public object Tag { get; set; }
		/// <summary>
		/// Get the value determining weither it will use SSL or not. SSLServerCertificate must not be null when set to true.
		/// </summary>
		public bool EnableSsl { get; internal set; }
		/// <summary>
		/// Get the certificate to use for SSL connections.
		/// </summary>
		public X509Certificate SSLServerCertificate { get; internal set; }
		/// <summary>
		/// Get the target host used with an SSL connection. Disabled if null.
		/// </summary>
		public string SSLTargetHost { get; internal set; }
		/// <summary>
		/// Get or set weither the length of a packet is combined into one with the data.
		/// </summary>
		public bool IsLengthInOneFrame { get; internal set; }
		/// <summary>
		/// Get or set the amount in milliseconds the connection will stay alive without receiving anything. Setting it at 0 or less disable it. This supersede a TcpServerHandler's ReadTimeout value.
		/// </summary>
		public int Timeout {
			get { return timeout; }
			set {
				if (timeout == value)
					return;
				timeout = value;
				if (TimeoutTimer != null) {
					if (timeout <= 0) {
						TimeoutTimer.Dispose();
						TimeoutTimer = null;
					} else {
						TimeoutTimer.Stop();
						TimeoutTimer.Interval = value;
						if (Client.Connected)
							TimeoutTimer.Start();
					}
				} else if (timeout > 0) {
					TimeoutTimer = new System.Timers.Timer(Timeout) { AutoReset = false };
					TimeoutTimer.Elapsed += TimeoutTimer_Elapsed;
					if (Client.Connected)
						TimeoutTimer.Start();
				}
			}
		}
		
		internal TcpClientInfo(ITcpHandler parent, TcpClient client, HandlerType type, int timeout, int buffersize) {
			Parent = parent;
			Client = client;
			HndlType = type;
			Timeout = timeout;
			BufferSize = buffersize;
			WriteSync = new ThreadSafeHelper<int>();
		}

		private void TimeoutTimer_Elapsed(object sender, ElapsedEventArgs e) {
			Disconnect();
		}

		private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) {
			var validate = Parent.ReportSslValidate(sender, certificate, chain, sslPolicyErrors);
			if (validate.HasValue)
				return validate.Value;
			// If the certificate is a valid, signed certificate, return true.
			if (sslPolicyErrors == SslPolicyErrors.None)
				return true;
			// If there are errors in the certificate chain, look at each error to determine the cause.
			if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0) {
				if (chain != null && chain.ChainStatus != null)
					foreach (var status in chain.ChainStatus)
						if ((certificate.Subject == certificate.Issuer) && (status.Status == System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.UntrustedRoot))
							continue; // Self-signed certificates with an untrusted root are valid. 
						else if (status.Status != System.Security.Cryptography.X509Certificates.X509ChainStatusFlags.NoError)
							return false; // If there are any other errors in the certificate chain, the certificate is invalid, so the method returns false.
				// When processing reaches this line, the only errors in the certificate chain are 
				// untrusted root errors for self-signed certificates. These certificates are valid
				// for default Exchange server installations, so return true.
				return true;
			}
			// In all other cases, deny.
			return false;
		}

		internal void StartReceive() {
			Exception innerEx = null;
			if (EnableSsl) {
				try {
					if (HndlType == HandlerType.Server) {
						FinalStream = new SslStream(Client.GetStream(), false);
						(FinalStream as SslStream).AuthenticateAsServer(SSLServerCertificate);
					} else {
						FinalStream = new SslStream(Client.GetStream(), false, ValidateServerCertificate, null);
						(FinalStream as SslStream).AuthenticateAsClient(SSLTargetHost);
					}
				} catch (Exception e) {
					if (FinalStream != null)
						FinalStream.Dispose();
					Parent.ReportSslError(this, e);
					Parent.ReportDisconnection(this, ref innerEx);
					if (TimeoutTimer != null) {
						TimeoutTimer.Dispose();
						TimeoutTimer = null;
					}
					if (innerEx != null)
						throw innerEx;
					return;
				}
			} else FinalStream = Client.GetStream();
			Task.Run(async () => {
				try {
					using (FinalStream) {
						var buffer = new byte[BufferSize + 8];
						var packet = new TcpFragment(buffer);
						if (TimeoutTimer != null)
							TimeoutTimer.Stop();
						while (true) {
							if (TimeoutTimer != null)
								TimeoutTimer.Start();
							var readCount = await FinalStream.ReadAsync(buffer, 8, BufferSize);
							if (TimeoutTimer != null)
								TimeoutTimer.Stop();
							if (readCount == 0)
								break;
							HandlePacket(packet, readCount, ref innerEx);
						}
					}
				} catch (Exception e) {
					if (innerEx == null)
						if (e is IOException == false && e is ObjectDisposedException == false)
							Parent.ReportReceiveError(this, e);
				} finally {
					Parent.ReportDisconnection(this, ref innerEx);
					if (TimeoutTimer != null) {
						TimeoutTimer.Dispose();
						TimeoutTimer = null;
					}
				}
			}).ContinueWith((task) => {
				if (innerEx != null)
					throw innerEx;
			});
		}

		/// <summary>
		/// Send an array of bytes, given an offset, length and if the 4 bytes length is sent before the real message.
		/// </summary>
		/// <param name="data">The data to send</param>
		/// <param name="offset">The offset of the data</param>
		/// <param name="count">The length to read from the data (if null, will take full length)</param>
		/// <param name="withLengthPrefixed">if the 4 bytes length is sent before the real message</param>
		public void Send(byte[] data, int offset = 0, int? count = null, bool withLengthPrefixed = true) {
			var size = count ?? data.Length;
			var lengthRaw = withLengthPrefixed ? /*BitConverter.GetBytes(size) : null;*/BitConverter.GetBytes(IPAddress.HostToNetworkOrder(size)) : null;
			InternalSend(data, offset, size, lengthRaw);
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
			var size = count ?? data.Length;
			var lengthRaw = withLengthPrefixed ? BitConverter.GetBytes(IPAddress.HostToNetworkOrder(size)) : null;
			await InternalSendAsync(data, offset, size, lengthRaw);
		}

		/// <summary>
		/// Send a whole file, given a prebuffer and a postbuffer, and if the 8 bytes length is sent before the real message.
		/// </summary>
		/// <param name="filepath"></param>
		/// <param name="preBuffer"></param>
		/// <param name="postBuffer"></param>
		/// <param name="withLengthPrefixed"></param>
		/// <param name="preBufferIsBeforeLength">Weither the prebuffer is placed before the length prefix (if applicable)</param>
		/// <remarks>If withLengthPrefixed is true, it's important for the receiving end to know that he is receiving a longer a 8 bytes length prefix. For the receiving end and within this class, you can set ReadNextAsLong to true to do so.</remarks>
		public void SendFile(string filepath, byte[] preBuffer = null, byte[] postBuffer = null, bool withLengthPrefixed = true, bool preBufferIsBeforeLength = false) {
			try {
				WriteSync.Wait(1);
				if (preBuffer != null && preBufferIsBeforeLength)
					FinalStream.Write(preBuffer, 0, preBuffer.Length);
				if (withLengthPrefixed)
					FinalStream.Write(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(new FileInfo(filepath).Length)), 0, 8);
				if (preBuffer != null && preBufferIsBeforeLength == false)
					FinalStream.Write(preBuffer, 0, preBuffer.Length);
				var buffer = new byte[FILEBUFFERSIZE];
				using (var filefs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					int read = 0;
					while (true) {
						read = filefs.Read(buffer, 0, FILEBUFFERSIZE);
						if (read == 0)
							break;
						FinalStream.Write(buffer, 0, read);
					}
				}
				if (postBuffer != null)
					FinalStream.Write(postBuffer, 0, postBuffer.Length);
			} finally {
				WriteSync.Release(1);
			}
		}

		/// <summary>
		/// Send a whole file, given a prebuffer and a postbuffer, and if the 8 bytes length is sent before the real message.
		/// </summary>
		/// <param name="filepath"></param>
		/// <param name="preBuffer"></param>
		/// <param name="postBuffer"></param>
		/// <param name="withLengthPrefixed"></param>
		/// <param name="preBufferIsBeforeLength">Weither the prebuffer is placed before the length prefix (if applicable)</param>
		/// <remarks>If withLengthPrefixed is true, it's important for the receiving end to know that he is receiving a longer a 8 bytes length prefix. For the receiving end and within this class, you can set ReadNextAsLong to true to do so.</remarks>
		/// <returns>A Task</returns>
		public async Task SendFileAsync(string filepath, byte[] preBuffer = null, byte[] postBuffer = null, bool withLengthPrefixed = true, bool preBufferIsBeforeLength = false) {
			try {
				await WriteSync.WaitAsync(1);
				if (preBuffer != null && preBufferIsBeforeLength)
					await FinalStream.WriteAsync(preBuffer, 0, preBuffer.Length);
				if (withLengthPrefixed)
					await FinalStream.WriteAsync(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(new FileInfo(filepath).Length)), 0, 8);
				if (preBuffer != null && preBufferIsBeforeLength == false)
					await FinalStream.WriteAsync(preBuffer, 0, preBuffer.Length);
				var buffer = new byte[FILEBUFFERSIZE];
				using (var filefs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
					int read = 0;
					while (true) {
						read = await filefs.ReadAsync(buffer, 0, FILEBUFFERSIZE);
						if (read == 0)
							break;
						await FinalStream.WriteAsync(buffer, 0, read);
					}
				}
				if (postBuffer != null)
					await FinalStream.WriteAsync(postBuffer, 0, postBuffer.Length);
			} finally {
				WriteSync.Release(1);
			}
		}

		internal async Task InternalSendAsync(byte[] data, int offset, int size, byte[] lengthRaw = null) {
			try {
				await WriteSync.WaitAsync(1);
				if (lengthRaw != null) {
					if (IsLengthInOneFrame) {
						var prevData = data;
						data = new byte[size + lengthRaw.Length];
						Buffer.BlockCopy(prevData, offset, data, lengthRaw.Length, size);
						Buffer.BlockCopy(lengthRaw, 0, data, 0, lengthRaw.Length);
						await FinalStream.WriteAsync(data, 0, data.Length);
					} else {
						await FinalStream.WriteAsync(lengthRaw, 0, lengthRaw.Length);
						await FinalStream.WriteAsync(data, offset, size);
					}
				} else await FinalStream.WriteAsync(data, offset, size);
			} finally {
				WriteSync.Release(1);
			}
		}

		internal void InternalSend(byte[] data, int offset, int size, byte[] lengthRaw = null) {
			try {
				WriteSync.Wait(1);
				if (lengthRaw != null) {
					if (IsLengthInOneFrame) {
						var prevData = data;
						data = new byte[size + lengthRaw.Length];
						Buffer.BlockCopy(prevData, offset, data, lengthRaw.Length, size);
						Buffer.BlockCopy(lengthRaw, 0, data, 0, lengthRaw.Length);
						FinalStream.Write(data, 0, data.Length);
					} else {
						FinalStream.Write(lengthRaw, 0, lengthRaw.Length);
						FinalStream.Write(data, offset, size);
					}
				} else FinalStream.Write(data, offset, size);
			} finally {
				WriteSync.Release(1);
			}
		}

		/// <summary>
		/// Disconnect the client. Do nothing if already disconnected.
		/// </summary>
		public void Disconnect() {
			try {
				Client.Client.Shutdown(SocketShutdown.Send);
			} catch { }
		}

		private void HandlePacket(TcpFragment packet, int readCount, ref Exception innerEx) {
			int offset = 8;
			do {
				if (packet.LengthFound == false) {
					var lengthSize = ReadNextAsLong ? 8 : 4;
					packet.CurrentReadCount += readCount;
					var countForLength = readCount - (packet.CurrentReadCount - lengthSize);
					if (packet.CurrentReadCount >= lengthSize) {
						packet.LengthRaw.Write(packet.Data, offset, countForLength);
						packet.FullLength = lengthSize == 4 ?
							IPAddress.NetworkToHostOrder(BitConverter.ToInt32(packet.Data, 0)) :
							IPAddress.NetworkToHostOrder(BitConverter.ToInt64(packet.Data, 0));
						offset += countForLength;
						readCount -= countForLength;
						packet.LengthFound = true;
						ReadNextAsLong = false;
					} else {
						packet.LengthRaw.Write(packet.Data, offset, readCount);
						readCount -= countForLength;
					}
				} else {
					var countForCurrent = readCount;
					if (packet.CumulativeReadCount + readCount >= packet.FullLength)
						countForCurrent -= (int)((packet.CumulativeReadCount + readCount) - packet.FullLength);
					packet.CurrentReadCount = countForCurrent;
					packet.CumulativeReadCount += countForCurrent;
					packet.CurrentOffset = offset;
					readCount -= countForCurrent;
					if (packet.CumulativeReadCount >= packet.FullLength) {
						packet.Completed = true;
						offset += countForCurrent;
						packet.CumulativeReadCount = packet.FullLength;
						Parent.ReportPacketFragment(this, packet, ref innerEx);
						packet.Recycle();
					} else Parent.ReportPacketFragment(this, packet, ref innerEx);
				}
			} while (readCount > 0);
		}

		#region backup handler

		/*private void HandlePacket(TcpFragment packet, int offset, int readCount) {
			if (packet.LengthFound == false) {
				if (packet.FullLength == -1) {
					packet.ReadLengthAsLong = ReadNextAsLong;
					packet.IgnoreFullEvent = ReadNextNotBuffered;
					packet.FullLength = 0;
				}
				var lengthSize = packet.ReadLengthAsLong ? 8 : 4;
				packet.CurrentReadCount += readCount;
				var countForLength = readCount - (packet.CurrentReadCount - lengthSize);
				if (packet.CurrentReadCount >= lengthSize) {
					packet.LengthRaw.Write(packet.Data, offset, countForLength);
					packet.FullLength = lengthSize == 4 ?
						IPAddress.NetworkToHostOrder(BitConverter.ToInt32(packet.Data, 0)) :
						IPAddress.NetworkToHostOrder(BitConverter.ToInt64(packet.Data, 0));
					offset += countForLength;
					readCount -= countForLength;
					packet.LengthFound = true;
					ReadNextAsLong = false;
					ReadNextNotBuffered = false;
					if (readCount > 0) {
						HandlePacket(packet, offset, readCount);
						return;
					}
				} else packet.LengthRaw.Write(packet.Data, offset, readCount);
			} else {
				var countForCurrent = readCount;
				if (packet.CumulativeReadCount + readCount >= packet.FullLength)
					countForCurrent -= (int)((packet.CumulativeReadCount + readCount) - packet.FullLength);
				packet.CurrentReadCount = countForCurrent;
				packet.CumulativeReadCount += countForCurrent;
				packet.CurrentOffset = offset;
				if (packet.CumulativeReadCount >= packet.FullLength) {
					packet.Completed = true;
					offset += countForCurrent;
					readCount -= countForCurrent;
					packet.CumulativeReadCount = packet.FullLength;
					Parent.ReportPacketFragment(this, packet);
					if (readCount > 0) {
						packet.Recycle();
						HandlePacket(packet, offset, readCount);
						return;
					}
				} else Parent.ReportPacketFragment(this, packet);
			}
			if (packet.Completed) {
				packet.Recycle();
				HandlePacket(packet, offset, readCount);
			}
		}*/

		#endregion
	}
}
