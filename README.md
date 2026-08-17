# Toplu PDF Yazdırıcı

Windows'ta bir klasördeki PDF dosyalarını seçilen yazıcıya toplu olarak gönderen küçük masaüstü uygulamasıdır.

## Çalıştırma

Python 3 kuruluysa:

```powershell
python toplu_pdf_yazdirici.py
```

## EXE oluşturma

```powershell
python -m pip install pyinstaller
python -m PyInstaller --noconfirm --clean --onefile --windowed --name "Toplu PDF Yazdirici" toplu_pdf_yazdirici.py
```

Oluşan dosya `dist\Toplu PDF Yazdirici.exe` konumundadır.

## Not

Uygulama Windows'un `printto` komutunu ve bilgisayarda `.pdf` dosyalarıyla ilişkilendirilmiş PDF okuyucuyu kullanır. Yazdırma çalışmazsa Adobe Acrobat Reader veya SumatraPDF gibi yazdırmayı destekleyen bir PDF okuyucuyu varsayılan PDF uygulaması yapın.
