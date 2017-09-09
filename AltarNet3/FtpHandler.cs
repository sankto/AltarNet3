using System;
using System.Net;
using System.Net.Cache;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace AltarNet {
	/// <summary>
	/// Allow the user to select a target on an FTP server, be it a directory or a file, and request or query on it.
	/// </summary>
	public class FtpHandler {
		/// <summary>
		/// Get the global options that will, if not overridden, be used for each requests.
		/// </summary>
		public readonly FtpOptions Options;
		/// <summary>
		/// Allow the user to customize a request before it is used.
		/// </summary>
		public Action<FtpWebRequest> OnMakeRequest { get; set; }

		/// <summary>
		/// Create an FTP Handler, with credentials and options.
		/// </summary>
		/// <param name="host">The host. example : "ftp://myhost.com"</param>
		/// <param name="user">The username for the credentials</param>
		/// <param name="pass">The password for the credentials</param>
		/// <param name="options">The options</param>
		public FtpHandler(string host, string user = null, string pass = null, FtpOptions options = null) :
				this(host, user == null ? null : new NetworkCredential(user, pass ?? string.Empty), options) { }

		/// <summary>
		/// Create an FTP Handler, with credentials and options.
		/// </summary>
		/// <param name="host">The host. example : "ftp://myhost.com"</param>
		/// <param name="creds">The credentials</param>
		/// <param name="options">The options</param>
		public FtpHandler(string host, NetworkCredential creds = null, FtpOptions options = null) :
				this(options) {
			Options.HostName = host;
			if (creds != null)
				Options.Credentials = creds;
		}

		/// <summary>
		/// Create an FTP Handler, with options.
		/// </summary>
		/// <param name="options">The options</param>
		public FtpHandler(FtpOptions options = null) {
			Options = options ?? new FtpOptions();
		}

		/// <summary>
		/// Create and return a subitem with the given path, with a Target value which ends up being 'HostName + "/" + remotePath'.
		/// </summary>
		/// <param name="remotePath">The target for the new subitem</param>
		/// <returns>The subitem</returns>
		public FtpRemoteItem Select(string remotePath = null) {
			return new FtpRemoteItem(this, null, remotePath ?? string.Empty);
		}

		/// <summary>
		/// Query a test on the FTP server to see if the credentials are accepted.
		/// </summary>
		/// <param name="options">The query-specific options</param>
		/// <returns>True if successful, false otherwise</returns>
		public async Task<bool> TestCredentials(FtpOptions options = null) {
			return await TestCredentials(CancellationToken.None, options);
		}
		/// <summary>
		/// Query a test on the FTP server to see if the credentials are accepted.
		/// </summary>
		/// <param name="cancelToken">The token to cancel the query</param>
		/// <param name="options">The query-specific options</param>
		/// <returns>True if successful, false otherwise</returns>
		public async Task<bool> TestCredentials(CancellationToken cancelToken, FtpOptions options = null) {
			try {
				using (var response = await MakeRequest(cancelToken, options).GetResponseAsync()) { }
			} catch { return false; }
			return true;
		}

		internal FtpWebRequest MakeRequest(CancellationToken token, FtpOptions options = null, string remote = null, string retrProtocol = null) {
			options = options ?? Options;
			remote = remote ?? string.Empty;
			var request = (FtpWebRequest)FtpWebRequest.Create(options.HostName + "/" + remote);
			if (options.Credentials != null)
				request.Credentials = options.Credentials;
			request.Method = retrProtocol ?? WebRequestMethods.Ftp.ListDirectory;
			request.UsePassive = options.UsePassive;
			request.KeepAlive = options.KeepAlive;
			request.UseBinary = options.UseBinary;
			request.Proxy = options.Proxy;
			request.ConnectionGroupName = options.GroupName;
			request.ServicePoint.ConnectionLimit = options.ConnectionsLimit;
			token.Register(() => request.Abort(), useSynchronizationContext: false);
			if (OnMakeRequest != null)
				OnMakeRequest(request);
			return request;
		}
	}
}
