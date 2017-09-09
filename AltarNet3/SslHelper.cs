using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using CERTENROLLLib;

namespace AltarNet {
	/// <summary>
	/// This class helps with the creation, obtention and removal of self-signed SSL Certificates.
	/// </summary>
	public static class SslHelper {
		/// <summary>
		/// This will iterate through the store's certificates and return their subject name.
		/// </summary>
		/// <returns>An iteration of all certificates' subject name</returns>
		public static IEnumerable<string> GetCertificatesName() {
			var store = new X509Store(StoreName.My);
			store.Open(OpenFlags.ReadWrite);
			try {
				foreach (var c in store.Certificates)
					yield return c.Subject;
			} finally {
				store.Close();
			}
		}
		/// <summary>
		/// This will remove a named SSL Certificate if it exist. Do nothing otherwise.
		/// </summary>
		/// <param name="subjectName"></param>
		public static void RemoveCertificate(string subjectName) {
			var store = new X509Store(StoreName.My);
			store.Open(OpenFlags.ReadWrite);
			// pass through the certs until the subject name match the desired one, or not.
			try {
				foreach (var c in store.Certificates)
					if (c.Subject == "CN=" + subjectName)
						store.Remove(c);
			} finally {
				store.Close();
			}
		}

		/// <summary>
		/// This method of convenience allow the user to get a previously created SSL Self-Signed Certificate. It will create it if it doesn't exist. Example of SubjectName : CN=altarapp.com
		/// </summary>
		/// <param name="subjectName">The certificate's subject name</param>
		/// <returns>The ssl certificate</returns>
		/// <param name="expiration">The expiration date for the certificate (Default to now + 25 years)</param>
		public static X509Certificate2 GetOrCreateSelfSignedCertificate(string subjectName, DateTime? expiration = null) {
			// open the certs store
			var store = new X509Store(StoreName.My);
			store.Open(OpenFlags.ReadWrite);
			try {
				// pass through the certs until the subject name match the desired one, or not.
				foreach (var c in store.Certificates)
					if (c.Subject == "CN=" + subjectName)
						return c;
				// Otherwise, start creating the self-signed certificate.

				// create DN for subject and issuer
				var dn = new CX500DistinguishedName();
				dn.Encode("CN=" + subjectName, X500NameFlags.XCN_CERT_NAME_STR_NONE);

				// create a new private key for the certificate
				CX509PrivateKey privateKey = new CX509PrivateKey();
				privateKey.ProviderName = "Microsoft Base Cryptographic Provider v1.0";
				privateKey.MachineContext = true;
				privateKey.Length = 2048;
				privateKey.KeySpec = X509KeySpec.XCN_AT_SIGNATURE; // use is not limited
				privateKey.ExportPolicy = X509PrivateKeyExportFlags.XCN_NCRYPT_ALLOW_PLAINTEXT_EXPORT_FLAG;
				privateKey.Create();

				// Use the stronger SHA512 hashing algorithm
				var hashobj = new CObjectId();
				hashobj.InitializeFromAlgorithmName(ObjectIdGroupId.XCN_CRYPT_HASH_ALG_OID_GROUP_ID,
					ObjectIdPublicKeyFlags.XCN_CRYPT_OID_INFO_PUBKEY_ANY,
					AlgorithmFlags.AlgorithmFlagsNone, "SHA512");

				// add extended key usage if you want - look at MSDN for a list of possible OIDs
				var oid = new CObjectId();
				oid.InitializeFromValue("1.3.6.1.5.5.7.3.1"); // SSL server
				var oidlist = new CObjectIds();
				oidlist.Add(oid);
				var eku = new CX509ExtensionEnhancedKeyUsage();
				eku.InitializeEncode(oidlist);

				// Create the self signing request
				var cert = new CX509CertificateRequestCertificate();
				cert.InitializeFromPrivateKey(X509CertificateEnrollmentContext.ContextMachine, privateKey, "");
				cert.Subject = dn;
				cert.Issuer = dn; // the issuer and the subject are the same
								  //cert.NotBefore = DateTime.Now - TimeSpan.FromDays(1);
								  // this cert expires immediately. Change to whatever makes sense for you
				cert.NotBefore = DateTime.Now;
				cert.NotAfter = expiration.HasValue ? expiration.Value : DateTime.Now.AddYears(25);
				cert.X509Extensions.Add((CX509Extension)eku); // add the EKU
				cert.HashAlgorithm = hashobj; // Specify the hashing algorithm
				cert.Encode(); // encode the certificate

				// Do the final enrollment process
				var enroll = new CX509Enrollment();
				enroll.InitializeFromRequest(cert); // load the certificate
				enroll.CertificateFriendlyName = subjectName; // Optional: add a friendly name
				string csr = enroll.CreateRequest(); // Output the request in base64
				// and install it back as the response
				enroll.InstallResponse(InstallResponseRestrictionFlags.AllowUntrustedCertificate,
					csr, EncodingType.XCN_CRYPT_STRING_BASE64, ""); // no password
				// output a base64 encoded PKCS#12 so we can import it back to the .Net security classes
				var base64encoded = enroll.CreatePFX("", // no password, this is for internal consumption
					PFXExportOptions.PFXExportChainWithRoot);

				// instantiate the target class with the PKCS#12 data (and the empty password)
				var finalCert = new System.Security.Cryptography.X509Certificates.X509Certificate2(
					System.Convert.FromBase64String(base64encoded), "",
					// mark the private key as exportable (this is usually what you want to do)
					System.Security.Cryptography.X509Certificates.X509KeyStorageFlags.Exportable
				);

				store.Add(finalCert);
				return finalCert;
			} finally {
				store.Close();
			}
		}
	}
}
