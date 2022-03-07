using CertificatesChecker;
using Microsoft.Extensions.Configuration;
using ShComp;
using ShComp.Net;
using System.Diagnostics;
using System.Text.Json;

var configuration = new ConfigurationBuilder().AddUserSecrets<Program>().Build();
var settingsFile = configuration.GetValue<string>("SettingsPath");

var settingsJson = await File.ReadAllTextAsync(settingsFile);
var targets = JsonSerializer.Deserialize<List<Target>>(settingsJson);
if (targets == null) return;

var ct = new CancellationTokenSource(10000).Token;

var sw = new Stopwatch();
sw.Start();
var logLock = new object();
await Task.WhenAll(targets.Select(target =>
{
    return Task.Run(async () =>
    {
        try
        {
            target.Certificate = (await SslUtils.GetCertificateAsync(target.Domain, ct))!;
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
                using (ConsoleColor.Red.SetForeground())
                {
                    Console.WriteLine();
                    Console.WriteLine($"Domain : ({sw.Elapsed.TotalSeconds:0.00}秒){target.Domain}");
                    Console.WriteLine(exp);
                }
            }
        }

        return target;
    });
}));

Console.WriteLine();

var now = DateTime.Now;
var limit = now.AddDays(-1);
foreach (var target in targets.OrderBy(t => t.Certificate?.NotAfter ?? DateTime.MinValue))
{
    using var restorer = new ConsoleColorRestorer();
    if (target.Certificate is { } certificate)
    {
        var span = certificate.NotAfter - now;
        if (span > TimeSpan.Zero)
        {
            if (span < TimeSpan.FromDays(60))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
            }
            Console.Write($"残り{span.TotalDays,4:0}日");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("期限切れ");
        }

        if (target.Thumbprint != certificate.Thumbprint)
        {
            target.Thumbprint = certificate.Thumbprint;
            target.NotAfter = certificate.NotAfter;
            target.UpdatedDate = DateTime.Now;
        }

        if (target.UpdatedDate > limit)
        {
            Console.CursorLeft = 11;
            using (ConsoleColor.DarkBlue.SetBackground())
            {
                Console.Write("更新");
            }
        }

        certificate.Dispose();
    }
    else
    {
        Console.Write("取得エラー");
    }

    Console.CursorLeft = 16;
    Console.WriteLine(target.Domain);
}

var oprions = new JsonSerializerOptions { WriteIndented = true };
settingsJson = JsonSerializer.Serialize(targets, oprions);
await File.WriteAllTextAsync(settingsFile, settingsJson);
