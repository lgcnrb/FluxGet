# FluxGet

**Gelismis indirme yoneticisi. Hizli, guvenilir, akillica.**

WinUI 3 ve .NET 8 ile yazilmis, IDM benzeri gucllu bir indirme yoneticisi. Paralel chunk indirme, YouTube destegi, tarayici eklentisi ve oncelikli kuyruk sistemi.

---

## Ekran Goruntusu

> yakinda eklenecek

---

## Ozellikler

### Indirme Motoru
- **Paralel Chunk Indirme** - Tek dosyayi birden fazla paralel baglantiyla indirme
- **Duraklat / Devam / Iptal** - Indirmeleri istediginiz zaman durdurun ve devam ettirin
- **Otomatik Yeniden Deneme** - Hatalarda otomatik yeniden deneme (exponential backoff)
- **Resume Destegi** - Yarim kalan indirmeleri kaldigi yerden devam ettirme
- **Hash Dogrulama** - SHA256/SHA1/MD5 ile dosya butunlugu dogrulama

### Hiz ve Kontrol
- **Kuresel Hiz Limiti** - Tum indirmeler icin genel hiz siniri
- **Indirme Bazli Hiz Limiti** - Her indirme icin ayri hiz siniri
- **Oncelikli Kuyruk** - Indirmeleri oncelige gore siralayin (0-10)
- **Es Zamanli Indirme Sayisi** - Ayn anda kac indirme yapilacagini ayarlayin (1-20)

### YouTube Destegi
- **Video Indirme** - 360p'den 2160p'ye kadar cozunurluk secenekleri
- **MP3 Donusturme** - YouTube videolarindan ses dosyasi olusturma
- **yt-dlp Entegrasyonu** - Gucllu YouTube indirme motoru
- **ffmpeg Destegi** - Ses/goruntu donusturme araci

### Tarayici Eklentisi
- **Chrome Manifest v3** - Modern tarayici eklentisi standartlari
- **Tek Tikla Indirme** - Tarayicidan dogrudan indirme ekleme
- **YouTube Algilama** - YouTube sayfalarinda cozunurluk secici modal
- **Indirme Gecmisi** - Tarayicida indirme gecmisini saklama
- **Sag Tik Menusu** - Link, video, gorsel ve sayfa uzerinde sag tik destegi

### Arayuz
- **Karanlik Tema** - Goze yakin karanlik arayuz
- **Modern WinUI 3 Tasarimi** - Fluent Design System
- **Surukle-Birak** - URL'leri surukleyip birakarak indirme
- **Pano URL Algilama** - Panodaki URL'leri otomatik algilama
- **Bildirimler** - Indirme tamamlaninca bildirim gosterme

---

## Mimari

```
FluxGet/
├── Core/
│   ├── Data/           # EF Core DbContext
│   ├── Helpers/        # Yardimci siniflar
│   ├── Models/         # Veri modelleri
│   ├── Security/       # Giris dogrulama, token, sanitizer
│   └── Services/       # Is mantigi servisleri
├── UI/
│   ├── Converters/     # XAML donusturuculeri
│   ├── ViewModels/     # MVVM ViewModel'ler
│   └── Views/          # WinUI 3 sayfalari
├── BrowserExtension/   # Chrome tarayici eklentisi
└── Assets/             # Uygulama resimleri
```

---

## Teknolojiler

| Teknoloji | Surum | Amac |
|-----------|-------|------|
| C# | 12 | Programlama dili |
| .NET | 8.0 | Calistirma platformu |
| Windows App SDK | 2.2 | WinUI 3 framework |
| WinUI | 3 | Kullanici arayuzu |
| Entity Framework Core | 8.0 | Veritabani ORM (SQLite) |
| CommunityToolkit.Mvvm | 8.2 | MVVM altyapisi |
| System.Reactive | 6.0 | Reaktif programlama |
| yt-dlp | - | YouTube video indirme |
| ffmpeg | - | Ses/goruntu donusturme |

---

## Gereksinimler

- **Isletim Sistemi**: Windows 10 (surum 1809 / build 17763) veya uzeri
- **Platform**: x64, x86 veya ARM64
- **.NET**: .NET 8.0 Runtime
- **Disk**: Minimum 100 MB bos alan
- **Internet**: Indirme ve YouTube icin gerekli

---

## Kurulum

### Yontem 1: Kaynak Kodundan

```bash
# Depoyu klonlayin
git clone https://github.com/lgcnrb/FluxGet.git
cd FluxGet

# Derleyin
dotnet build -p:Platform=x64

# Calistirin
dotnet run --project FluxGet -p:Platform=x64
```

### Yontem 2: Yayinlanan Surum

1. [Releases](https://github.com/lgcnrb/FluxGet/releases) sayfasindan en son surumu indirin
2. `.msix` veya `.appxbundle` dosyasini calistirin
3. Yolu izleyerek kurulumu tamamlayin

---

## Baslangic

1. Uygulamayi acin
2. **Araclar** sayfasindan `yt-dlp` ve `ffmpeg` dosyalarini secin (YouTube icin gerekli)
3. **Ayarlar** sayfasindan varsayilan indirme konumunu ayarlayin
4. **Tarayici Eklentisi** sayfasindan Chrome eklentisini kurun (opsiyonel)
5. Bir URL yapistirin veya surukleyip birakin!

---

## Tarayici Eklentisi Kurumu

1. `FluxGet/BrowserExtension` klasorunu bir yere kopyalayin
2. Chrome'da `chrome://extensions/` adresine gidin
3. "Gelistirici modu"nu acin
4. "Yuklenmis uzantiyi yukle"ye tiklayin
5. FluxGet klasorunu secin
6. Eklenti hazir!

---

## Lisans

Bu proje [MIT Lisansi](LICENSE) altinda yayinlanmaktadir.

```
MIT License

Copyright (c) 2026 lgcnrb

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction...
```

---

## Katki

Katkiilariniz bekleniyor! Forklayin, branch olusturun ve PR acin.

---

## Iletisim

- **Sorunlar**: [GitHub Issues](https://github.com/lgcnrb/FluxGet/issues)
- **Gelistirici**: lgcnrb
