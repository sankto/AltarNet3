using System;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AltarNet;

namespace AltarNet3Testing {
	public sealed class ProgressMonitor : ProgressMonitorBase {
		public override void Progressed() {}

		public override void OnInit() {
			Progress.BytesPerSecondUpdated += Progress_BytesPerSecondUpdated;
		}

		void Progress_BytesPerSecondUpdated(object sender, EventArgs e) {
			Console.WriteLine((Progress.BytesPerSecond / 1024) + " KB per second, " + Progress.Percent() + "%");
		}
	}

class MonProgramme {
	private static TcpServerHandler Serveur;
	private static TcpClientHandler MonClient;

	static void Maine(string[] args) {
		// IPAddress.Any = on accepte lex connexions venant de partout
		// 12345 = le port dont les clients doivent se connecter. doit etre > 1024 et < 64000, environs.
		Serveur = new TcpServerHandler(IPAddress.Any, 12345);
		// On peux maintenant souscrire à diverse evenements
		Serveur.Connected += Serveur_Connected;
		Serveur.Disconnected += Serveur_Disconnected;
		Serveur.ReceivedFull += Serveur_ReceivedFull;
		Serveur.Start();
		// IPAddress.Loopback veux simplement dire que tu te connecte en local.
		MonClient = new TcpClientHandler(IPAddress.Loopback, 12345);
		MonClient.Disconnected += MonClient_Disconnected;
		MonClient.ReceivedFull += MonClient_ReceivedFull;
		if (MonClient.Connect()) {
			// La connexion a réussi
			Console.WriteLine("Le client a réussi à se connecter.");
			// Juste pour tester, on envoie au serveur un message
			byte[] message = Encoding.UTF8.GetBytes("Hello");
			MonClient.Send(message);
		}
		Console.ReadKey();
	}

	static void MonClient_ReceivedFull(object sender, TcpReceivedEventArgs e) {
		string message = Encoding.UTF8.GetString(e.Data);
		Console.WriteLine("Le client a reçu un message : " + message);
	}

	static void MonClient_Disconnected(object sender, TcpEventArgs e) {
		Console.WriteLine("Le client a été notifié de sa déconnexion.");
	}

	static void Serveur_Connected(object sender, TcpEventArgs e) {
		Console.WriteLine("Un client s'est connecté!");
	}

	static void Serveur_Disconnected(object sender, TcpEventArgs e) {
		Console.WriteLine("Un client s'est déconnecté!");
	}

	static void Serveur_ReceivedFull(object sender, TcpReceivedEventArgs e) {
		string message = Encoding.UTF8.GetString(e.Data);
		Console.WriteLine("Le serveur a reçu un message : " + message);
		// Juste pour tester, on envoie au client un message
		Serveur.Send(e.Client, Encoding.UTF8.GetBytes("World"));
	}

}

	class Program {
		static void Main(string[] args) {
			try {
				//TestHttp();
				TestTcp().Wait();
				//Test().Wait();
				Console.WriteLine("Done!");
			} catch (AggregateException aggrE) {
				Console.WriteLine(aggrE.InnerException.ToString());
			} catch (WebException e) {
				Console.WriteLine((e.Response as FtpWebResponse).StatusDescription);
			}
			Console.ReadKey();
		}

		static void TestHttp() {
			var server = new HttpServerHandler("http://localhost:8080/");
			server.Requested += server_Requested;
			server.AuthScheme = AuthenticationSchemes.Basic;
			server.Listen();
			Console.WriteLine("Listening.");
			Console.ReadKey();
			server.Listen(false);
		}

		static void server_Requested(object sender, HttpContextEventArgs e) {
			var identity = (HttpListenerBasicIdentity)e.Context.User.Identity;
			Console.WriteLine(identity.Name + "::" + identity.Password);
			var data = Encoding.UTF8.GetBytes("<html><body>Hello World</body></html>");
			e.Context.Response.ContentLength64 = data.Length;
			e.Context.Response.OutputStream.Write(data, 0, data.Length);
			Console.WriteLine("Received");
			
		}

