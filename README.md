# Bazde-Game

Unity 6 (URP) ile geliştirilen 3D parkur oyunu projesi.

---

## Kurulum ve Projeyi Açma (Ekip İçin Adım Adım)

### 1. Gereksinimler
- **Unity Hub** (güncel sürüm)
- **Unity Editor `6000.6.9f1`** (Unity 6)  
  > Başka bir Unity sürümüyle açılırsa paket veya sahne uyumluluk sorunları çıkabilir. Unity Hub üzerinden mümkünse tam olarak `6000.6.9f1` sürümünü kurun.
- **Git** (veya GitHub Desktop / ZIP indirme)

### 2. Projeyi İndirme
Terminal üzerinden klonlamak için:
```bash
git clone https://github.com/Ryzgar73k/Bazde-Game.git
```
Alternatif olarak GitHub sayfasından **Code > Download ZIP** ile indirip klasöre çıkarabilirsiniz.

### 3. Unity Hub'a Ekleme ve İlk Açılış
1. **Unity Hub**'ı açın.
2. **Add > Add project from disk** seçeneğine tıklayın ve indirdiğiniz `Bazde-Game` klasörünü seçin.
3. Unity sürümü olarak **`6000.6.9f1`** seçili olduğundan emin olup projeyi açın.
4. `Library/` klasörü depoda tutulmadığı için ilk açılışta Unity varlıkları ve paketleri yeniden derleyecektir (**2–5 dakika** sürebilir, normaldir).

### 4. Doğru Sahneyi Açma
Proje açıldığında boş bir sahne gelirse **Project** penceresinden şu sahneye çift tıklayın:
- **`Assets/Scenes/Level1.unity`** *(Ana oynanabilir parkur sahnesi)*

> Not: Build ayarlarında da şu an yalnızca `Assets/Scenes/Level1.unity` aktiftir. `Level2.unity` ve `Test.unity` henüz boş/taslak sahnelerdir.

---

## Kontroller

| Platform | Hareket | Koşma | Zıplama | Kamera | İmleç |
|---|---|---|---|---|---|
| **PC (Klavye + Fare)** | `W A S D` | `Sol Shift` (basılı tut) | `Space` | Fare hareketi | `ESC`: İmleci açar, `Sol Tık`: İmleci kilitler |
| **Gamepad** | Sol Analog | Sol Analog Basma (`L3`) | Güney Tuşu (`A` / `X`) | Sağ Analog | - |
| **Mobil / Dokunmatik** | Sol alt sanal joystick | Sağ alttaki `KOŞ` butonu | Sağ alttaki `ZIPLA` butonu | Ekranın sağ tarafını sürükle | - |

---

## Proje Yapısı

- `Assets/Scenes/`
  - `Level1.unity`: Parkur, karakter, kamera, mobil kontroller, dönen çark, dönen silindirler ve trambolinin bulunduğu aktif sahne.
  - `Level2.unity`, `Test.unity`: Şu an sadece `Main Camera` ve `Directional Light` içeren boş sahneler.
