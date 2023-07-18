using Microsoft.AspNetCore.Components;
using Enigmi.Blazor.Shared.Models;

namespace Enigmi.Blazor.Components.ViewLeftoverPuzzlePieces;

public partial class ViewLeftoverPuzzlePieces
{
    [Parameter]
    public bool Visible { get; set; } = false;

    [Parameter]
    public List<UserPuzzle> LeftoverPuzzles { get; set; } = new();
}