		static async Task TestTcp() {
			var server = new TcpServerHandler(IPAddress.Loopback, 5555);
			server.SSLServerCertificate = SslHelper.GetOrCreateSelfSignedCertificate("altarapp.com");
			server.IsLengthInOneFrame = true;
			server.Connected += server_Connected;
			server.Disconnected += server_Disconnected;
			server.ReceiveError += server_ReceiveError;
			server.SslError += server_SslError;
			server.MaxClientsReached += server_MaxClientsReached;
			server.ReceivedFull += server_ReceivedFull;
			var client = new TcpClientHandler(IPAddress.Loopback, 5555);
			client.ReceivedFull += client_ReceivedFull;
			client.SslError += client_SslError;
			client.SSLTargetHost = "altarapp.com";
			server.Start();
			await client.ConnectAsync();
			client.Send(Encoding.UTF8.GetBytes("HELLOWORLD"));
			Console.ReadKey();
			server.DisconnectAll();
		}

		static void server_ReceivedFull(object sender, TcpReceivedEventArgs e) {
			var message = Encoding.UTF8.GetString(e.Data);
			Console.WriteLine("server::received [" + message + "]");
			foreach (var client in (sender as TcpServerHandler).Clients) {
				// ici, client.Key est un TcpClient, et client.Value est un TcpClientInfo
				(sender as TcpServerHandler).Send(client.Value, Encoding.UTF8.GetBytes("Hello World!"));
			}
		}

		static void client_SslError(object sender, TcpErrorEventArgs e) {
			Console.WriteLine("client.SSLERROR::" + e.Error.ToString());
		}

		static void server_SslError(object sender, TcpErrorEventArgs e) {
			Console.WriteLine("server.SSLERROR::" + e.Error.ToString());
		}

		static void client_ReceivedFull(object sender, TcpReceivedEventArgs e) {
			var message = Encoding.UTF8.GetString(e.Data);
			Console.WriteLine("client::received [" + message + "]");
		}

		static void server_MaxClientsReached(object sender, TcpEventArgs e) {
			var server = sender as TcpServerHandler;
			Console.WriteLine("server::Max client reached.");
			server.Send(e.Client, Encoding.UTF8.GetBytes("client::Too many clients on this server [" + server.Clients.Count + "/" + server.MaxClients + "]"));
		}

		static void server_ReceiveError(object sender, TcpErrorEventArgs e) {
			Console.WriteLine("server.RECVERROR::" + e.Error.ToString());
		}

		static void server_Disconnected(object sender, TcpEventArgs e) {
			Console.WriteLine("server::disconnected");
		}

		static void server_Connected(object sender, TcpEventArgs e) {
			var address = ((IPEndPoint)e.Client.Client.Client.RemoteEndPoint).Address;
			Console.WriteLine("server::connected : " + address);
			(sender as TcpServerHandler).Send(e.Client, Encoding.UTF8.GetBytes("YOLO"));
		}

		static async Task Test() {
			var ftp = new FtpHandler("ftp://ftp.altarapp.com", "altarapp", "142857443556cpanel", new FtpOptions() {
				ConnectionsLimit = 8,
				GroupName = "AltarNetTesting",
				KeepAlive = true
			});
			ftp.OnMakeRequest = (request) => {
				request.EnableSsl = true;
			};
			using (var source = new CancellationTokenSource()) {
				Console.WriteLine("Testing Started.");
				if (await ftp.Select(@"public_html").Exists("testing", source.Token) == false)
					await ftp.Select(@"public_html\testing").MakeDirectory(source.Token);
				await Task.WhenAll(
					ftp.Select(@"public_html\testing\testingUpload0.txt").Upload("c:/recipes.txt", source.Token, new ProgressMonitor()),
					ftp.Select(@"public_html\testing\testingUpload1.txt").Upload("c:/recipes.txt", source.Token, new ProgressMonitor()),
					ftp.Select(@"public_html\testing\testingUpload2.txt").Upload("c:/recipes.txt", source.Token, new ProgressMonitor())	
				);
			}
		}
	}
}
