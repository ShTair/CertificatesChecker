using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;

namespace CertificatesChecker;

class Target
{
    public string Domain { get; set; } = default!;

    public string? Thumbprint { get; set; }

    public DateTime? NotAfter { get; set; }

    public DateTime? UpdatedDate { get; set; }

    [JsonIgnore]
    public X509Certificate2? Certificate { get; set; }
}
