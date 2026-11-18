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
  - `PlayerController.cs`: `CharacterController` tabanlı hareket, değişken zıplama yüksekliği, coyote time, jump buffer, havada kontrol, basamak/eğim desteği, denize düşünce (`killY`) yeniden doğma ve engel/trambolin etkileşimi.
  - `PlayerInputState.cs`: Klavye, fare, gamepad ve dokunmatik UI girdilerini birleştirir.
  - `ThirdPersonCamera.cs`: Üçüncü şahıs takip kamerası (PC'de fare kilitleme dahil).
  - `MobileControlsUI.cs`: Çalışma anında dokunmatik joystick ve `ZIPLA` / `KOŞ` butonlarını oluşturur.
  - `RootMotionRelay.cs`: Zıplama animasyonundaki root motion verisini kapsüle aktarır.
  - `ContinuousRotator.cs`: Dış çark (`OnSag_Cark_Dis_Cember`) ve 4 kolunu tek pivot üzerinden döndürür, üstündeki oyuncuyu döndürür/kaydırır.
  - `RotatingCylinderObstacle.cs`: Döner silindir parçalarını (`Sag_Dev_Sisme_Silindir_Parca_*`) grup halinde döndürür ve oyuncuyu kaydırır.
  - `RotatorPivotMarker.cs`: Dönen grupların dinamik pivotlarında referans tutar.
  - `TrampolinePad.cs`: `Merkez_Trambolin_Yuzeyi` üzerinde oyuncuyu basıldığı anda (`0.25 sn` tekrar aralığı, `6 m`) yukarı fırlatır.
  - `FootIK.cs`: Unity Humanoid IK (`OnAnimatorIK`) ile ayakların eğim ve silindir yüzeylerine basmasını ve kalça hizasını ayarlar.
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
  - `Level1.unity` içindeki `Player` ayarları sahneye özel düzenlenmiştir (`CharacterController.center.y = 2.785` vb.). **`Tools > Parkur > Oyuncuyu Kur`** menüsünü mevcut `Level1` sahnesinde çalıştırırsanız oyuncu kapsülü ve modeli varsayılan değerlere sıfırlanabilir.
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
- **Trambolin Yerleşimi ve Görsel/Fiziksel Geri Bildirim (`Merkez_Trambolin_Yuzeyi`):**
  - Trambolin şu an parkurda zıplama gerektirmeyen bir konuma yerleştirilmiş durumda; seviye tasarımı düzenlemesinde oyuncunun gerçekten yararlanacağı/atlayacağı işlevsel bir yere taşınması gerekiyor.
  - **Önerilen İyileştirmeler (Planlanan):** `TrampolinePad.cs` içinde `Bounce()` tetiklendiği anda pedin kendi `localScale` değerini kısa süreli yaylandıran (`~0.08 sn` sıkışıp `0.15 sn` eski haline dönen *Squash & Stretch* coroutine), yukarı doğru sıçrayan bir `ParticleSystem` efekti ve `AudioSource.PlayOneShot` zıplama sesi eklenerek tepki hissi güçlendirilebilir.
- **Havada Dik (`Idle`) Pozda Asılı Görünme Sorunu (`Level1.unity` / `PlayerAnimatorController` Notu):**
  - **Kök Neden:** O noktada karakterin ayağının altında gizli bir collider yoktur (`On_Kemer_Kopru_*` ve `OnSol_Ust_Kemer_*` non-convex `MeshCollider`, `Merkez_Trambolin_Yuzeyi` ise `y = 0.69` lokal yüksekliğindedir). Karakter havada doğrudan `Idle` (şu an `Assets/char@Bored.fbx`, eskiden `X Bot@Offensive Idle.fbx`) animasyonuna geçmiştir:
    1. Eski `PlayerAnimatorController.controller` içinde `!IsGrounded` (düşüş) geçişi bulunmadığından, zıplamadan kenardan düşerken yön tuşu bırakılınca (`Speed < 0.2`) Animator havada `Idle` durumuna dönüyordu.
    2. Eski `Bounce()` sırasında `cc.Move` / `OnControllerColliderHit` aynı karede `IsGrounded` parametresini `true` bırakabildiği için fırlatma anında `Jump -> Idle` geri dönüşü oluşabiliyordu.
    3. `Reload Domain disabled` (`Enter Play Mode Options`) etkinken Play Mode açıkken derleme yapılırsa `LevelAutoInitializer` controller yenilemesini o anda atlar (`Parameter 'Hash -1380605809' does not exist` logu üretebilirdi). `LevelAutoInitializer` artık Play Mode çıkışında (`PlayModeStateChange.EnteredEditMode`) da çalışacak şekilde güncellendi ve `PlayerController` parametre önbellek koruması eklendi. *(Play Mode açıksa controller'ın güncellenmesi için Play Mode'dan bir kez çıkıp tekrar girmek gerekir.)*
- **Genel Animasyon Durumu ve Planlanan Düzeltmeler (`Jump` & `Idle`):**
  - Şu an `Idle` için `Assets/char@Bored.fbx`, `Jump` için ise geçici olarak `Assets/X Bot@Jumping.fbx` (`JumpRise` / `JumpFall` alt klipleri) kullanılmaktadır.
  - **Planlanan:** `Jump` ve `Idle` başta olmak üzere genel animasyon düzeltmeleri (daha uyumlu zıplama/düşüş/iniş klipleri, `Idle` poz/döngü iyileştirmesi, durumlar arası daha akıcı geçişler ve `FootIK` ince ayarı) sonraki aşamada yapılacaktır.

---

## Yapılacaklar / Yol Haritası (Ekip İletişimi)

Ekip üyeleri eklediği veya değiştirdiği maddeleri buraya işaretleyebilir:

- [ ] **Animasyonların Elden Geçirilmesi:** `Idle`, kısa zıplama (tap), normal zıplama (hold), trambolin zıplaması ve iniş/geçiş animasyonlarının eklenmesi/değiştirilmesi (`X Bot@Jumping.fbx` ve `char@Bored.fbx` geçicilerinin yenilenmesi), `Idle / Walk / Run` Blend Tree, yön değiştirirken pivot/turn-in-place ve `FootIK` ince ayarı
- [ ] **Parkur ve Engel Tasarımı Revizyonu:** `Merkez_Trambolin_Yuzeyi` nesnesinin gerçekten işe yarayacağı anlamlı bir noktaya taşınması; dönen ve üzerinde parçalar bulunan platformda parçaların üzerinden koşarak/zıplayarak kolayca geçilebilmesi sorununun (yükseklik/çarpışma/yerleşim) giderilmesi ve parkurdaki gereksiz parçaların temizlenmesi
- [ ] **Genel Düzenlemeler ve Hissiyat Araştırmaları:** Kamera, zıplama/iniş eğrileri, çarpışma ve parkur akışı için referans araştırmaları ve ince ayarlar
- [ ] **Tutunma ve Tırmanma (Ledge Grab / Climb):** Parkur oynanışı için platform kenarlarına tutunma, kenardan yukarı tırmanma (mantle) ve düşme anında kenar yakalama mekanikleri
- [ ] **Checkpoint & Gelişmiş Respawn Sistemi:** Denize düşünce başlangıca dönme (`killY`) mevcut; parkur boyunca kayıt noktaları (checkpoint) eklenip son checkpoint'e döndürme eklenecek
- [ ] **Bitiş Noktası ve Level Geçişi:** Parkur sonu tetikleyicisi, bayrak/Timeline etkileşimi ve `Level2.unity` sahnesine geçiş
- [ ] **Level 2 Tasarımı:** Şu an boş olan `Assets/Scenes/Level2.unity` sahnesinin oluşturulması ve Build Settings'e eklenmesi
- [ ] **PC / Mobil Kontrol Ayrımı:** PC build'lerinde `MobileControlsUI.showOnDesktop` ayarının kapatılması veya platforma göre otomatik gizlenmesi
- [ ] **Ses Efektleri ve Müzik:** Zıplama, koşma, trambolin ve ortam sesleri
- [ ] **Arayüz (UI):** Başlangıç menüsü, duraklatma (`ESC` menüsü) ve bölüm sonu ekranı
- [ ] **Son Aşama — Branch Birleştirme:** Tüm karakter, animasyon ve parkur düzenlemeleri tamamlandıktan sonra `dev/umutcan` branch'ındaki değişikliklerle (`Level2`, `Level3` vb.) çakışmasız şekilde birleştirme (`merge`) yapılması
