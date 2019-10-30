using Newtonsoft.Json;
using System;
using System.Security.Cryptography.X509Certificates;

namespace CertificatesChecker
{
    class Target
    {
        public string Domain { get; set; }

        public string Thumbprint { get; set; }

        public DateTime? NotAfter { get; set; }

        [JsonIgnore]
        public X509Certificate2 Certificate { get; set; }
    }
}
