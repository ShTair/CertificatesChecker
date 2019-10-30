using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;

namespace CertificatesChecker
{
    class Target
    {
        public string Uri { get; set; }

        public string Thumbprint { get; set; }

        [JsonIgnore]
        public X509Certificate2 Certificate { get; set; }
    }
}
