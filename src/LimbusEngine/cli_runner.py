import sys
import os
import argparse
import json
from separator import LimbusSeparatorEngine

def stream_json_event(data: dict):
    print(json.dumps(data), flush=True)

def main():
    parser = argparse.ArgumentParser(description="Limbus Split Pro ML Engine CLI Runner")
    parser.add_argument("--input", required=True, help="Input audio file path")
    parser.add_argument("--output-dir", required=True, help="Output folder path")
    parser.add_argument("--stems", required=True, help="Comma-separated stem IDs to extract")
    parser.add_argument("--device", default="Auto", help="Target processing device (Auto, CPU, CUDA, DirectML)")
    parser.add_argument("--models-dir", default="", help="Path to models directory")

    args = parser.parse_args()

    input_file = os.path.abspath(args.input)
    output_dir = os.path.abspath(args.output_dir)
    stems = [s.strip() for s in args.stems.split(",") if s.strip()]
    device = args.device

    if not os.path.isfile(input_file):
        stream_json_event({
            "type": "error",
            "code": "ERR_FILE_NOT_FOUND",
            "message": f"El archivo de entrada no existe: {input_file}"
        })
        sys.exit(1)

    os.makedirs(output_dir, exist_ok=True)

    stream_json_event({
        "type": "started",
        "input": input_file,
        "output_dir": output_dir,
        "stems": stems,
        "device": device
    })

    try:
        engine = LimbusSeparatorEngine(models_dir=args.models_dir, progress_callback=stream_json_event)
        results = engine.process(input_file=input_file, output_dir=output_dir, requested_stems=stems, device=device)

        stream_json_event({
            "type": "completed",
            "generated_files": results
        })
        sys.exit(0)
    except Exception as ex:
        stream_json_event({
            "type": "error",
            "code": "ERR_ENGINE_FAILURE",
            "message": str(ex)
        })
        sys.exit(2)

if __name__ == "__main__":
    main()
