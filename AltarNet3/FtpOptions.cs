using System;
using System.Net;

namespace AltarNet {
	/// <summary>
	/// Represent the options used for an FTP request.
	/// </summary>
	public class FtpOptions : ICloneable {
		/// <summary>
		/// The default buffer size, used when downloading or uploading a file. Default is 4096.
		/// </summary>
		public const int DefaultBufferSize = 4 * 1024;
		/// <summary>
		/// The default connections limit.
		/// </summary>
		public const int DefaultConnLimit = 2;

		/// <summary>
		/// Get or set the buffer size, used when downloading or uploading a file.
		/// </summary>
		public int BufferSize { get; set; }
		/// <summary>
		/// Get or set if the request shall be in binary or ASCII, during downloads or uploads. Defaulted to true.
		/// </summary>
		public bool UseBinary { get; set; }
		/// <summary>
		/// Get or set if the request shall be passive or not.
		/// </summary>
		public bool UsePassive { get; set; }
		/// <summary>
		/// Get or set if the request shall close or not at the end.
		/// </summary>
		public bool KeepAlive { get; set; }
		/// <summary>
		/// Get or set the hostname, like "ftp://myhost.com".
		/// </summary>
		public string HostName { get; set; }
		/// <summary>
		/// Get or set the proxy.
		/// </summary>
		public IWebProxy Proxy { get; set; }
		/// <summary>
		/// Get or set the credentials.
		/// </summary>
		public NetworkCredential Credentials { get; set; }
		/// <summary>
		/// Get or set the Connection Group Name, best used with KeepAlive activated.
		/// </summary>
		public string GroupName { get; set; }
		/// <summary>
		/// Get or set the quantity of concurrent connections that can be made.
		/// </summary>
		public int ConnectionsLimit { get; set; }

		/// <summary>
		/// Create an FtpOptions.
		/// </summary>
		public FtpOptions() {
			BufferSize = DefaultBufferSize;
			UseBinary = true;
			UsePassive = true;
			KeepAlive = false;
			ConnectionsLimit = DefaultConnLimit;
		}

		/// <summary>
		/// Do a shallow copy of these options.
		/// </summary>
		/// <returns>The copy.</returns>
		public FtpOptions Clone() {
			return this.MemberwiseClone() as FtpOptions;
		}

		/// <summary>
		/// Do a shallow copy of these options.
		/// </summary>
		/// <returns>The copy.</returns>
		object ICloneable.Clone() {
			return Clone();
		}
	}
}