- `Assets/Scripts/`
  - `PlayerController.cs`: `CharacterController` tabanlı hareket, coyote time, jump buffer, havada kontrol, basamak/eğim desteği ve engel/trambolin etkileşimi.
  - `PlayerInputState.cs`: Klavye, fare, gamepad ve dokunmatik UI girdilerini birleştirir.
  - `ThirdPersonCamera.cs`: Üçüncü şahıs takip kamerası (PC'de fare kilitleme dahil).
  - `MobileControlsUI.cs`: Çalışma anında dokunmatik joystick ve `ZIPLA` / `KOŞ` butonlarını oluşturur.
  - `RootMotionRelay.cs`: Zıplama animasyonundaki root motion verisini kapsüle aktarır.
  - `ContinuousRotator.cs`: Dış çark (`OnSag_Cark_Dis_Cember`) ve 4 kolunu tek pivot üzerinden döndürür, üstündeki oyuncuyu döndürür/kaydırır.
  - `RotatingCylinderObstacle.cs`: Döner silindir parçalarını (`Sag_Dev_Sisme_Silindir_Parca_*`) grup halinde döndürür ve oyuncuyu kaydırır.
  - `RotatorPivotMarker.cs`: Dönen grupların dinamik pivotlarında referans tutar.
  - `TrampolinePad.cs`: `Merkez_Trambolin_Yuzeyi` üzerinde oyuncuyu belirli aralıklarla (`2 sn`, `6 m`) yukarı fırlatır.
- `Assets/Scripts/Editor/`
  - `LevelBoxColliderTool.cs`: `Assets/Models/` altındaki FBX'lere collider ekler ve editör açılışında çark/silindir/trambolin bileşenlerini isimlere göre otomatik bağlar.
  - `PlayerSetupMenu.cs`, `AnimationSetupTool.cs`, `FixRemyMaterials.cs`: `Tools > Parkur > ...` altındaki kurulum yardımcıları.
- `Assets/Prefabs/Parkur3D/`: Parkur parçalarının prefabları (97 adet).
- `Assets/Models/level-one.fbx`: Parkurun ana 3D model dosyası.

---

## Ortak Çalışma Notları ve Dikkat Edilecekler

- **Git ile çalışırken:**
  - `.meta` dosyalarını kesinlikle silmeyin ve sahnede yaptığınız değişikliklerde ilgili `.meta` dosyalarını da commit'leyin.
  - Aynı anda aynı sahne (`Level1.unity`) üzerinde birden fazla kişi düzenleme yaparsa çakışma (merge conflict) çıkabilir; sahne düzenlemeden önce ekibe haber verin ve değişiklikleri prefab üzerinden yapmaya çalışın.
- **Editör Menüleri Uyarısı (`Tools > Parkur`):**
  - `Level1.unity` içindeki `Player` ayarları sahneye özel düzenlenmiştir (`CharacterController.center.y = 3.25` vb.). **`Tools > Parkur > Oyuncuyu Kur`** menüsünü mevcut `Level1` sahnesinde çalıştırırsanız oyuncu kapsülü ve modeli varsayılan değerlere sıfırlanabilir.
  - **Düzeltme Önerisi (Not):** Yeni seviyelerde (`Level2.unity` vb.) bu aracı kullanmak yerine doğrudan `Assets/Prefabs/Player.prefab` dosyasını sahneye sürüklemek daha güvenlidir. İleride istenirse `PlayerSetupMenu.cs` içindeki varsayılan kapsül/model değerleri `Player.prefab` ve `Level1.unity` ile eşitlenerek menü aracının sahnedeki hizalamayı bozması tamamen önlenebilir.
- **Engel İsimlendirme Kuralları:**
  - `LevelBoxColliderTool.cs` editör açılışında/derlemede nesne isimlerine bakarak otomatik işlem yapar:
    - `OnSag_Cark_Dis_Cember` ve `OnSag_Cark_Kol*` -> Çark döndürücü (`ContinuousRotator`)
    - `Sisme_Silindir` içeren (ve `Destek` içermeyen) parçalar -> Döner silindir (`RotatingCylinderObstacle` + `MeshCollider`)
    - `Merkez_Trambolin_Yuzeyi` -> Trambolin (`TrampolinePad`)
  - Sahneye yeni engel eklenecekse veya isim değiştirilecekse bu kurallara dikkat edilmelidir.
- **PC Testinde Ekrandaki Mobil Butonlar ve İmleç Kilidi:**
  - `Level1.unity` içinde `MobileControls` nesnesinde `Show On Desktop` açıktır (`true`); oyun telefon için geliştirildiğinden editörde mobil arayüzü sürekli görmek açısından faydalıdır.
  - Ancak `ThirdPersonCamera.cs` masaüstünde sol tıkta fare imlecini tekrar kilitlediği için, `ESC` ile imleci açsanız bile ekrandaki dokunmatik `ZIPLA` / `KOŞ` butonlarına veya joystick'e fareyle tıkladığınızda imleç yeniden kilitlenir.
  - **Düzeltme Önerisi (Not):** İleride `ThirdPersonCamera.cs` içinde UI elemanlarının (`EventSystem.current.IsPointerOverGameObject()`) üzerine tıklandığında imleci kilitlemeyecek şekilde güncelleme yapılabilir ve `MobileControlsUI.HoldButton` için `OnPointerExit` eklenerek parmağı buton dışına kaydırınca koşunun takılı kalması önlenebilir. Editörde mobil görünüm/dokunmatik test için **Unity Device Simulator** (*Game → Simulator*) da kullanılabilir.

---

## Yapılacaklar / Yol Haritası (Ekip İletişimi)

Ekip üyeleri eklediği veya değiştirdiği maddeleri buraya işaretleyebilir:

- [ ] **Düşme / Respawn Sistemi:** Parkurdan aşağı düşen oyuncuyu başlangıca veya son checkpoint'e döndürme
- [ ] **Checkpoint Sistemi:** Parkur boyunca kayıt noktaları
- [ ] **Bitiş Noktası ve Level Geçişi:** Parkur sonu tetikleyicisi, bayrak/Timeline etkileşimi ve `Level2.unity` sahnesine geçiş
- [ ] **Level 2 Tasarımı:** Şu an boş olan `Assets/Scenes/Level2.unity` sahnesinin oluşturulması ve Build Settings'e eklenmesi
- [ ] **PC / Mobil Kontrol Ayrımı:** PC build'lerinde `MobileControlsUI.showOnDesktop` ayarının kapatılması veya platforma göre otomatik gizlenmesi
- [ ] **Ses Efektleri ve Müzik:** Zıplama, koşma, trambolin ve ortam sesleri
- [ ] **Arayüz (UI):** Başlangıç menüsü, duraklatma (`ESC` menüsü) ve bölüm sonu ekranı
