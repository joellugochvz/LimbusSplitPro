namespace LimbusSplitPro.Core.Models;

public class ModelManifest
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string ExpectedSha256 { get; set; } = string.Empty;
    public long ExpectedSizeBytes { get; set; }
    public string Origin { get; set; } = string.Empty;
    public string CodeLicense { get; set; } = string.Empty;
    public string WeightsLicense { get; set; } = string.Empty;
    public string LicenseEvidence { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool IsCommercialAllowed { get; set; }
    public bool IsRedistributable { get; set; }
    public List<string> Capabilities { get; set; } = new();
}

public class ModelVerificationResult
{
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ComputedSha256 { get; set; } = string.Empty;
}
