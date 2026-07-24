using LimbusSplitPro.Core.Models;

namespace LimbusSplitPro.Core.Interfaces;

public interface IModelVerifier
{
    IReadOnlyList<ModelManifest> LoadedManifests { get; }
    ModelVerificationResult VerifyModel(string modelId, string modelFolderPath);
    bool VerifyAllModels(string modelsRootPath, out List<ModelVerificationResult> failures);
    ModelManifest? GetManifest(string modelId);
}
