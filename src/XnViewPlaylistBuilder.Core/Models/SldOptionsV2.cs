namespace XnViewPlaylistBuilder.Core.Models;

public sealed class SldOptionsV2
{
    public bool UseTimer { get; set; } = true;
    public int Timer { get; set; } = 15;
    public bool Loop { get; set; } = true;
    public bool FullScreen { get; set; } = true;
    public int WinWidth { get; set; } = 640;
    public int WinHeight { get; set; } = 480;
    public bool Stretch { get; set; } = true;
    public bool RandomOrder { get; set; } = true;
    public bool ShowInfo { get; set; } = true;
    public string Info { get; set; } = SldInfoTokens.DefaultTemplate;
    public bool TitleBar { get; set; }
    public bool OnTop { get; set; }
    public bool CursorAutoHide { get; set; }
    public RgbaColor BackgroundColor { get; set; } = RgbaColor.Black;
    public RgbaColor TextColor { get; set; } = RgbaColor.White;
    public bool UseTextBackColor { get; set; }
    public int TextPosition { get; set; } = SldTextPosition.Default;
    public RgbaColor TextBackColor { get; set; } = RgbaColor.Gray128;
    public int Opacity { get; set; } = 100;
    public string Font { get; set; } = "MS Shell Dlg 2,8.25,-1,5,50,0,0,0,0,0";
    public int EffectDuration { get; set; } = 1000;
    public IReadOnlyList<int> Effects { get; set; } = Enumerable.Range(1, 56).ToArray();

    public static SldOptionsV2 CreateDefaults() => new();

    public void NormalizeForWrite()
    {
        Info = SldInfoTokens.NormalizeTemplate(Info);
        TextPosition = SldTextPosition.Normalize(TextPosition);
        Opacity = Math.Clamp(Opacity, 0, 100);
        Timer = Math.Max(1, Timer);
        WinWidth = Math.Max(1, WinWidth);
        WinHeight = Math.Max(1, WinHeight);
        EffectDuration = Math.Max(0, EffectDuration);

        if (Effects.Count == 0)
        {
            Effects = SldEffects.All;
        }
    }
}
