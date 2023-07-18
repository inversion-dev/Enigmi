using Microsoft.AspNetCore.Components;

namespace Enigmi.Blazor.Components.IconButton;

public enum IconPlacement { Left, Right, Top, Bottom }
public enum TextSize { Default, Small, Medium, Large, ExtraLarge }

public partial class IconButton
{
	[Parameter]
	public string? Text { get; set; }

	[Parameter]
	public string? IconUrl { get; set; }

	[Parameter]
	public IconPlacement IconPlacement { get; set; } = IconPlacement.Left;

	[Parameter]
	public TextSize TextSize { get; set; } = TextSize.Default;

	[Parameter]
	public bool Enabled { get; set; } = true;

    [Parameter]
    public Func<Task> ClickAction { get; set; } = null!;

    // TODO: move to css...
    private string LayoutStyle => IconPlacement switch
	{
		IconPlacement.Left => "flex-direction:row;",
		IconPlacement.Right => "flex-direction:row-reverse;",
		IconPlacement.Top => "flex-direction:column;",
		IconPlacement.Bottom => "flex-direction:column-reverse;",
		_ => "",
	};

	private string TextStyle => TextSize switch
	{
		TextSize.Small => "font-size: 10px",
		TextSize.Medium => "font-size: 14px",
		TextSize.Large => "font-size: 22px; font-weight:bold;",
		TextSize.ExtraLarge => "font-size: 28px; font-weight:bold;",
		_ => "",
	};
}