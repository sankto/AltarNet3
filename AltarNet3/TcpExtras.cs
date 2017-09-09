using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace AltarNet {
	internal enum HandlerType {
		Server, Client
	}
	/// <summary>
	/// Represent a portion of one TCP message.
	/// </summary>
	public class TcpFragment {
		internal MemoryStream LengthRaw { get; set; }
		/// <summary>
		/// Gets the full length.
		/// </summary>
		public long FullLength { get; internal set; }
		/// <summary>
		/// Get the cumulative read count for each pass. Also represent the current progress toward FullLength.
		/// </summary>
		public long CumulativeReadCount { get; internal set; }
		/*/// <summary>
		/// Get a value indicating whether length of the package is thought as longer than 4 bytes (Int32), but instead as 8 bytes (Int64). Useful for files.
		/// </summary>
		public bool ReadLengthAsLong { get; internal set; }*/
		/// <summary>
		/// Get the amount of bytes the current fragment has read. Not to be confused with CumulativeReadCount.
		/// </summary>
		public int CurrentReadCount { get; internal set; }
		/// <summary>
		/// Get the offset to start reading into Data.
		/// </summary>
		public int CurrentOffset { get; internal set; }
		/// <summary>
		/// Get a value indicating whether the Length of the message has been read.
		/// </summary>
		public bool LengthFound { get; internal set; }
		/// <summary>
		/// Get a value indicating whether the message is fully read.
		/// </summary>
		public bool Completed { get; internal set; }
		/// <summary>
		/// Get the array of bytes from which the fragment has informations. To be used with CurrentOffset and CurrentReadCount to determinate the fragment.
		/// </summary>
		public byte[] Data { get; internal set; }
		/*/// <summary>
		/// Get or set a value indicating whether the handler will also call the ReceivedFull event for this packet, if registered.
		/// </summary>
		public bool IgnoreFullEvent { get; set; }*/
		/// <summary>
		/// Get or set a convenient object which can contains anything you want.
		/// </summary>
		public object Tag { get; set; }
		internal MemoryStream MemStream { get; set; }

		internal TcpFragment(byte[] buffer) {
			Data = buffer;
			LengthRaw = new MemoryStream(Data, 0, 8, writable: true, publiclyVisible: true);
			Recycle();
		}

		internal void Recycle() {
			FullLength = -1;
			CumulativeReadCount = 0;
			CurrentReadCount = 0;
			LengthRaw.Seek(0, SeekOrigin.Begin);
			LengthFound = false;
			Completed = false;
			Tag = null;
			MemStream = null;
		}

		/// <summary>
		/// This method automatically take the Data, CurrentOffset and CurrentReadCount properties and write the fragment's data into a given stream.
		/// </summary>
		/// <param name="stream">The stream to write to</param>
		public void WriteToStream(Stream stream) {
			if (LengthFound == false || CurrentReadCount == 0)
				return;
			stream.Write(Data, CurrentOffset, CurrentReadCount);
		}

		/// <summary>
		/// Get the percentage of the message that has been received so far.
		/// </summary>
		/// <returns>The percent, between 0 and 100</returns>
		public int GetPercentDone() {
			return FullLength == 0 ? 100 : (int)(((float)CumulativeReadCount / (float)FullLength) * 100.0f);
		}
	}

	/// <summary>
	/// Represent the interface both the TCP server and TCP client must adhere to.
	/// </summary>
	public interface ITcpHandler {
		/// <summary>
		/// Called when a (or the) client is disconnected.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="innerEx">the inner exception</param>
		void ReportDisconnection(TcpClientInfo info, ref Exception innerEx);
		/// <summary>
		/// Called when a packet fragment is received.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="packet">The packet fragment received</param>
		/// <param name="innerEx">the inner exception</param>
		void ReportPacketFragment(TcpClientInfo info, TcpFragment packet, ref Exception innerEx);
		/// <summary>
		/// Called when an exception occur while receiving messages.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="e">The error</param>
		void ReportReceiveError(TcpClientInfo info, Exception e);
		/// <summary>
		/// Called when an exception occur while starting an SSL connection.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="e">The error</param>
		void ReportSslError(TcpClientInfo info, Exception e);
		/// <summary>
		/// Only called by a client.
		/// </summary>
		/// <param name="sender">The sender</param>
		/// <param name="certificate">The certificate</param>
		/// <param name="chain">The chain</param>
		/// <param name="sslPolicyErrors">The policy errors</param>
		bool? ReportSslValidate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors);
	}

	/// <summary>
	/// Represent the informations sent with the ReceivedFragment event.
	/// </summary>
	public class TcpFragmentReceivedEventArgs : EventArgs {
		/// <summary>
		/// The client who sent the packet.
		/// </summary>
		public TcpClientInfo Client { get; private set; }
		/// <summary>
		/// The packet which was received.
		/// </summary>
		public TcpFragment Packet { get; private set; }
		/// <summary>
		/// Create the TcpFragmentReceivedEventArgs.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="packet">The packet fragment received</param>
		public TcpFragmentReceivedEventArgs(TcpClientInfo info, TcpFragment packet) {
			Client = info;
			Packet = packet;
		}
	}

	/// <summary>
	/// Represent the informations sent with the ReceivedFull event.
	/// </summary>
	public class TcpReceivedEventArgs : EventArgs {
		/// <summary>
		/// The client who sent the packet.
		/// </summary>
		public TcpClientInfo Client { get; private set; }
		/// <summary>
		/// The full array of bytes of the packet.
		/// </summary>
		public byte[] Data { get; private set; }
		/// <summary>
		/// Create the TcpReceivedEventArgs.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="data">The full data</param>
		public TcpReceivedEventArgs(TcpClientInfo info, byte[] data) {
			Client = info;
			Data = data;
		}
	}

	/// <summary>
	/// Represent some generic informations related to a TCP event.
	/// </summary>
	public class TcpEventArgs : EventArgs {
		/// <summary>
		/// The client involved.
		/// </summary>
		public TcpClientInfo Client { get; private set; }
		/// <summary>
		/// Create the TcpEventArgs.
		/// </summary>
		/// <param name="info">The client</param>
		public TcpEventArgs(TcpClientInfo info) {
			Client = info;
		}
	}

	/// <summary>
	/// Represent the informations related to the validation of a client's SSL connection.
	/// </summary>
	public class TcpSslValidateEventArgs : EventArgs {
		/// <summary>
		/// Get or set weither the SSL connection is valid. Set to null to let the default validator do his own works.
		/// </summary>
		public bool? Accepted { get; set; }
		/// <summary>
		/// The client involved.
		/// </summary>
		public TcpClientInfo Client { get; private set; }
		/// <summary>
		/// Create the TcpEventArgs.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="accepted">The acceptance state</param>
		public TcpSslValidateEventArgs(TcpClientInfo info, bool? accepted = null) {
			Client = info;
			Accepted = accepted;
		}
	}

	/// <summary>
	/// Represent some informations related to an error while reading messages.
	/// </summary>
	public class TcpErrorEventArgs : EventArgs {
		/// <summary>
		/// The client involved.
		/// </summary>
		public TcpClientInfo Client { get; private set; }
		/// <summary>
		/// The error which occured.
		/// </summary>
		public Exception Error { get; private set; }
		/// <summary>
		/// Create the TcpErrorEventArgs.
		/// </summary>
		/// <param name="info">The client</param>
		/// <param name="e">The error</param>
		public TcpErrorEventArgs(TcpClientInfo info, Exception e) {
			Client = info;
			Error = e;
		}
	}
}
