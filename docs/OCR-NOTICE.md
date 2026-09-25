# Optional offline OCR

ShotCab.Ocr runs locally in a separate process and exits after each request.
No screen content is sent over the network. Cancellation terminates only that
request's worker. The main application uses .NET Framework 4.8; this optional
self-contained worker carries its own .NET 8 runtime and needs no Python install.

Engine: RapidOcrNet 4.2.0 (Apache-2.0), https://github.com/BobLd/RapidOcrNet
Models: RapidAI/RapidOCR PP-OCRv6 small + PP-OCRv5 classifier.
Sources and checksums: https://github.com/RapidAI/RapidOCR/blob/main/python/rapidocr/default_models.yaml
Model provenance: https://www.modelscope.cn/models/RapidAI/RapidOCR
PaddleOCR: https://github.com/PaddlePaddle/PaddleOCR (Apache-2.0).
Runtime dependencies retain their own licenses (ONNX Runtime, SkiaSharp, Clipper2).

Run scripts/build-ocr.ps1 to build and obtain models on F:. In ShotCab settings,
select the resulting ShotCab.Ocr.exe. The entire component directory is required,
not just the exe. This component is excluded from the base portable download.
