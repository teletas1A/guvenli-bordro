# Güvenli Bordro – Windows Uygulaması

Güvenli Bordro yönetim panelini kendi penceresinde açan Windows uygulaması.

- Panel, adres çubuğu olmadan kendi penceresinde açılır ve oturum açık kalır.
- WhatsApp butonları doğrudan WhatsApp masaüstü uygulamasını açar.
- "Toplu otomatik gönder" butonu, uygulamayla birlikte gelen gönderici programı çalıştırır. Program mesajları WhatsApp'ta sırayla kendisi gönderir.
- Menü (Alt tuşu): Yenile, Geri, Panel adresini değiştir, Yakınlaştır, Çıkış.

## Kurulum dosyası nasıl alınır?

Her yüklemede GitHub uygulamayı otomatik derler:

1. Depoda **Actions** sekmesine gir.
2. En üstteki yeşil ✔ işaretli çalışmaya tıkla.
3. Sayfanın altındaki **Artifacts** bölümünden **GuvenliBordro-Kurulum** dosyasını indir.
4. İndirdiğin zip'i aç ve `GuvenliBordro-Kurulum-1.0.0.exe` dosyasını çalıştır.

Yeni sürüm çıkarmak için `package.json` içindeki `version` değerini artırıp kaydet.

## Dosyalar

| Dosya | Açıklama |
|---|---|
| `main.js` | Uygulama penceresi ve link yönlendirmeleri |
| `package.json` | Uygulama adı, sürüm ve kurulum ayarları |
| `helper/GuvenliBordro.cs` | WhatsApp toplu gönderici programı (derleme sırasında exe yapılır) |
| `build/icon.ico` | Uygulama simgesi |
| `.github/workflows/build.yml` | GitHub'da otomatik derleme |

> ⚠️ Bu depoyu **Private (gizli)** tutun. İçinde yönetim panelinin gizli adresi var.
