using JetBrains.Annotations;

namespace Techardry.UI.Interfaces;

public interface IBorderElement
{
    [UsedImplicitly] public bool BorderActive { get; set; }
    [UsedImplicitly] public float BorderWidth { get; set; }
}