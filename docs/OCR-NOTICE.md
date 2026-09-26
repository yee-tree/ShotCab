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

The optional installer component uses RapidOcrNet 4.2.0 (Apache-2.0),
ONNX Runtime 1.29.0 (MIT), SkiaSharp 3.119.1 (MIT), Clipper2 2.0.0
(Boost Software License 1.0), and a self-contained .NET 8 runtime.
Its models are the two PP-OCRv6 small ONNX files and dictionary listed in
`scripts/build-ocr.ps1`, plus the PP-OCRv5 text-line orientation classifier
included by RapidOcrNet. The installer builder checks SHA-256 for all four
model/dictionary files and bundles the corresponding license texts under
`licenses/`. Blob Emoji and developer test images are not installer content.

Run scripts/build-ocr.ps1 to build and obtain models on F:. For a portable
installation, select the resulting ShotCab.Ocr.exe in ShotCab settings; the
entire component directory is required, not just the exe. The installer
presents OCR as an optional component; selecting it uses the built-in path.
