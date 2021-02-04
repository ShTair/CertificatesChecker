using System;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace CertificatesChecker
{
    public static class CertificateManager
    {
        public static Task<X509Certificate2> GetAsync(string uri)
        {
            return GetAsync(uri, CancellationToken.None);
        }

        public static Task<X509Certificate2> GetAsync(string uri, int timeout)
        {
            return GetAsync(uri, new CancellationTokenSource(timeout).Token);
        }

        public static async Task<X509Certificate2> GetAsync(string uri, CancellationToken token)
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
                            if (c == null) tcs.TrySetException(new NullReferenceException());
                            else tcs.TrySetResult(new X509Certificate2(c.RawData));
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
                    catch (Exception exp)
                    {
                        tcs.TrySetException(exp);
                    }
                });
            }

            return await tcs.Task;
        }
    }
}
