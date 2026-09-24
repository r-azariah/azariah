using Avalonia.Media;

namespace Azariah.App;

/// <summary>
/// Brand geometry. <c>[A]</c> is the mark (icon, compact spots); <c>[AZARIAH]</c> is the wordmark.
/// The mark is traced from the owner's logo. The wordmark's letters are Montserrat Bold outlines
/// (OFL) converted to paths, so no font ships; its brackets use the logo's proportions.
/// </summary>
public static class Brand
{
    public const double MarkWidth = 541;
    public const double MarkHeight = 422;
    public const string MarkLeftData = "M0,0 H110 V50 H56 V372 H110 V422 H0 Z";
    public const string MarkAData = "F0 M218,50 H311 L434,351 H360 L338,291 H200 L178,351 H104 Z M270,117 L317,237 H222 Z";
    public const string MarkRightData = "M541,0 H431 V50 H485 V372 H431 V422 H541 Z";

    public const double WordWidth = 5620.9;
    public const double WordHeight = 981.4;
    public const string WordLeftData = "M0,0 L255.8,0 L255.8,116.3 L130.2,116.3 L130.2,865.1 L255.8,865.1 L255.8,981.4 L0,981.4 Z";
    public const string WordTextData = "F1 M 367.4 816.3 L 679.4 116.3 H 839.4 L 1152.4 816.3 H 982.4 L 726.4 198.3 H 790.4 L 533.4 816.3 Z M 523.4 666.3 L 566.4 543.3 H 926.4 L 970.4 666.3 Z M 1200.4 816.3 V 711.3 L 1633.4 189.3 L 1653.4 248.3 H 1208.4 V 116.3 H 1797.4 V 221.3 L 1365.4 743.3 L 1345.4 684.3 H 1812.4 V 816.3 Z M 1844.4 816.3 L 2156.4 116.3 H 2316.4 L 2629.4 816.3 H 2459.4 L 2203.4 198.3 H 2267.4 L 2010.4 816.3 Z M 2000.4 666.3 L 2043.4 543.3 H 2403.4 L 2447.4 666.3 Z M 2722.4 816.3 V 116.3 H 3021.4 Q 3167.4 116.3 3248.4 183.8 Q 3329.4 251.3 3329.4 370.3 Q 3329.4 448.3 3292.4 504.8 Q 3255.4 561.3 3187.4 591.3 Q 3119.4 621.3 3025.4 621.3 H 2812.4 L 2884.4 550.3 V 816.3 Z M 3167.4 816.3 L 2992.4 562.3 H 3165.4 L 3342.4 816.3 Z M 2884.4 568.3 L 2812.4 492.3 H 3016.4 Q 3091.4 492.3 3128.4 459.8 Q 3165.4 427.3 3165.4 370.3 Q 3165.4 312.3 3128.4 280.3 Q 3091.4 248.3 3016.4 248.3 H 2812.4 L 2884.4 171.3 Z M 3477.4 816.3 V 116.3 H 3639.4 V 816.3 Z M 3733.4 816.3 L 4045.4 116.3 H 4205.4 L 4518.4 816.3 H 4348.4 L 4092.4 198.3 H 4156.4 L 3899.4 816.3 Z M 3889.4 666.3 L 3932.4 543.3 H 4292.4 L 4336.4 666.3 Z M 5091.4 816.3 V 116.3 H 5253.4 V 816.3 Z M 4611.4 816.3 V 116.3 H 4773.4 V 816.3 Z M 4761.4 529.3 V 392.3 H 5103.4 V 529.3 Z";
    public const string WordRightData = "M5620.9,0 L5365.1,0 L5365.1,116.3 L5490.7,116.3 L5490.7,865.1 L5365.1,865.1 L5365.1,981.4 L5620.9,981.4 Z";

    public static Geometry Mark { get; } = Geometry.Parse(
        "F0 " + MarkLeftData + " " + MarkAData[3..] + " " + MarkRightData);

    public static Geometry Wordmark { get; } = Geometry.Parse(
        "F1 " + WordLeftData + " " + WordTextData[3..] + " " + WordRightData);

    public const string Name = "[AZARIAH]";
}
