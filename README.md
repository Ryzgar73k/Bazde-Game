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
- **Karakter ve Kamera (`PlayerController` v7 / `ThirdPersonCamera` v2):**
  - `PlayerController.cs`: Mobile-First ayarlar (`walkSpeed = 4.0`, `runSpeed = 7.2`, `acceleration = 45`, `jumpHeight = 1.70`, `lowJumpGravityMultiplier = 2.6`, `fallGravityMultiplier = 1.35`, `trampolineMaxHorizSpeed = 5.2`, `coyoteTime = 0.16`, `jumpBufferTime = 0.16`, `mantleDuration = 0.42`).
  - **Auto Ledge Mantle:** Statik platform kenarlarında (`0.22m..0.82m` yükselme bandı) duvar normaline dik hizalanarak (`0.42s`) otomatik tırmanma; tehlikeler (`TimedSpikeTrap`, `RotatingHazard`, `KillZone`), trambolin ve hareketli/dönen platformlarda tetiklenmez.
  - **`dev/umutcan` Uyumlu Korumalar ve Sistemler:** `Checkpoint` (disk `BoxCollider` + direk `CapsuleCollider` katı çarpıştırıcılı), `RespawnManager`, `MovingPlatform`, `KillZone`, `LevelGoal`, `VictoryUI` (`VictoryUI.IsAnyVictoryVisible` ile imleç kilidini otomatik açar), `PressureButton`, `ActivatedBridge`, `TimedSpikeTrap`, `RotatingHazard` (`CenterHubBlocker` merkez çarpıştırıcılı, `11.5 m/s` dışarı savurma), `LevelHUD`, `MobileControlsUI` içindeki `EventSystem` + `InputSystemUIInputModule.AssignDefaultActions()` ve trambolin yatay hız tavanı (`airHorizSpeedCap = 5.2`).
  - **Diğer Çekirdek Bileşenler:** `TrampolinePad.cs` (squash-stretch animasyonlu fırlatma), `ContinuousRotator.cs` (`useExplicitSetup` desteği), `RotatingCylinderObstacle.cs` (`standalonePivot` desteği, `95..115 deg/s` dönüş + `playerPushMultiplier = 0.45`), `FootIK.cs` (ayak IK + eğimde gövde ileri/geri/yan eğilmesi), `RootMotionRelay.cs`.
- **Eski Referans Varlıklar:** `Assets/Models/level-one.fbx`, `Assets/Prefabs/Parkur3D/` ve `LevelBoxColliderTool.cs` (eski sahne kontrolü korumalı) geriye dönük uyumluluk için tutulur.

---

## Mixamo Animasyon Seti (`Assets/Animations/` + `X Bot@Running.fbx`)

`PlayerAnimatorController.controller` 8 durumlu (`Locomotion`, `JumpUp`, `RunJump`, `FallLoop`, `Bounce`, `BounceTwist`, `Land`, `Mantle`) yapıda kuruludur; `AnimationSetupTool.cs` kare kırpmalarını, `In Place` olmayan kliplerde kök XZ çıkarımını ve Humanoid dönüş/kalça hizasını otomatik ayarlar (`Tools > Parkur > Animasyonları Ayarla ve Bağla`). `FallLoop` yalnızca yüksek düşüşlerde (`VerticalVelocity < -7.0` / `-11.5` / `-8.5`) devreye girer.

- [x] **`Anim_Idle`** *(Breathing Idle)* — Loop
- [x] **Walk / Run (`Assets/X Bot@Running.fbx`)** — BlendTree `4.0 m/s` (`0.72x` hız) ve `7.2 m/s` (`1.05x` hız)
- [x] **`Anim_JumpUp`** *(Jumping, `12..32f` kırpılmış; dururken/yürürken)* — Loop kapalı
- [x] **`Anim_RunJump`** *(Running Jump, `0..20f` kırpılmış; koşarken zıplama)* — Loop kapalı
- [x] **`Anim_FallLoop`** *(Falling Idle — yalnız yüksek düşüşte)* — Loop açık
- [x] **`Anim_Land`** *(Falling To Landing, `8..26f` kırpılmış)* — Loop kapalı
- [x] **`Anim_Bounce_1`** *(Front Flip, `13..45f` kırpılmış)* & **`Anim_Bounce_2`** *(Front Twist Flip, `15..44f` kırpılmış)* — Trambolinde %50/%50 rastgele takla
- [x] **`Anim_Mantle`** *(Braced Hang To Crouch, `3..29f` kırpılmış, `1.65x` hız)* — Duvara dik hizalı kenar tırmanma

---

## Yol Haritası

- [x] **KayKit 4-Level Standardı (`Level1`–`Level4`):** Tek paket ve ortak kurucu (`KayKitLevelKit`) ile oynanabilir 4 seviye ve Build Settings entegrasyonu
- [x] **Mobil-Öncelikli Kontrol ve Hissiyat Revizyonu:** Analog yürüme/koşma joystick'i (`WalkRunRing`), tek `ZIPLA` butonu, çoklu dokunuş `LookPad`, duvara hizalı Auto Ledge Mantle, trambolin hız tavanı (`5.2 m/s`), eğimde gövde eğilmesi ve squash-stretch
- [x] **`dev/umutcan` Sistemlerinin Adaptasyonu:** `Checkpoint`, `RespawnManager`, `MovingPlatform`, `KillZone`, `LevelGoal`, `VictoryUI` (+ imleç açma) ve `EventSystem` varsayılan aksiyonları
- [x] **Mixamo Animasyon Paketinin Bağlanması:** `Assets/Animations/` + `X Bot@Running.fbx` kare kırpmaları, `RunJump` ayrımı ve `Bounce` varyasyonlarıyla bağlandı
- [ ] **Ses Efektleri ve Ana/Duraklatma Menüleri:** Zıplama, koşma, trambolin sesleri ve başlangıç/duraklatma (`ESC`) arayüzü
- [ ] **Final İnceleme ve Branch Birleştirme:** Kullanıcı incelemesi tamamlandıktan sonra push ve `dev/umutcan` ile final birleştirme
