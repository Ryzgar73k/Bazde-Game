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

### 4. Doğru Sahneyi Açma ve KayKit 4-Level Standardı
Proje açıldığında boş bir sahne gelirse **Project** penceresinden şu sahneyi açın:
- **`Assets/Scenes/Level1.unity`** *(1. bölüm — tüm `Level1..Level4` sahneleri KayKit - Platformer Pack standardında kuruludur ve Build Settings'e kayıtlıdır)*

İstediğiniz zaman üst menüden dört standart bölümü tek tıkla yeniden üretebilirsiniz:
- **`Tools > Parkur > Tum KayKit Levellerini Olustur (1-4)`**

---

## Kontroller

| Platform | Hareket / Koşma | Zıplama | Kamera | İmleç |
|---|---|---|---|---|
| **PC (Klavye + Fare)** | `W A S D` + `Sol Shift` (basılı tut) | `Space` (kısa/uzun basış = değişken yükseklik) | Fare hareketi | `ESC`: İmleci açar, `Sol Tık`: UI dışı alanda imleci kilitler |
| **Gamepad** | Sol Analog (`0..0.55` Yürüme -> `0.55..1.0` Koşma) | Güney Tuşu (`A` / `X`) | Sağ Analog | - |
| **Mobil / Dokunmatik** | Sol alt tek analog joystick (`0..0.55` Yürüme, `0.55..1.0` Koşma; ayrı `KOŞ` butonu yoktur) | Sağ alt tek büyük `ZIPLA` butonu | Ekranın sağ %60'ı (`LookArea` / `LookPad`, çoklu dokunuş) | UI butonlarına tıklandığında imleç kilitlenmez |

---

## Temel Mekanik ve Sahne Yapısı

- **KayKit Standart Sahneler (`Assets/Scenes/`):**
  - `Level1.unity` -> `Level2.unity` -> `Level3.unity` -> `Level4.unity` -> `Level1.unity` döngüsü kuruludur.
  - Ortak kurucu `Assets/Scripts/Editor/KayKitLevelKit.cs`, 1–3 kurucusu `KayKitLevelsBuilder.cs`, 4. bölüm kurucusu `Level4Builder.cs` içindedir.
  - Yön tabelaları (`signage_arrows_*`), bitiş/başlangıç kemerleri ve checkpoint direkleri katı çarpıştırıcılıdır (`BoxCollider` / `CapsuleCollider`).
- **Karakter ve Kamera (`PlayerController` v10 / `ThirdPersonCamera` v2):**
  - `PlayerController.cs`: Dengelenmiş parkur ayarları (`walkSpeed = 2.1`, `runSpeed = 4.5`, `acceleration = 28`, `deceleration = 32`, `turnAccelerationMultiplier = 1.35`, `turnSpeed = 390`, `jumpHeight = 1.70`, `lowJumpGravityMultiplier = 2.6`, `fallGravityMultiplier = 1.35`, `trampolineMaxHorizSpeed = 4.3`, `coyoteTime = 0.16`, `jumpBufferTime = 0.16`, `mantleDuration = 0.54`).
  - `ThirdPersonCamera.cs` & `PlayerInputState.cs`: DPI ölçekli mobil bakış hassasiyeti (`0.135` yatay / `0.105` dikey), yatay/dikey takip yumuşatması ve duvar çarpışma önleme.
  - **Auto Ledge Mantle & El IK:** Statik platform kenarlarında (`0.22m..1.05m` yükselme bandı) duvar normaline dik hizalanarak (`0.54s`) `FootIK.ApplyMantleHandIK` el tutunmasıyla otomatik tırmanma; tehlikeler (`TimedSpikeTrap`, `RotatingHazard`, `KillZone`), trambolin ve hareketli/dönen platformlarda tetiklenmez.
  - **`dev/umutcan` Uyumlu Korumalar ve Sistemler:** `Checkpoint` (disk `BoxCollider` + direk `CapsuleCollider` katı çarpıştırıcılı), `RespawnManager`, `MovingPlatform` (`FrameDelta` + `GetComponent<PlayerController>() == null`), `KillZone`, `LevelGoal`, `VictoryUI` (+ `LevelHUD` yıldız/elmas özeti ve imleç kilidini otomatik açma), `PressureButton`, `ActivatedBridge`, `TimedSpikeTrap`, `RotatingHazard` (`CenterHubBlocker` merkez çarpıştırıcılı, `11.5 m/s` dışarı savurma), `MobileControlsUI` (`EventSystem` + `InputSystemUIInputModule.AssignDefaultActions()`) ve trambolin hız tavanı (`airHorizSpeedCap = 4.3`).
  - **Diğer Çekirdek Bileşenler:** `TrampolinePad.cs` (squash-stretch ve dinamik `BounceSpeed`), `ContinuousRotator.cs` (`useExplicitSetup` desteği), `RotatingCylinderObstacle.cs` (`standalonePivot` desteği, 4 segment / 8m genişlik, `130..160 deg/s` dönüş, `playerPushMultiplier = 0.56`), `FootIK.cs` (ayak IK + eğimde gövde eğimi + tırmanma el IK), `RootMotionRelay.cs`.
- **Android / Mobil Yapılandırma:** `com.bazde.parkur`, yatay (*Landscape*) kilitli, `IL2CPP` + `ARM64`, Android API 26+, `vulkanEnablePreTransform = 0`, `Mobile_RPAsset` (`RenderScale = 1.0`, `2048` 2-kaskadlı yumuşak gölge, `MSAA` kapalı).
- **Eski Referans Varlıklar:** `Assets/Models/level-one.fbx`, `Assets/Prefabs/Parkur3D/` ve `LevelBoxColliderTool.cs` (eski sahne kontrolü korumalı) geriye dönük uyumluluk için tutulur.

---

## Animasyon Seti (`Kevin Iglesias HumanM` + `Mixamo`)

`PlayerAnimatorController.controller` 9 durumlu (`Locomotion`, `JumpUp`, `RunJump`, `InAir`, `FallLoop`, `Bounce`, `BounceTwist`, `Land`, `Mantle`) yapıda kuruludur; `AnimationSetupTool.cs` kırpılmış zıplama/iniş kliplerini ve Humanoid ayarlarını otomatik bağlar (`Tools > Parkur > Animasyonları Ayarla ve Bağla`).

- [x] **Locomotion (`HumanM` BlendTree):** `Idle01` (`0`) -> `Walk01_Forward` (`1.4 m/s`, `1.02x`) -> `Run01_Forward` (`2.8 m/s`, `1.06x`) -> `Sprint01_Forward` (`4.5 m/s`, `1.10x`)
- [x] **`JumpUp` & `RunJump` (`BM_JumpAir_Clean.anim`):** `HumanM@Jump01 - Begin` kırpılmış temiz klip (`0.00..0.44s`; `0.68x` / `0.72x` hız — eller omuz hizası altında kalır)
- [x] **`Land` (`BM_JumpLand_Clean.anim`):** `HumanM@Jump01 - Land` kırpılmış temiz klip (`0.17..0.56s`, `1.25x` hız)
- [x] **`InAir` (`HumanM@Fall01`) & `FallLoop` (`Anim_FallLoop`):** Yalnızca gerçek boşluk düşüşlerinde (`InAir < -6.5 / -16.0`, `FallLoop < -14.0 / -19.5 / -23.0`) devreye girer
- [x] **`Bounce` & `BounceTwist` (`Anim_Bounce_1` / `Anim_Bounce_2`):** Trambolinde %50/%50 rastgele takla; uçuş süresine göre dinamik `BounceSpeed` (`0.56..0.82`)
- [x] **`Mantle` (`Anim_Mantle`):** Braced Hang To Crouch (`3..29f` kırpılmış, `1.28x` hız + `FootIK` el tutunması)

---

## Yol Haritası

- [x] **Faz 1 — KayKit 4-Level, Kontrol, Animasyon & Mobil APK (`v0.1.0`):** `Level1`–`Level4` KayKit kurulumu, `dev/umutcan` mekaniklerinin uyarlanması, `HumanM` + Mixamo hibrit animasyonlar, Foot/Hand IK, hız & dönüş dengesi, Android URP/Vulkan düzeltmeleri ve telefon testi (PR `#2` & Release `v0.1.0`)
- [ ] **Faz 2 — Oyun Hissi ("Juice"), Fizik Tepkileri & Görsel Çeşitlilik:** Çarpma/itmede sendeleme (*stagger*) veya yüksek düşüşte *ragdoll* değerlendirmesi, eğimde ek gövde uyumu, yıldız/buton/trambolin için parlama (*pulse*), *camera shake*, parçacık/ses efektleri ve kompleks kit birleşimleri
- [ ] **Ses Efektleri ve Ana/Duraklatma Menüleri:** Zıplama, koşma, trambolin sesleri ve başlangıç/duraklatma (`ESC`) arayüzü
