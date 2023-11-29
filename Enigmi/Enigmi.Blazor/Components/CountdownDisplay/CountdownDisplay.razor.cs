using Microsoft.AspNetCore.Components;

namespace Enigmi.Blazor.Components.CountdownDisplay;

public partial class CountdownDisplay
{
    private int _fullDashArray = 283;
    
    [Parameter, EditorRequired] 
    public int SecondsLeft { get; set; }
    
    [Parameter, EditorRequired] 
    public int TotalSeconds { get; set; }

    private string GetCircleDashArrayValue()
    {
        var calculatedValue = Math.Round(GetPercentage() * _fullDashArray, 0);
        return $"{calculatedValue} {_fullDashArray}";
    }

    private decimal GetPercentage()
    {
        if (TotalSeconds == 0)
        {
            return 0;
        }

        return Convert.ToDecimal(SecondsLeft) / Convert.ToDecimal(TotalSeconds);
    }

    private string GetClassColour()
    {
        var percentage = GetPercentage();
        if (percentage > 0.8M)
        {
            return "green";
        }
        else if (percentage > 0.3M)
        {
            return "orange";
        }
        else
        {
            return "red";
        }
    }
}