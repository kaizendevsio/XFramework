using Microsoft.AspNetCore.Components.Web;

namespace XFramework.Blazor.Components.Buttons;

public class XButtonBase : ComponentBase
{
    /// <summary>The title of the button.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>The variant to use.</summary>
    [Parameter]
    [Category("Appearance")]
    public Variant Variant { get; set; } = Variant.Filled;

    /// <summary>Child content of component.</summary>
    [Parameter]
    [Category("Behavior")]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>Button click event.</summary>
    [Parameter]
    public EventCallback<MouseEventArgs> OnClick { get; set; }
    
    /// <summary>Icon placed before the text if set.</summary>
    [Parameter]
    [Category("Behavior")]
    public string StartIcon { get; set; }

    /// <summary>Icon placed after the text if set.</summary>
    [Parameter]
    [Category("Behavior")]
    public string EndIcon { get; set; }

    /// <summary>The color of the icon. It supports the theme colors.</summary>
    [Parameter]
    [Category("Appearance")]
    public Color IconColor { get; set; } = Color.Inherit;

    /// <summary>
    /// The size of the icon. When null, the value of Size is used.
    /// </summary>
    [Parameter]
    [Category("Appearance")]
    public Size IconSize { get; set; }

    /// <summary>Icon class names, separated by space</summary>
    [Parameter]
    [Category("Appearance")]
    public string IconClass { get; set; }

    /// <summary>The Size of the component.</summary>
    [Parameter]
    [Category("Appearance")]
    public Size Size { get; set; } = Size.Medium;

    /// <summary>The button Type (Button, Submit, Refresh)</summary>
    [Parameter]
    [Category("Click action")]
    public ButtonType ButtonType { get; set; } = ButtonType.Button;
    
    /// <summary>
    /// If true, the button will take up 100% of available width.
    /// </summary>
    [Parameter]
    [Category("Appearance")]
    public bool FullWidth { get; set; }
    
    /// <summary>
    /// Pass class name to button
    /// </summary>
    [Parameter]
    public string? Class { get; set; }
    
    /// <summary>
    /// User styles, applied on top of the component's own classes and styles.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.ComponentBase.Common)]
    public string? Style { get; set; }
    
    /// <summary>
    /// Set the text-align on the component.
    /// </summary>
    [Parameter]
    [Category(CategoryTypes.Text.Appearance)]
    public Align Align { get; set; } = Align.Inherit;

    /// <summary>If true, the button will be disabled.</summary>
    [Parameter]
    [Category("Behavior")]
    public bool Disabled { get; set; }

    /// <summary>If true, no drop-shadow will be used.</summary>
    [Parameter]
    [Category("Appearance")]
    public bool DisableElevation { get; set; }

    /// <summary>If true, disables ripple effect.</summary>
    [Parameter]
    [Category("Appearance")]
    public bool DisableRipple { get; set; }
}