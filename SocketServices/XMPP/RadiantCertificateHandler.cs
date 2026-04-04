using System.Security.Cryptography.X509Certificates;
using RadiantConnect.SocketServices.XMPP.XMPPManagement;

#pragma warning disable IDE0046
#pragma warning disable CA2000

namespace RadiantConnect.SocketServices.XMPP
{
	internal static class RadiantCertificateHandler
	{
		private static string CertificateFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RadiantConnect");
		private static string CertificateLocation => Path.Combine(CertificateFolder, "server.pfx");

		internal static async Task<X509Certificate2> GetCertificate()
		{
			if (!Directory.Exists(CertificateFolder)) await DownloadCertificate().ConfigureAwait(false);
			if (!Path.Exists(CertificateLocation)) await DownloadCertificate().ConfigureAwait(false);

			X509Certificate2 certificate = new (CertificateLocation);

			if (
				!certificate.HasPrivateKey ||
				certificate.NotAfter < DateTime.UtcNow ||
				certificate.NotBefore > DateTime.UtcNow ||
				!IsTrustedCa(certificate) ||
				!IsRevoked(certificate) ||
				!IsCertificateValidForSubdomain(certificate, InternalProxy.LocalHostUrl)
			) await DownloadCertificate().ConfigureAwait(false);

			if (!await ValidateRedirect().ConfigureAwait(false))
			{
				throw new InvalidOperationException($"Unable to proxy '{InternalProxy.LocalHostUrl}' please add the follow records to your hosts file:\n\n`127.0.0.1 {InternalProxy.LocalHostUrl}`");
			}

			return certificate;
		}

		private static async Task<bool> ValidateRedirect()
		{
			IPAddress[] hostNames = await Dns.GetHostAddressesAsync(InternalProxy.LocalHostUrl).ConfigureAwait(false);
			return hostNames.All(x => x.ToString() == "127.0.0.1");
		}


		private static bool IsCertificateValidForSubdomain(X509Certificate2 certificate, string subdomain)
		{
			DateTime now = DateTime.UtcNow;
			if (now < certificate.NotBefore || now > certificate.NotAfter)
				return false;

			X509SubjectAlternativeNameExtension? sanExtension = certificate
				.Extensions
				.OfType<X509SubjectAlternativeNameExtension>()
				.FirstOrDefault();

			if (sanExtension is not null)
			{
				IEnumerable<string> dnsNames = sanExtension.EnumerateDnsNames();
				return dnsNames.Any(dnsName => IsMatch(dnsName, subdomain));
			}

			string subject = certificate.Subject;
			string? commonName = subject
				.Split(',')
				.Select(part => part.Trim())
				.FirstOrDefault(part => part.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
				?[3..];

			return commonName is not null && IsMatch(commonName, subdomain);
		}

		private static bool IsMatch(string certName, string subdomain)
		{
			if (string.Equals(certName, subdomain, StringComparison.OrdinalIgnoreCase))
				return true;

			if (!certName.StartsWith("*.", StringComparison.OrdinalIgnoreCase)) return false;
			string certSuffix = certName[2..];
			int dotIndex = subdomain.IndexOf('.', StringComparison.InvariantCultureIgnoreCase);
			if (dotIndex < 0) return false;
			string subdomainSuffix = subdomain[(dotIndex + 1)..];
			return string.Equals(certSuffix, subdomainSuffix, StringComparison.OrdinalIgnoreCase);

		}

		private static bool IsTrustedCa(X509Certificate2 certificate)
		{
			X509Chain chain = new();

			chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
			chain.ChainPolicy.RevocationFlag = X509RevocationFlag.ExcludeRoot;
			chain.ChainPolicy.VerificationFlags = X509VerificationFlags.NoFlag;
			chain.ChainPolicy.DisableCertificateDownloads = false;

			bool isValid = chain.Build(certificate);

			return isValid && chain.ChainStatus.Length == 0;
		}

		private static bool IsRevoked(X509Certificate2 certificate)
		{
			X509Chain chain = new();

			chain.ChainPolicy.RevocationMode = X509RevocationMode.Online;
			chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;

			chain.Build(certificate);

			return chain.ChainStatus.All(status => status.Status != X509ChainStatusFlags.Revoked);
		}

		private static async Task DownloadCertificate()
		{
			try
			{
				Directory.CreateDirectory(CertificateFolder);

				using HttpRequestMessage requestMessage = new(HttpMethod.Get, "https://assets.radiantconnect.ca/dev/server.pfx");
				requestMessage.Headers.Clear();
				requestMessage.Headers.TryAddWithoutValidation("User-Agent", "RadiantConnect");
				requestMessage.Headers.TryAddWithoutValidation("RadiantConnect", "true");

				using HttpResponseMessage response = await InternalHttp.InternalClient.SendAsync(requestMessage).ConfigureAwait(false);
				response.EnsureSuccessStatusCode();

				byte[] responseBytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
				await File.WriteAllBytesAsync(CertificateLocation, responseBytes).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				throw new RadiantConnectXMPPException($"failed to download required certificate; {ex}");
			}
		}
	}
}
