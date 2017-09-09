using System;
using System.Net;

namespace AltarNet {
	/// <summary>
	/// Represent some informations related to an error while receiving http contexts.
	/// </summary>
	public class HttpErrorEventArgs : EventArgs {
		/// <summary>
		/// The error which occured.
		/// </summary>
		public Exception Error { get; private set; }
		/// <summary>
		/// Create the HttpErrorEventArgs.
		/// </summary>
		/// <param name="e">The error</param>
		public HttpErrorEventArgs(Exception e) {
			Error = e;
		}
	}
	/// <summary>
	/// Represent some informations related to newly received http context.
	/// </summary>
	public class HttpContextEventArgs : EventArgs {
		/// <summary>
		/// The received context.
		/// </summary>
		public HttpListenerContext Context { get; private set; }
		/// <summary>
		/// Create the HttpContextEventArgs.
		/// </summary>
		/// <param name="con">The context</param>
		public HttpContextEventArgs(HttpListenerContext con) {
			Context = con;
		}
	}
}
