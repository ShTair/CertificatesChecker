using System;
using System.Net.Http;
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
            using (var certificate = await GetCertificateAsync("https://www.google.co.jp/"))
            {
                Console.WriteLine(certificate.Subject);
            }
        }

        private static async Task<X509Certificate2> GetCertificateAsync(string uri)
        {
            var tcs = new TaskCompletionSource<X509Certificate2>();

            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_1, c, _2, _3) =>
                {
                    tcs.TrySetResult(new X509Certificate2(c.RawData));
                    return true;
                },
            };

            using (var hc = new HttpClient(handler))
            {
                await hc.GetAsync(uri);
            }

            return await tcs.Task;
        }
    }
}
