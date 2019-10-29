using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace CertificatesChecker
{
    class Program
    {
        static void Main(string[] args)
        {
            Run().Wait();
        }

        private static async Task Run()
        {
            var certificate = await GetCertificateAsync("https://www.google.co.jp/");
        }

        private static async Task<X509Certificate2> GetCertificateAsync(string uri)
        {
            var tcs = new TaskCompletionSource<X509Certificate2>();

            bool ServerCertificateCustomValidationCallback(HttpRequestMessage message, X509Certificate2 certificate, X509Chain chain, SslPolicyErrors errors)
            {
                tcs.TrySetResult(new X509Certificate2(certificate.RawData));
                return true;
            }

            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = ServerCertificateCustomValidationCallback };
            using (var hc = new HttpClient(handler))
            {
                await hc.GetAsync(uri);
            }

            return await tcs.Task;
        }
    }
}
