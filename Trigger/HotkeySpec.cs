namespace FlashGrab.Trigger;

/// <summary>
/// 一組全域快捷鍵(修飾鍵 + 主鍵)。以 "Win+Shift+C" 這種字串存進 settings.json,
/// 使用者可直接讀、直接改;解析失敗一律視為「未設定」而不是丟例外。
/// </summary>
internal readonly record struct HotkeySpec(ModifierKeys Modifiers, Keys Key)
{
    public static readonly HotkeySpec Default = new(ModifierKeys.Win | ModifierKeys.Shift, Keys.C);

    /// <summary>
    /// 主預設鍵被其他程式佔走時,依序嘗試的備用鍵。刻意避開常見系統/輸入法組合;
    /// 備用鍵只在本次執行生效,不會寫進設定檔。
    /// </summary>
    public static readonly IReadOnlyList<HotkeySpec> Fallbacks = new[]
    {
        new HotkeySpec(ModifierKeys.Win | ModifierKeys.Shift, Keys.X),
        new HotkeySpec(ModifierKeys.Control | ModifierKeys.Alt, Keys.C),
        new HotkeySpec(ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift, Keys.C),
    };

    /// <summary>設定視窗主鍵下拉清單提供的鍵:A–Z、0–9、F1–F12。</summary>
    public static IEnumerable<Keys> SelectableKeys()
    {
        for (Keys k = Keys.A; k <= Keys.Z; k++)
        {
            yield return k;
        }

        for (Keys k = Keys.D0; k <= Keys.D9; k++)
        {
            yield return k;
        }

        for (Keys k = Keys.F1; k <= Keys.F12; k++)
        {
            yield return k;
        }
    }

    /// <summary>至少要有一個修飾鍵(單獨字母鍵當全域快捷鍵會吃掉一般打字),且主鍵在可選範圍內。</summary>
    public bool IsValid => Modifiers != ModifierKeys.None && SelectableKeys().Contains(Key);

    public static string KeyLabel(Keys key) =>
        key is >= Keys.D0 and <= Keys.D9 ? ((int)key - (int)Keys.D0).ToString() : key.ToString();

    /// <summary>存檔格式,如 "Ctrl+Alt+C"。</summary>
    public override string ToString() => Join("+");

    /// <summary>給人看的格式,如 "Win + Shift + C"。</summary>
    public string ToDisplay() => Join(" + ");

    private string Join(string sep)
    {
        var parts = new List<string>(5);
        // Windows 慣用順序:Win 在前(與既有顯示「Win + Shift + C」一致)。
        if (Modifiers.HasFlag(ModifierKeys.Win)) parts.Add("Win");
        if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        parts.Add(KeyLabel(Key));
        return string.Join(sep, parts);
    }

    /// <summary>解析設定檔字串;空值、拼錯、沒有修飾鍵、主鍵不在可選範圍都回 false。</summary>
    public static bool TryParse(string? text, out HotkeySpec spec)
    {
        spec = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] tokens = text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
        {
            return false;
        }

        var mods = ModifierKeys.None;
        foreach (string t in tokens[..^1])
        {
            switch (t.ToLowerInvariant())
            {
                case "ctrl" or "control": mods |= ModifierKeys.Control; break;
                case "alt": mods |= ModifierKeys.Alt; break;
                case "shift": mods |= ModifierKeys.Shift; break;
                case "win" or "windows": mods |= ModifierKeys.Win; break;
                default: return false;
            }
        }

        string keyToken = tokens[^1];
        if (keyToken.Length == 1 && char.IsDigit(keyToken[0]))
        {
            keyToken = "D" + keyToken;
        }

        if (!Enum.TryParse(keyToken, ignoreCase: true, out Keys key))
        {
            return false;
        }

        var candidate = new HotkeySpec(mods, key);
        if (!candidate.IsValid)
        {
            return false;
        }

        spec = candidate;
        return true;
    }

    /// <summary>設定檔字串 → 生效的快捷鍵;無法解析時回預設值。</summary>
    public static HotkeySpec FromSettingOrDefault(string? text) =>
        TryParse(text, out var spec) ? spec : Default;
}
