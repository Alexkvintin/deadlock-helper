# Third-party runtime components

- Tesseract .NET wrapper 5.2.0: Charles Weld and contributors. Apache-2.0. https://github.com/charlesw/tesseract . Package: https://api.nuget.org/v3-flatcontainer/tesseract/5.2.0/tesseract.5.2.0.nupkg . See LICENSE-Tesseract.txt. Wrapper includes InteropDotNet by Andrey Akinshin (MIT), attribution in upstream license.
- Tesseract OCR native library: Tesseract contributors. Apache-2.0. https://github.com/tesseract-ocr/tesseract . Included x64 binary supplied by the above NuGet package.
- Leptonica 1.82.0: Dan Bloomberg and contributors. BSD-2-Clause. https://github.com/DanBloomberg/leptonica . License included in LICENSE-Leptonica.txt.
- eng.traineddata, rus.traineddata, ukr.traineddata: tessdata_fast, Apache-2.0. https://github.com/tesseract-ocr/tessdata_fast . Used for local match ID, clock and spectator-label recognition. No images or OCR text are transmitted.

Native binaries require Microsoft Visual C++ 2015–2022 x64 runtime. It is present on the development PC; if missing, install from Microsoft's official supported redistributable page: https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist . No installer modifies the system automatically.

Counter-item recommendations: https://deadlockitembuilder.com/counter-item-helper . Public JSON is refreshed at runtime and cached locally. Item images: https://assets.deadlock-api.com . No affiliation implied.
