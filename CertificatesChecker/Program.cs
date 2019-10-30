﻿using Newtonsoft.Json;
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
            var targets = JsonConvert.DeserializeObject<List<Target>>(settingsJson);
            if (targets == null) return;

            var sw = new Stopwatch();
            sw.Start();
            var logLock = new object();
            await Task.WhenAll(targets.Select(async target =>
            {
                try
                {
                    target.Certificate = await GetCertificateAsync($"https://{target.Domain}/", new CancellationTokenSource(5000).Token);
                    lock (logLock)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Domain : ({sw.Elapsed.TotalSeconds:0.00}秒) {target.Domain}");
                        Console.WriteLine($"Subject: {target.Certificate.Subject}");
                        Console.WriteLine($"Issuer : {target.Certificate.Issuer}");
                        Console.WriteLine($"Thumb  : {target.Certificate.Thumbprint}");
                        Console.WriteLine($"Valid  : [{target.Certificate.NotBefore:yyyy/MM/dd HH:mm:ss}] -> [{target.Certificate.NotAfter:yyyy/MM/dd HH:mm:ss}]");
                    }
                }
                catch (Exception exp)
                {
                    lock (logLock)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"Domain : ({sw.Elapsed.TotalSeconds:0.00}秒){target.Domain}");
                        Console.WriteLine(exp);
                    }
                }

                return target;
            }));

            Console.WriteLine();

            var now = DateTime.Now;
            var limit = now.AddDays(-30);
            foreach (var target in targets.OrderBy(t => t.Certificate?.NotAfter ?? DateTime.MinValue))
            {
                var isChanged = target.Thumbprint != target.Certificate.Thumbprint;
                target.Thumbprint = target.Certificate?.Thumbprint;
                target.NotAfter = target.Certificate?.NotAfter;
                target.Certificate.Dispose();

                var message = "期限切れ";
                if (target.Certificate == null)
                {
                    message = "取得エラー";
                }
                else
                {
                    var span = target.NotAfter - now;
                    if (span > TimeSpan.Zero)
                    {
                        message = $"残り{span.Value.TotalDays.ToString("0").PadLeft(3)}日";
                    }

                    if (isChanged)
                    {
                        message = message + " 更新";
                    }

                }

                Console.WriteLine($"{message} {target.Domain}");
            }

            settingsJson = JsonConvert.SerializeObject(targets, Formatting.Indented);
            await File.WriteAllTextAsync(settingsFile, settingsJson);
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
