# ASCOS PrintHub

Windows'ta bir klasördeki PDF dosyalarını seçilen yazıcıya toplu olarak gönderen küçük masaüstü uygulamasıdır.

## Sürümler

- **`TopluPdfYazdirici/`** — Güncel sürüm (C# / .NET 8 WinForms). Yazdırma motoru olarak uygulamaya gömülü SumatraPDF kullanır. Müşterilerde dağıtılan sürüm budur.
- **`toplu_pdf_yazdirici.py`** — Eski prototip (Python/tkinter). Windows'un `printto` komutuna ve varsayılan PDF okuyucuya bağımlıdır; yalnızca referans içindir.

## Derleme (C# sürümü)

```powershell
dotnet build -c Release TopluPdfYazdirici/TopluPdfYazdirici.csproj
```

## EXE oluşturma (Python sürümü)

```powershell
python -m pip install pyinstaller
python -m PyInstaller --noconfirm --clean --onefile --windowed --name "Toplu PDF Yazdirici" toplu_pdf_yazdirici.py
```

## Web dağıtımı

`web/` klasörü siteye yüklenmeye hazırdır:

- `web/index.html` — İndirme sayfası (exe, arşiv ve kaynak paketi indirme bağlantıları)
- `web/gplv3.html` — Türkçe + özgün GPLv3 lisans metni (sayfayı `LicenseUrl` ile aynı adrese koyun)
- `web/download/` — İndirilecek dosyalar (`ASCOS PrintHub.exe`, `ASCOS-PrintHub.zip`, `ASCOS-PrintHub-src.zip`)

## Lisans

ASCOS PrintHub, **GNU Genel Kamu Lisansı (GPL) v3** kapsamında ücretsiz dağıtılır. Türkçe ve özgün lisans metni web sayfasında yayınlanır: `web/gplv3.html` dosyasını sitenize yükleyin ve `TopluPdfYazdirici/Program.cs` içindeki `LicenseUrl` sabitini (varsayılan: `https://rotaniz.com/gplv3`) o adresle eşleştirin. Uygulamanın ana ekranındaki "GPLv3 Lisansı" bağlantısı ve Hakkında penceresi bu adrese gider.

**GARANTİ YOKTUR.** Ayrıntılar için lisans metnine bakın.

### GPL uyumluluğu

- Uygulama GPLv3 lisanslıdır; değiştirip dağıtan herkes, kendi değişikliklerini de aynı lisans altında sunmak zorundadır.
- Yazdırma motoru olarak kullanılan **SumatraPDF**, GPLv3 lisanslı özgür bir yazılımdır (telif hakkı SumatraPDF geliştiricilerine aittir). Kaynak kodu: <https://github.com/sumatrapdfreader/sumatrapdf>
- Kaynak kod, dağıtımın yanında `ASCOS-PrintHub-src.zip` olarak sunulur (GPLv3 bölüm 6 uyarınca); bu paket `TopluPdfYazdirici/LICENSE.txt` tam metnini içerir.
- Dağıtım paketini yayınlarken `ASCOS PrintHub.exe` ile birlikte `ASCOS-PrintHub-src.zip` dosyasını da aynı yerde paylaşmayı unutmayın.

## Not

Uygulama gömülü SumatraPDF ile yazdırır; müşteri makinesine ek yazılım kurulumu gerektirmez. Parola korumalı PDF'ler sessiz modda yazdırılamaz.