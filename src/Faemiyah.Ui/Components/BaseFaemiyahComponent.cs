using Microsoft.AspNetCore.Components;

namespace Faemiyah.Ui.Components;

/// <summary>
/// Basic Faemiyah component utility methods.
/// </summary>
public class BaseFaemiyahComponent : ComponentBase
{
    /// <summary>
    /// Requests a re-render on the renderer's synchronization context.
    /// </summary>
    protected void InvokeStateChange()
    {
        _ = InvokeAsync(StateHasChanged);
    }
}
