using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
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

            var sw = new Stopwatch();
            sw.Start();
            var logLock = new object();
            var datas = await Task.WhenAll(settings.Select(async uri =>
            {
                X509Certificate2 certificate = null;
                try
                {
                    certificate = await GetCertificateAsync(uri, new CancellationTokenSource(5000).Token);
                    lock (logLock)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Uri    : ({sw.Elapsed.TotalSeconds:0.00}秒){uri}");
                        Console.WriteLine($"Subject: {certificate.Subject}");
                        Console.WriteLine($"Issuer : {certificate.Issuer}");
                        Console.WriteLine($"Thumb  : {certificate.Thumbprint}");
                        Console.WriteLine($"Valid  : [{certificate.NotBefore:yyyy/MM/dd HH:mm:ss}] -> [{certificate.NotAfter:yyyy/MM/dd HH:mm:ss}]");
                    }
                }
                catch (Exception exp)
                {
                    lock (logLock)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Uri    : ({sw.Elapsed.TotalSeconds:0.00}秒){uri}");
                        Console.WriteLine(exp);
                    }
                }

                return (uri, certificate);
            }));

            Console.WriteLine();

            var now = DateTime.Now;
            var limit = now.AddDays(-30);
            foreach (var (uri, certificate) in datas.OrderBy(t => t.certificate?.NotAfter ?? DateTime.MinValue))
            {
                var message = "期限切れ";
                if (certificate == null)
                {
                    message = "取得エラー";
                }
                else
                {
                    var span = certificate.NotAfter - now;
                    certificate.Dispose();

                    if (span > TimeSpan.Zero)
                    {
                        var days = span.TotalDays;
                        message = $"残り {days.ToString("0").PadLeft(3)} 日";
                    }
                }

                Console.WriteLine($"{message} {uri}");
            }
        }

        private static async Task<X509Certificate2> GetCertificateAsync(string uri, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<X509Certificate2>();
            token.Register(() => tcs.TrySetException(new OperationCanceledException(token)));

            if (!token.IsCancellationRequested)
            {
                _ = Task.Run(async () =>
                {
                    var handler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (_1, c, _2, _3) =>
                        {
                            tcs.TrySetResult(new X509Certificate2(c.RawData));
                            return false;
                        },
                    };

                    try
                    {
                        using (var hc = new HttpClient(handler))
                        {
                            await hc.GetAsync(uri);
                        }
                    }
                    catch { }
                });
            }

            return await tcs.Task;
        }
    }
}
