using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using LimbusSplitPro.Core.Interfaces;
using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.App.Engine;

public class ModelManifestVerifier : IModelVerifier
{
    private readonly List<ModelManifest> _manifests = new();
    public IReadOnlyList<ModelManifest> LoadedManifests => _manifests.AsReadOnly();

    public ModelManifestVerifier(string manifestJsonPath)
    {
        if (File.Exists(manifestJsonPath))
        {
            try
            {
                string json = File.ReadAllText(manifestJsonPath);
                var items = JsonSerializer.Deserialize<List<ModelManifest>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (items != null)
                {
                    _manifests.AddRange(items);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load manifest JSON: {ex.Message}");
            }
        }
    }

    public ModelManifest? GetManifest(string modelId)
    {
        return _manifests.FirstOrDefault(m => string.Equals(m.Id, modelId, StringComparison.OrdinalIgnoreCase));
    }

    public ModelVerificationResult VerifyModel(string modelId, string modelFolderPath)
    {
        var manifest = GetManifest(modelId);
        if (manifest == null)
        {
            return new ModelVerificationResult
            {
                IsValid = false,
                ModelId = modelId,
                ErrorMessage = $"El modelo '{modelId}' no está registrado en el manifiesto oficial. (Fail-closed)"
            };
        }

        string fullPath = Path.Combine(modelFolderPath, manifest.RelativePath);
        if (!File.Exists(fullPath))
        {
            return new ModelVerificationResult
            {
                IsValid = false,
                ModelId = modelId,
                ErrorMessage = $"El archivo del modelo no existe en la ruta esperada: {fullPath}"
            };
        }

        var fileInfo = new FileInfo(fullPath);
        if (manifest.ExpectedSizeBytes > 0 && fileInfo.Length != manifest.ExpectedSizeBytes)
        {
            return new ModelVerificationResult
            {
                IsValid = false,
                ModelId = modelId,
                ErrorMessage = $"Tamaño inesperado para {manifest.Name}. Esperado: {manifest.ExpectedSizeBytes} bytes, Encontrado: {fileInfo.Length} bytes."
            };
        }

        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(fullPath);
        byte[] hashBytes = sha256.ComputeHash(stream);
        string computedHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        if (!string.IsNullOrEmpty(manifest.ExpectedSha256) &&
            !string.Equals(computedHash, manifest.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            return new ModelVerificationResult
            {
                IsValid = false,
                ModelId = modelId,
                ComputedSha256 = computedHash,
                ErrorMessage = $"Hash SHA-256 no coincide para {manifest.Name}. Posible corrupción o alteración del archivo."
            };
        }

        return new ModelVerificationResult
        {
            IsValid = true,
            ModelId = modelId,
            ComputedSha256 = computedHash
        };
    }

    public bool VerifyAllModels(string modelsRootPath, out List<ModelVerificationResult> failures)
    {
        failures = new List<ModelVerificationResult>();
        foreach (var manifest in _manifests)
        {
            var res = VerifyModel(manifest.Id, modelsRootPath);
            if (!res.IsValid)
            {
                failures.Add(res);
            }
        }
        return failures.Count == 0;
    }
}
