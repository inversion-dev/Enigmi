using Microsoft.AspNetCore.Components;

namespace Enigmi.Blazor.Components.Spinner;

public partial class Spinner
{
    [Parameter] 
    public bool IsLoading { get; set; }
}