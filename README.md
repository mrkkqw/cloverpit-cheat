# CloverCheat — эксплойт/чит-меню для CloverPit

Внутриигровое чит-меню (BepInEx 5 плагин) для игры CloverPit (Panik Arcade, Unity 6000.2.6f2 Mono).
Позволяет прямо во время игры менять деньги, билеты, шансы символов, множители, удачу, спины и «всю хуйню».

## Установка (уже выполнена)

В папку игры `C:\Program Files (x86)\Steam\steamapps\common\CloverPit` установлено:

- **BepInEx 5.4.23.4 x64** (важно: именно **.4** — версии 5.4.23.2 крашат игру на Unity 6, 5.4.23.4 с Doorstop 4.4.1 работает; BepInEx 6 на этой игре не грузит плагины)
- **BepInEx\plugins\CloverCheat.dll** — сам эксплойт

Запуск игры как обычно через Steam. Плагин детектится игрой как мод (бейдж MODDED — чисто косметика, см. ниже).

## Использование

| Клавиша | Действие |
|---|---|
| **F1** | Открыть/закрыть чит-меню |
| **F6** | Быстро +100 000 монет |
| **F7** | Быстро +100 билетов |
| **F8** | Быстрая удача x3 на 999 спинов |

Клавиши переназначаются в `BepInEx\config\clover.cheat.menu.cfg`.

### Возможности меню (разделы):

- **MONEY** — +1K/10K/100K/1M/×2, установка точного значения (BigInteger — переполнения нет)
- **TICKETS** — +10/100/1000/×2, установка, бонусные билеты LittleBet/BigBet/раунды
- **BANK / INTEREST** — депозит (добавить/установить), процентная ставка 0–100
- **SPINS** — +1/5/10/до максимума, установка, MaxSpins+1, ExtraSpins, **бесконечные спины (топ-ап каждый кадр)**
- **LUCK** — слайдер 1–100x: PowerupLuck / ActivationLuck / StoreLuck, ExtraLuck на 999 спинов, сброс
- **MULTIPLIERS** — множитель всех символов ×10/×100/установка, всех паттернов ×10/×100/установка, сброс
- **SYMBOL ODDS & MODIFIERS** — выбор символа (lemon…nine): шанс 99%, golden/instant reward/ticket/chain 100%, доп. стоимость; массовое применение на ВСЕ символы; сброс модификаторов
- **PATTERNS** — доп. значение всем паттернам (+= / =), вкл. jackpot
- **EVENT 666** — подавить событие 666 на +10 спинов, форсировать 666 на СЛЕДУЮЩИЙ спин
- **STORE & MISC** — бесплатные рестоки, скидка в магазине 100%, +1 слот под пауэрапы, множитель красной кнопки
- **TIME SCALE** — ползунок скорости игры 0x (фриз) — 5x, кнопки FREEZE/0.5x/1x/2x/3x/5x; значение остаётся и после закрытия меню, вернуть 1x — открыть меню и нажать 1x
- **SAVE GAME NOW** — принудительное сохранение (reason `debug`), чтобы всё зафиксировать в сейве

HUD игры опрашивает монеты каждый кадр — все изменения видны сразу.

## Скрытие бейджа MODDED

В конфиге `BepInEx\config\clover.cheat.menu.cfg` → `Spoof.HideModdedBadge = true` — Harmony-патч `Panik.Master.IsModded()` возвращает false, бейдж не показывается.
⚠️ Учти: подделка результатов в лидербордах — подлость, по умолчанию выключено.

## Сборка из исходников

```
dotnet build CloverCheat\CloverCheat.csproj -c Release
```

Требования: .NET SDK 9, путь к игре прописан в csproj (`GameDir`). Таргет **net462** (игровые DLL собраны под mscorlib 4.0; BigInteger из System.Numerics).

Готовая DLL копируется в `BepInEx\plugins\`.

## Реверс-инжиниринг: карта кода игры

Декомпилированные классы (233 типа) лежат в `cloverpit_re\src\`. Ключевое:

- `GameplayData.cs` (глобальный ns) — статический фасад над `Panik.Data.game.gameplayData`:
  `CoinsGet/Set/Add(BigInteger)`, `CloverTicketsGet/Set/Add(long)`, `Deposit*`, `InterestRate*` (0..100),
  `SpinsLeft*/MaxSpins_*/ExtraSpins*`, `LuckGet`, `ExtraLuck_SetEntry(tag, value, spins, applyPowerup)`,
  `PowerupLuck*/ActivationLuck*/StoreLuck*` (мин. 0.5), `AllSymbolsMultiplier*/AllPatternsMultiplier*` (BigInteger),
  `Symbol_Chance_Set(Kind, float)`, `Symbol_ModifierChance_Set(Kind, Modifier, float)` (0..1),
  `Symbol_CoinsValueExtra_Set(Kind, BigInteger)`, `Pattern_ValueExtra_Set/Add(Kind, double)`,
  `SixSixSix_SuppressedSpinsSet/BookedSpinSet`, `StoreFreeRestocks*/StoreTemporaryDiscount*/MaxEquippablePowerups*/RedButtonActivationsMultiplier*`
- `Panik\Panik_Data.cs` — `Panik.Data.SaveGame(GameSavingReason.debug)`, `Panik.Data.game` → `GameData.inst`
- `Panik\Panik_PlatformDataMaster.cs` — путь сейва `SaveData\GameData\GameDataFull.json`, шифрование `EncryptCustom(data, password)`
- `Panik\Panik_Master.cs` — `IsModded()`: сканирует загруженные сборки на префиксы MonoMod/0Harmony/Harmony/BepInEx/MelonLoader
- Символы: `SymbolScript.Kind { lemon, cherry, clover, bell, diamond, coins, seven, six, nine }`,
  `Modifier { instantReward, cloverTicket, golden, repetition, battery, chain, Revenge, SMult, Interest, Copy }`
- Паттерны: `PatternScript.Kind { jackpot, horizontal2-5, vertical2-3, diagonal2-3, pyramid, pyramidInverted, triangle, triangleInverted, snakeUpDown, snakeDownUp, eye }`

## Сейвы

`SaveData\GameData\GameDataFull.json` — XOR-шифрованный JSON (ключ 27 байт, см. `SaveTool.ps1`).
Инструмент: `powershell -File cloverpit_re\SaveTool.ps1 decrypt|encrypt|verify`

## Устранение неполадок

- Игра крашится на старте (0xC0000005) → стоит BepInEx 5.4.23.2 вместо **5.4.23.4** (Doorstop 4.4.1 обязателен для Unity 6000)
- Меню не открывается → смотрим `BepInEx\LogOutput.log`, раздел `:CloverCheat`
- «game data: not loaded» → нужно зайти в ран (данные gameplayData ещё не созданы в главном меню)
- Сохранение не происходит → игра не сохраняет во время отсчёта смерти и на кастомном сиде (так задумано в игре)
