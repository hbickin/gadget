# MyGadget

Windows masaüstü için araç konsolu (analog gösterge paneli) tarzında bir sistem
izleme şeridi. Tek dosyalık C# / WinForms uygulaması (`mygadget.cs`), ekranın
üst kısmına yerleşen, şeffaf ve kenarlıksız bir şerit içinde CPU, RAM, disk,
ağ, GPU ve (varsa) sıcaklık göstergeleri çizer.

## Gereksinimler

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (derlemek için).
  Çalıştırmak için yalnızca .NET 8 Desktop Runtime yeterlidir; `publish`
  çıktısı tek-dosya exe üretir ve ayrı runtime kurulumu istemeden çalışır
  (bkz. aşağıdaki "Dağıtım" bölümü).
- Yönetici yetkisi **gerekmez**. (ACPI sıcaklık okuması bazı sistemlerde
  yönetici ister; bu durumda sessizce atlanır, uygulama yükseltme istemez.)

## Derleme

```bash
build.cmd
```

Bu, `dotnet build -c Release` çalıştırır. Çıktı:
`bin\Release\net8.0-windows\MyGadget.exe`

Hata olursa pencere kapanmadan hata mesajını görebilmeniz için `pause` ile
durur.

## Dağıtım (tek dosya exe)

```bash
build.cmd publish
```

Bu, şu komutu çalıştırır:

```
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

Çıktı: `publish\MyGadget.exe`. Hedef makinede .NET 8 Desktop Runtime kurulu
olmalıdır (`--self-contained false` kullanıldığı için).

İlk derlemede NuGet, `System.Diagnostics.PerformanceCounter` ve
`System.Management` paketlerini nuget.org'dan indirir. İnternet erişimi
yoksa bu paketleri elle indirip yerel bir NuGet kaynağına eklemeniz gerekir.

## Sıcaklık okuma için: LibreHardwareMonitor

Windows, mantıksal işlemci (thread) başına sıcaklık **vermez**; sıcaklık
yalnızca çekirdek/paket düzeyinde ve genellikle bir kernel sürücüsü
aracılığıyla okunabilir. MyGadget kendi sürücüsünü yüklemez; bunun yerine
sırasıyla şu kaynakları dener:

1. **LibreHardwareMonitor (WMI)** — [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor)
   çalışıyorsa (`root\LibreHardwareMonitor` WMI namespace'i üzerinden).
2. **LibreHardwareMonitor (HTTP)** — LibreHardwareMonitor'ün dahili web
   sunucusu açıksa (`http://localhost:8085/data.json`, port Ayarlar →
   Veri sekmesinden değiştirilebilir).
3. **ACPI** (`MSAcpi_ThermalZoneTemperature`) — son çare; genellikle tek bir
   genel değer verir (çekirdek bazlı değil) ve bazı sistemlerde yönetici
   yetkisi ister.

**Sıcaklık göstergelerinin çalışması için LibreHardwareMonitor'ü indirip
çalıştırmanız önerilir.** Hiçbir kaynak bulunamazsa göstergeler gösterilmez
(N/A olan göstergeler şeritte yer kaplamaz); bu bir hata değildir.

**"Package" (genel CPU) göstergesi hakkında:** Windows/donanım izleme
araçları genellikle ayrı bir "genel CPU sıcaklığı" değeri vermez — yalnızca
çekirdek başına sıcaklık ve (varsa) üretici bazlı bir paket/Tctl/Die sensörü
raporlanır. Kullandığınız kaynak (LibreHardwareMonitor vb.) ayrı bir
paket/Tctl/Die sensörü vermiyorsa, MyGadget "Package" göstergesinde o anki
**en sıcak çekirdeğin** değerini otomatik olarak gösterir; böylece bu
gösterge hangi donanımda olursanız olun her zaman anlamlı bir "genel CPU
sıcaklığı" sunar.

## Ayarlar

Sağ tıklama (veya sistem tepsisi simgesi → Ayarlar) ile açılan pencerede 4
sekme bulunur:

- **Göstergeler** — her göstergeyi göster/gizle, sırasını değiştir (▲/▼),
  sarı/kırmızı eşik değerleri, ağ adaptörleri için elle maksimum hız
  (Mbps). İş parçacığı (thread) başına mini göstergeler "Veri" sekmesinden
  tek bir anahtarla açılıp kapatılır.
- **Veri** — güncelleme aralığı, yumuşatma (hareketli ortalama) örnek
  sayısı, ibre sönüm katsayısı, tepe değer ibresi ve tutma süresi, sıcaklık
  kaynağı/birimi, sanal/VPN adaptörlerini gizleme.
- **Görünüm** — ölçek, boşluk, tema (Krom/Koyu spor/Açık/Retro), saydamlık,
  konum (Üst-orta/Üst-sol/Üst-sağ/Serbest) ve hedef monitör, dijital
  değer/etiket görünürlüğü, LED ayarları (açık/kapalı, yanıp sönme hızları,
  histerezis, gecikme).
- **Davranış** — her zaman üstte / masaüstü seviyesinde, konumu kilitle,
  click-through (fare tıklamalarını geçir — açıkken ayarlara sistem
  tepsisinden ulaşın), tam ekran uygulama/oyun açıkken gizle, Windows ile
  başlat.

Ayarlar `%APPDATA%\MyGadget\settings.json` dosyasına kaydedilir ve açılışta
okunur. Dosya bozuksa veya okunamazsa varsayılan ayarlarla açılır. Konumu
kilitli değilken şeridi sol tıkla sürükleyebilirsiniz; bıraktığınız yer
otomatik olarak "Serbest" konuma kaydedilir.

Hatalar kullanıcıya mesaj kutusu olarak gösterilmez; `%APPDATA%\MyGadget\log.txt`
dosyasına yazılır.

İkinci bir MyGadget örneği başlatmaya çalışırsanız (örn. kısayolu tekrar
tıklarsanız), yeni süreç hemen kapanır ve zaten çalışan örneğin Ayarlar
penceresi açılır.

## Bilinen sınırlamalar

- "Masaüstü seviyesinde" seçeneği, Gezgin'in `WorkerW` penceresine yeniden
  ebeveynleme (Rainmeter/wallpaper engine'lerin kullandığı yöntem) ile
  çalışır. Bu resmî olmayan bir davranıştır; büyük bir Windows güncellemesi
  bunu bozarsa uygulama çökmez, sessizce normal pencere moduna düşer.
- GPU kullanım göstergeleri, Windows'un "GPU Engine" performans
  sayaçlarına dayanır (Windows 10 1803+ gereklidir).
- Ağ hız yüzdesi, adaptörün bildirdiği bağlantı hızına (`NetworkInterface.Speed`)
  göre hesaplanır; bu değer yanlış veya belirsizse Ayarlar → Göstergeler'den
  ilgili adaptör için elle Mbps girilebilir.

## Dosyalar

- `mygadget.cs` — uygulamanın tamamı (tek dosya).
- `MyGadget.csproj` — proje dosyası (.NET 8, WinForms).
- `build.cmd` — derleme (`build.cmd`) ve dağıtım (`build.cmd publish`) betiği.
- `.gitignore` — derleme çıktıları ve kişisel ayar/log dosyaları hariç
  tutulur.
