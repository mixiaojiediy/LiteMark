namespace LiteMarkWin.Models;

internal sealed record MarkupShape(
    MarkupMode Mode,
    int X1,
    int Y1,
    int X2,
    int Y2,
    int Thickness
);
