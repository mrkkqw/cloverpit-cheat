using System;
using System.Globalization;
using System.Numerics;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CloverCheat
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInProcess("CloverPit.exe")]
    public class CloverCheatPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "clover.cheat.menu";
        public const string PluginName = "CloverCheat";
        public const string PluginVersion = "1.0.0";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> HideModdedBadge;

        private ConfigEntry<KeyboardShortcut> _menuKey;
        private ConfigEntry<KeyboardShortcut> _coinsKey;
        private ConfigEntry<KeyboardShortcut> _ticketsKey;
        private ConfigEntry<KeyboardShortcut> _luckKey;

        // ---------- UI state ----------
        private bool _showMenu;
        private Rect _win = new Rect(40f, 40f, 460f, 660f);
        private UnityEngine.Vector2 _scroll;
        private GUIStyle _boxStyle, _btnStyle, _lblStyle, _txtStyle, _headStyle;
        private bool _stylesInit;

        private string _coinsField = "1000000";
        private string _ticketsField = "1000";
        private string _depositField = "10000";
        private string _interestField = "15";
        private string _spinsField = "30";
        private string _symMultField = "100";
        private string _patMultField = "100";
        private string _symValueField = "100";
        private string _patValueField = "100";
        private float _luckSlider = 10f;
        private int _selectedSymbol;
        private bool _infiniteSpins;

        // OS cursor state before the menu was opened (the game hides the real cursor
        // once in VirtualCursors.Awake and never re-asserts it, so we must restore it ourselves)
        private bool _cursorSaved;
        private bool _cursorWasVisible;
        private CursorLockMode _cursorWasLock;

        private static readonly SymbolScript.Kind[] SymbolKinds =
        {
            SymbolScript.Kind.lemon, SymbolScript.Kind.cherry, SymbolScript.Kind.clover,
            SymbolScript.Kind.bell, SymbolScript.Kind.diamond, SymbolScript.Kind.coins,
            SymbolScript.Kind.seven, SymbolScript.Kind.six, SymbolScript.Kind.nine
        };
        private static readonly string[] SymbolNames =
        {
            "LEMON", "CHERRY", "CLOVER", "BELL", "DIAMOND", "COINS", "SEVEN", "SIX", "NINE"
        };
        private static readonly SymbolScript.Modifier[] UsefulModifiers =
        {
            SymbolScript.Modifier.instantReward, SymbolScript.Modifier.cloverTicket,
            SymbolScript.Modifier.golden, SymbolScript.Modifier.chain,
            SymbolScript.Modifier.battery, SymbolScript.Modifier.repetition,
            SymbolScript.Modifier.SMult, SymbolScript.Modifier.Interest,
            SymbolScript.Modifier.Copy, SymbolScript.Modifier.Revenge
        };

        private void Awake()
        {
            Log = Logger;
            _menuKey = Config.Bind("Hotkeys", "ToggleMenuKey", new KeyboardShortcut(KeyCode.F1), "Show/hide cheat menu");
            _coinsKey = Config.Bind("Hotkeys", "AddCoinsKey", new KeyboardShortcut(KeyCode.F6), "Quick +100000 coins");
            _ticketsKey = Config.Bind("Hotkeys", "AddTicketsKey", new KeyboardShortcut(KeyCode.F7), "Quick +100 tickets");
            _luckKey = Config.Bind("Hotkeys", "LuckBoostKey", new KeyboardShortcut(KeyCode.F8), "Quick luck x3 for 999 spins");
            HideModdedBadge = Config.Bind("Spoof", "HideModdedBadge", false,
                "Patch Panik.Master.IsModded() to report the game as unmodded (hides the 'MODDED' badge).");

            var h = new Harmony(PluginGuid);
            h.PatchAll();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded. Press F1 in game.");
        }

        private void Update()
        {
            if (_menuKey.Value.IsDown())
            {
                _showMenu = !_showMenu;
                if (_showMenu)
                {
                    // save the game's current OS cursor state, then force it visible for IMGUI
                    _cursorWasVisible = Cursor.visible;
                    _cursorWasLock = Cursor.lockState;
                    _cursorSaved = true;
                    Cursor.visible = true;
                    Cursor.lockState = CursorLockMode.None;
                }
                else
                {
                    // restore exactly what the game had (normally hidden + locked, see VirtualCursors.Awake)
                    Cursor.visible = _cursorSaved ? _cursorWasVisible : false;
                    Cursor.lockState = _cursorSaved ? _cursorWasLock : CursorLockMode.Locked;
                    _cursorSaved = false;
                }
            }
            if (_coinsKey.Value.IsDown()) Safe(() => GameplayData.CoinsAdd(100000, true), "coins hotkey");
            if (_ticketsKey.Value.IsDown()) Safe(() => GameplayData.CloverTicketsAdd(100, true), "tickets hotkey");
            if (_luckKey.Value.IsDown()) Safe(() => GameplayData.ExtraLuck_SetEntry("clovercheat", 3f, 999, true), "luck hotkey");

            if (_infiniteSpins)
            {
                Safe(delegate
                {
                    int max = GameplayData.MaxSpins_Get();
                    if (GameplayData.SpinsLeftGet() < max)
                        GameplayData.SpinsLeftSet(max);
                }, "infinite spins");
            }
        }

        private void LateUpdate()
        {
            // The game (Rewired) hides/locks the cursor during gameplay —
            // force it back while the cheat menu is open so IMGUI can be clicked.
            if (_showMenu)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }

        private void OnGUI()
        {
            if (!_showMenu) return;
            // Re-assert cursor state here as well: OnGUI runs after all Update/LateUpdate calls,
            // so it wins over the game hiding the cursor in the same frame.
            if (!Cursor.visible || Cursor.lockState != CursorLockMode.None)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
            InitStyles();
            _win = GUILayout.Window(7331, _win, DrawWindow, "CloverCheat 1.0  (F1 to close)", GUILayout.Width(460f), GUILayout.Height(680f));
        }

        private void InitStyles()
        {
            if (_stylesInit) return;
            _boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
            _lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, margin = new RectOffset(0, 0, 1, 1), richText = true };
            _headStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, richText = true };
            _btnStyle = new GUIStyle(GUI.skin.button) { fontSize = 11, margin = new RectOffset(1, 1, 1, 1) };
            _txtStyle = new GUIStyle(GUI.skin.textField) { fontSize = 12 };
            _stylesInit = true;
        }

        // ================= WINDOW =================

        private void DrawWindow(int id)
        {
            bool inGame;
            try { inGame = Panik.Data.game != null && Panik.Data.game.gameplayData != null; }
            catch { inGame = false; }

            GUILayout.BeginHorizontal();
            GUILayout.Label(inGame ? "<color=green>game data: OK</color>" : "<color=red>game data: not loaded (open a run)</color>", _lblStyle);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (!inGame)
            {
                GUILayout.Label("Start/continue a run first, then reopen this menu.", _lblStyle);
                GUI.DragWindow();
                return;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);

            DrawInfo();
            if (Section("MONEY")) DrawMoney();
            if (Section("TICKETS")) DrawTickets();
            if (Section("BANK / INTEREST")) DrawBank();
            if (Section("SPINS")) DrawSpins();
            if (Section("LUCK")) DrawLuck();
            if (Section("MULTIPLIERS")) DrawMultipliers();
            if (Section("SYMBOL ODDS & MODIFIERS  (selected: " + SymbolNames[_selectedSymbol] + ")")) DrawSymbols();
            if (Section("PATTERNS")) DrawPatterns();
            if (Section("EVENT 666")) Draw666();
            if (Section("STORE & MISC")) DrawStore();
            DrawSave();

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static readonly bool[] _sectionOpen =
        {
            true, true, true, true, true, true, true, true, true, true, true
        };
        private int _sectionIdx;

        private bool Section(string title)
        {
            int i = _sectionIdx++;
            bool open = GUILayout.Toggle(_sectionOpen[i], title, GUI.skin.button);
            _sectionOpen[i] = open;
            return open;
        }

        private void RowReset() { _sectionIdx = 0; }

        // ================= SECTIONS =================

        private void DrawInfo()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("<color=#ffd700>Coins:</color> {0}", FormatBig(GameplayData.CoinsGet())), _lblStyle);
            GUILayout.Label(string.Format("<color=#7CFC00>Tickets:</color> {0}", GameplayData.CloverTicketsGet()), _lblStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Spins: {0}/{1}", GameplayData.SpinsLeftGet(), GameplayData.MaxSpins_Get()), _lblStyle);
            GUILayout.Label(string.Format("Luck: {0:0.00}", GameplayData.LuckGet()), _lblStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("SymMult: {0}", FormatBig(GameplayData.AllSymbolsMultiplierGet(true))), _lblStyle);
            GUILayout.Label(string.Format("PatMult: {0}", FormatBig(GameplayData.AllPatternsMultiplierGet(true))), _lblStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(string.Format("Deposit: {0}  Interest: {1:0.##}%", FormatBig(GameplayData.DepositGet()), GameplayData.InterestRateGet()), _lblStyle);
            GUILayout.Label(string.Format("666 chance: {0:0.###}", GameplayData.SixSixSix_ChanceGet(true)), _lblStyle);
            GUILayout.EndHorizontal();
        }

        private void DrawMoney()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            if (Btn("+1K")) GameplayData.CoinsAdd(1000, true);
            if (Btn("+10K")) GameplayData.CoinsAdd(10000, true);
            if (Btn("+100K")) GameplayData.CoinsAdd(100000, true);
            if (Btn("+1M")) GameplayData.CoinsAdd(1000000, true);
            if (Btn("x2")) GameplayData.CoinsAdd(GameplayData.CoinsGet(), true);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Set:", _lblStyle, GUILayout.Width(30f));
            _coinsField = GUILayout.TextField(_coinsField, _txtStyle);
            if (Btn("SET", 60f)) GameplayData.CoinsSet(ParseBig(_coinsField));
            GUILayout.EndHorizontal();
        }

        private void DrawTickets()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            if (Btn("+10")) GameplayData.CloverTicketsAdd(10, true);
            if (Btn("+100")) GameplayData.CloverTicketsAdd(100, true);
            if (Btn("+1000")) GameplayData.CloverTicketsAdd(1000, true);
            if (Btn("x2")) GameplayData.CloverTicketsAdd(GameplayData.CloverTicketsGet(), true);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Set:", _lblStyle, GUILayout.Width(30f));
            _ticketsField = GUILayout.TextField(_ticketsField, _txtStyle);
            if (Btn("SET", 60f)) GameplayData.CloverTicketsSet(ParseLong(_ticketsField));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (Btn("Bonus LittleBet +50")) GameplayData.CloverTickets_BonusLittleBet_Add(50);
            if (Btn("Bonus BigBet +50")) GameplayData.CloverTickets_BonusBigBet_Add(50);
            if (Btn("Bonus rounds +5")) GameplayData.CloverTickets_BonusRoundsLeft_Add(5);
            GUILayout.EndHorizontal();
        }

        private void DrawBank()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            if (Btn("Deposit +10K")) GameplayData.DepositAdd(10000);
            GUILayout.Label("Set:", _lblStyle, GUILayout.Width(30f));
            _depositField = GUILayout.TextField(_depositField, _txtStyle);
            if (Btn("SET", 50f)) GameplayData.DepositSet(ParseBig(_depositField));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Interest %:", _lblStyle, GUILayout.Width(70f));
            _interestField = GUILayout.TextField(_interestField, _txtStyle);
            if (Btn("SET", 50f)) GameplayData.InterestRateSet(Mathf.Clamp(ParseFloat(_interestField), 0f, 100f));
            if (Btn("+5%", 60f)) GameplayData.InterestRateAdd(5f);
            GUILayout.EndHorizontal();
        }

        private void DrawSpins()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            if (Btn("+1")) GameplayData.SpinsLeftAdd(1);
            if (Btn("+5")) GameplayData.SpinsLeftAdd(5);
            if (Btn("+10")) GameplayData.SpinsLeftAdd(10);
            if (Btn("+MAX")) GameplayData.SpinsLeftSet(GameplayData.MaxSpins_Get());
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Set:", _lblStyle, GUILayout.Width(30f));
            _spinsField = GUILayout.TextField(_spinsField, _txtStyle);
            if (Btn("SET", 50f)) GameplayData.SpinsLeftSet(ParseInt(_spinsField));
            if (Btn("MaxSpins +1", 90f)) GameplayData.MaxSpins_Add(1);
            if (Btn("Extra +5", 70f)) GameplayData.ExtraSpinsAdd(5);
            GUILayout.EndHorizontal();

            bool inf = GUILayout.Toggle(_infiniteSpins, "INFINITE SPINS (tops up every frame)");
            if (inf != _infiniteSpins)
            {
                _infiniteSpins = inf;
                if (!inf) Safe(() => GameplayData.SpinsLeftSet(GameplayData.MaxSpins_Get()), "reset spins");
            }
        }

        private void DrawLuck()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Luck value:", _lblStyle);
            _luckSlider = GUILayout.HorizontalSlider(_luckSlider, 1f, 100f);
            GUILayout.Label(_luckSlider.ToString("0.#") + "x", _lblStyle, GUILayout.Width(40f));
            GUILayout.EndHorizontal();

            float v = _luckSlider;
            GUILayout.BeginHorizontal();
            if (Btn("PowerupLuck =")) GameplayData.PowerupLuckSet(v);
            if (Btn("ActivationLuck =")) GameplayData.ActivationLuckSet(v);
            if (Btn("StoreLuck =")) GameplayData.StoreLuckSet(v);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (Btn("ExtraLuck " + v.ToString("0.#") + "x for 999 spins"))
                GameplayData.ExtraLuck_SetEntry("clovercheat", v, 999, true);
            if (Btn("RESET ALL LUCK"))
            {
                GameplayData.PowerupLuckReset();
                GameplayData.ActivationLuckReset();
                GameplayData.StoreLuckReset();
                GameplayData.ExtraLuck_ResetAll();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawMultipliers()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            if (Btn("Symbols x10")) GameplayData.AllSymbolsMultiplierAdd(GameplayData.AllSymbolsMultiplierGet(true) * 9);
            if (Btn("Symbols x100")) GameplayData.AllSymbolsMultiplierAdd(GameplayData.AllSymbolsMultiplierGet(true) * 99);
            GUILayout.Label("Set:", _lblStyle, GUILayout.Width(30f));
            _symMultField = GUILayout.TextField(_symMultField, _txtStyle);
            if (Btn("SET", 45f)) GameplayData.AllSymbolsMultiplierSet(ParseBig(_symMultField));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (Btn("Patterns x10")) GameplayData.AllPatternsMultiplierAdd(GameplayData.AllPatternsMultiplierGet(true) * 9);
            if (Btn("Patterns x100")) GameplayData.AllPatternsMultiplierAdd(GameplayData.AllPatternsMultiplierGet(true) * 99);
            GUILayout.Label("Set:", _lblStyle, GUILayout.Width(30f));
            _patMultField = GUILayout.TextField(_patMultField, _txtStyle);
            if (Btn("SET", 45f)) GameplayData.AllPatternsMultiplierSet(ParseBig(_patMultField));
            GUILayout.EndHorizontal();

            if (Btn("RESET both multipliers"))
            {
                GameplayData.allSymbolsMultiplierReset();
                GameplayData.AllPatternsMultiplierReset();
            }
        }

        private void DrawSymbols()
        {
            RowReset();

            GUILayout.BeginHorizontal();
            for (int i = 0; i < SymbolKinds.Length; i++)
            {
                bool sel = i == _selectedSymbol;
                string label = (sel ? "[ " : "") + SymbolNames[i] + (sel ? " ]" : "");
                GUIStyle st = sel ? _headStyle : _btnStyle;
                if (sel)
                {
                    Color c = GUI.backgroundColor;
                    GUI.backgroundColor = Color.yellow;
                    if (GUILayout.Button(label, st)) _selectedSymbol = i;
                    GUI.backgroundColor = c;
                }
                else if (GUILayout.Button(label, st)) _selectedSymbol = i;
            }
            GUILayout.EndHorizontal();

            SymbolScript.Kind k = SymbolKinds[_selectedSymbol];

            GUILayout.BeginHorizontal();
            if (Btn("Chance 99%"))
            {
                GameplayData.Symbol_Chance_Set(k, 99f);
            }
            if (Btn("Golden 100%")) GameplayData.Symbol_ModifierChance_Set(k, SymbolScript.Modifier.golden, 1f);
            if (Btn("Instant reward 100%")) GameplayData.Symbol_ModifierChance_Set(k, SymbolScript.Modifier.instantReward, 1f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (Btn("CloverTicket 100%")) GameplayData.Symbol_ModifierChance_Set(k, SymbolScript.Modifier.cloverTicket, 1f);
            if (Btn("Chain 100%")) GameplayData.Symbol_ModifierChance_Set(k, SymbolScript.Modifier.chain, 1f);
            if (Btn("ValueExtra =")) GameplayData.Symbol_CoinsValueExtra_Set(k, ParseBig(_symValueField));
            GUILayout.Label("val:", _lblStyle, GUILayout.Width(28f));
            _symValueField = GUILayout.TextField(_symValueField, _txtStyle);
            GUILayout.EndHorizontal();

            GUILayout.Space(4f);
            GUILayout.Label("<color=#ffd700>Apply to ALL symbols:</color>", _lblStyle);
            GUILayout.BeginHorizontal();
            if (Btn("Chance 99% ALL"))
            {
                foreach (SymbolScript.Kind kk in SymbolKinds) GameplayData.Symbol_Chance_Set(kk, 99f);
            }
            if (Btn("Golden ALL"))
            {
                foreach (SymbolScript.Kind kk in SymbolKinds) GameplayData.Symbol_ModifierChance_Set(kk, SymbolScript.Modifier.golden, 1f);
            }
            if (Btn("Instant ALL"))
            {
                foreach (SymbolScript.Kind kk in SymbolKinds) GameplayData.Symbol_ModifierChance_Set(kk, SymbolScript.Modifier.instantReward, 1f);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (Btn("Ticket ALL"))
            {
                foreach (SymbolScript.Kind kk in SymbolKinds) GameplayData.Symbol_ModifierChance_Set(kk, SymbolScript.Modifier.cloverTicket, 1f);
            }
            if (Btn("ValueExtra = (ALL)")) 
            {
                BigInteger v = ParseBig(_symValueField);
                foreach (SymbolScript.Kind kk in SymbolKinds) GameplayData.Symbol_CoinsValueExtra_Set(kk, v);
            }
            if (Btn("Reset ALL modifiers"))
            {
                foreach (SymbolScript.Kind kk in SymbolKinds)
                    foreach (SymbolScript.Modifier m in UsefulModifiers)
                        GameplayData.Symbol_ModifierChance_Set(kk, m, 0f);
            }
            GUILayout.EndHorizontal();
        }

        private void DrawPatterns()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Pattern value:", _lblStyle, GUILayout.Width(85f));
            _patValueField = GUILayout.TextField(_patValueField, _txtStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            double v = ParseDouble(_patValueField);
            if (Btn("ALL += value"))
            {
                foreach (PatternScript.Kind p in PatternKinds()) GameplayData.Pattern_ValueExtra_Add(p, v);
            }
            if (Btn("ALL = value"))
            {
                foreach (PatternScript.Kind p in PatternKinds()) GameplayData.Pattern_ValueExtra_Set(p, v);
            }
            GUILayout.EndHorizontal();
        }

        private static System.Collections.Generic.IEnumerable<PatternScript.Kind> PatternKinds()
        {
            yield return PatternScript.Kind.jackpot;
            yield return PatternScript.Kind.horizontal2;
            yield return PatternScript.Kind.horizontal3;
            yield return PatternScript.Kind.horizontal4;
            yield return PatternScript.Kind.horizontal5;
            yield return PatternScript.Kind.vertical2;
            yield return PatternScript.Kind.vertical3;
            yield return PatternScript.Kind.diagonal2;
            yield return PatternScript.Kind.diagonal3;
            yield return PatternScript.Kind.pyramid;
            yield return PatternScript.Kind.pyramidInverted;
            yield return PatternScript.Kind.triangle;
            yield return PatternScript.Kind.triangleInverted;
            yield return PatternScript.Kind.snakeUpDown;
            yield return PatternScript.Kind.snakeDownUp;
            yield return PatternScript.Kind.eye;
        }

        private void Draw666()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            if (Btn("Suppress 666 for +10 spins")) GameplayData.SixSixSix_SuppressedSpinsSet(GameplayData.SixSixSix_SuppressedSpinsGet() + 10);
            if (Btn("Force 666 on NEXT spin"))
                GameplayData.SixSixSix_BookedSpinSet(Math.Max(0, GameplayData.SpinsLeftGet() - 1));
            GUILayout.EndHorizontal();
        }

        private void DrawStore()
        {
            RowReset();
            GUILayout.BeginHorizontal();
            if (Btn("Free restocks +1")) GameplayData.StoreFreeRestocksSet(GameplayData.StoreFreeRestocksGet() + 1);
            if (Btn("Store discount 100%")) GameplayData.StoreTemporaryDiscountSet(100, true);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (Btn("Max equip powerups +1")) GameplayData.MaxEquippablePowerupsAdd(1);
            if (Btn("RedButton mult +1")) GameplayData.RedButtonActivationsMultiplierAdd(1);
            GUILayout.EndHorizontal();
        }

        private void DrawSave()
        {
            RowReset();
            GUILayout.Space(4f);
            if (Btn("SAVE GAME NOW", 140f))
            {
                bool ok = false;
                try { ok = Panik.Data.SaveGame(Panik.Data.GameSavingReason.debug).GetAwaiter().IsCompleted; }
                catch (Exception e) { Log.LogWarning("Save failed: " + e.Message); }
                _saveStatus = "Saving... (async, check later)";
                Log.LogInfo("SaveGame requested, task completed instantly: " + ok);
            }
            GUILayout.Label(_saveStatus, _lblStyle);
        }

        private string _saveStatus = "";

        // ================= helpers =================

        private bool Btn(string label, float width = 0f)
        {
            return width > 0f ? GUILayout.Button(label, _btnStyle, GUILayout.Width(width)) : GUILayout.Button(label, _btnStyle);
        }

        private static void Safe(Action a, string what)
        {
            try { a(); }
            catch (Exception e) { Log.LogWarning(what + " failed: " + e.Message); }
        }

        private static string FormatBig(BigInteger v)
        {
            string s = v.ToString();
            if (s.Length > 15) return s.Substring(0, 12) + "e+" + (s.Length - 1);
            return s;
        }

        private static BigInteger ParseBig(string s)
        {
            BigInteger v;
            if (BigInteger.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
            return 0;
        }

        private static long ParseLong(string s)
        {
            long v;
            return long.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static int ParseInt(string s)
        {
            int v;
            return int.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static float ParseFloat(string s)
        {
            float v;
            return float.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0f;
        }

        private static double ParseDouble(string s)
        {
            double v;
            return double.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v) ? v : 0.0;
        }
    }

    // Optional: make the game think it's unmodded (hides the MODDED badge)
    [HarmonyPatch(typeof(Panik.Master), "IsModded")]
    internal static class Master_IsModded_Patch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!CloverCheatPlugin.HideModdedBadge.Value) return true;
            __result = false;
            return false;
        }
    }
}
