using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace CertificatesChecker
{
    class Program
    {
        static void Main(string[] args)
        {
            Run(args[0]).Wait();
        }

        private static async Task Run(string settingsFile)
        {
            var settingsJson = await File.ReadAllTextAsync(settingsFile);
            var settings = JsonConvert.DeserializeObject<List<string>>(settingsJson);
            if (settings == null) return;

            foreach (var setting in settings)
            {
                using (var certificate = await GetCertificateAsync(setting))
                {
                    Console.WriteLine();
                    Console.WriteLine(setting);
                    Console.WriteLine(certificate.Subject);
                    Console.WriteLine($"{certificate.NotBefore:yyyy/MM/dd HH:mm} -> {certificate.NotAfter:yyyy/MM/dd HH:mm}");
                }
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
