using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace YuzuhaToolkit.Desktop;

/// <summary>Workspace canvas; isolated rendering can ignore only its native host layout clip.</summary>
public sealed class WorkspaceSurface : Grid
{
    /// <summary>Enabled only by isolated smoke rendering; child controls retain their own clipping and scrolling.</summary>
    internal bool FixedViewport { get; set; }

    /// <summary>Keep normal window clipping in production; render the full explicit canvas for screenshot verification.</summary>
    /// <param name="layoutSlotSize">Available native host slot, which may be smaller than the requested test canvas.</param>
    /// <returns>The normal layout clip, or no host clip for an explicitly sized smoke canvas.</returns>
    protected override Geometry? GetLayoutClip(Size layoutSlotSize) => FixedViewport ? null : base.GetLayoutClip(layoutSlotSize);
}
