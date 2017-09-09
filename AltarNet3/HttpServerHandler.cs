using System;
using System.Net;
using System.Threading.Tasks;

namespace AltarNet {
	/// <summary>
	/// Represent a simple HTTP server.
	/// </summary>
	public class HttpServerHandler {
		private HttpListener Listener;
		private ThreadSafeHelper<int> StateSync;
		private volatile Task ContextTask;

		/// <summary>
		/// Get or set the authentication Schemes.
		/// </summary>
		public AuthenticationSchemes AuthScheme {
			get { return Listener.AuthenticationSchemes; }
			set { Listener.AuthenticationSchemes = value; }
		}

		/// <summary>
		/// Called when an error occured while listening.
		/// </summary>
		public event EventHandler<HttpErrorEventArgs> ListenError;
		/// <summary>
		/// Called when a new request was made to this server.
		/// </summary>
		public event EventHandler<HttpContextEventArgs> Requested;

		/// <summary>
		/// Create an HTTP server, given the prefixes (ie: "http://localhost:8080") and weither the server start listening immediately.
		/// </summary>
		/// <param name="prefix">The prefixes (ie: "http://localhost:8080")</param>
		/// <param name="andStart">Should the server start now</param>
		public HttpServerHandler(string prefix, bool andStart = false) : this(new string[] { prefix }, andStart) { }
		/// <summary>
		/// Create an HTTP server, given the prefix (ie: "http://localhost:8080") and weither the server start listening immediately.
		/// </summary>
		/// <param name="prefixes">The prefix (ie: "http://localhost:8080")</param>
		/// <param name="andStart">Should the server start now</param>
		public HttpServerHandler(string[] prefixes, bool andStart = false) {
			Listener = new HttpListener();
			StateSync = new ThreadSafeHelper<int>();
			if (prefixes != null)
				foreach (var pref in prefixes)
					Listener.Prefixes.Add(pref);
			Listen(andStart);
		}

		/// <summary>
		/// Start listening. Equivalent to Listen(true). Does nothing if already started.
		/// </summary>
		public void Start() { Listen(true); }
		/// <summary>
		/// Stop listening. Equivalent to Listen(false). Does nothing if already stopped.
		/// </summary>
		public void Stop() { Listen(false); }

		/// <summary>
		/// Start or stop listening.
		/// </summary>
		/// <param name="state">if true : Start, otherwise, stop</param>
		public async void Listen(bool state = true) {
			if (state) {
				try {
					await StateSync.WaitAsync(1);
					if (Listener.IsListening)
						return;
					Listener.Start();
				} finally {
					StateSync.Release(1);
				}
				try {
					while (true) {
						var context = await Listener.GetContextAsync();
						ContextTask = Task.Run(() => OnRequest(context));
					}
				} catch (Exception e) {
					OnListenError(e);
				}
			} else if (Listener.IsListening)
				Listener.Stop();
		}

		/// <summary>
		/// Overrideable. Called when an error occur while listening.
		/// </summary>
		/// <param name="e">The error</param>
		protected virtual void OnListenError(Exception e) {
			if (ListenError != null)
				ListenError(this, new HttpErrorEventArgs(e));
		}

		/// <summary>
		/// Overrideable. Called when a request is made to the server.
		/// </summary>
		/// <param name="context">The context of the request</param>
		protected virtual void OnRequest(HttpListenerContext context) {
			if (Requested != null)
				Requested(this, new HttpContextEventArgs(context));
		}
	}
}
