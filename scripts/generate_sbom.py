import json
import uuid
import datetime
import os

def generate_cyclonedx_sbom():
    sbom = {
        "$schema": "http://cyclonedx.org/schema/bom-1.4.json",
        "bomFormat": "CycloneDX",
        "specVersion": "1.4",
        "serialNumber": f"urn:uuid:{uuid.uuid4()}",
        "version": 1,
        "metadata": {
            "timestamp": datetime.datetime.utcnow().isoformat() + "Z",
            "tools": [
                {
                    "vendor": "Limbus Audio Systems",
                    "name": "Limbus SBOM Generator",
                    "version": "1.0.0"
                }
            ],
            "component": {
                "type": "application",
                "name": "Limbus Split Pro",
                "version": "1.0.0",
                "description": "Native Windows Application for AI Audio Stem Separation",
                "licenses": [
                    {
                        "license": {
                            "id": "Proprietary"
                        }
                    }
                ]
            }
        },
        "components": [
            {
                "type": "framework",
                "name": ".NET Runtime & WPF",
                "version": "8.0.5",
                "purl": "pkg:generic/dotnet-runtime@8.0.5",
                "licenses": [{"license": {"id": "MIT"}}]
            },
            {
                "type": "library",
                "name": "NAudio",
                "version": "2.2.1",
                "purl": "pkg:nuget/NAudio@2.2.1",
                "licenses": [{"license": {"id": "MIT"}}]
            },
            {
                "type": "library",
                "name": "CommunityToolkit.Mvvm",
                "version": "8.3.2",
                "purl": "pkg:nuget/CommunityToolkit.Mvvm@8.3.2",
                "licenses": [{"license": {"id": "MIT"}}]
            },
            {
                "type": "model",
                "name": "OpenUnmix (UMX-HQ)",
                "version": "1.0.0",
                "description": "4-Stem Music Source Separation Model",
                "licenses": [{"license": {"id": "BSD-3-Clause"}}]
            },
            {
                "type": "model",
                "name": "BS-Roformer ONNX",
                "version": "1.2.0",
                "description": "High Quality Vocal Specialist Model",
                "licenses": [{"license": {"id": "MIT"}}]
            }
        ]
    }

    out_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "dist"))
    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, "SBOM.cdx.json")

    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(sbom, f, indent=2)

    print(f"CycloneDX SBOM successfully generated at: {out_path}")

if __name__ == "__main__":
    generate_cyclonedx_sbom()
