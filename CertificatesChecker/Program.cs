using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CertificatesChecker
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
            var settingsFile = configuration.GetValue<string>("SettingsPath");

            var settingsJson = await File.ReadAllTextAsync(settingsFile);
            var targets = JsonSerializer.Deserialize<List<Target>>(settingsJson);
            if (targets == null) return;

            var sw = new Stopwatch();
            sw.Start();
            var logLock = new object();
            await Task.WhenAll(targets.Select(async target =>
            {
                try
                {
                    target.Certificate = await CertificateManager.GetAsync($"https://{target.Domain}/", 5000);
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
                var isChanged = target.Thumbprint != target.Certificate?.Thumbprint;
                target.Thumbprint = target.Certificate?.Thumbprint;
                target.NotAfter = target.Certificate?.NotAfter;
                target.Certificate?.Dispose();

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
                        message = $"残り{span.Value.TotalDays,3:0}日";
                    }

                    if (isChanged)
                    {
                        message = message + " 更新";
                    }
                }

                Console.WriteLine($"{message} {target.Domain}");
            }

            var oprions = new JsonSerializerOptions { WriteIndented = true };
            settingsJson = JsonSerializer.Serialize(targets, oprions);
            await File.WriteAllTextAsync(settingsFile, settingsJson);
        }
    }
}
