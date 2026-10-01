namespace BeastKeeper;

public enum AssignMode
{
    /// <summary>Re-use the familiars assigned before the previous battle, in the same order.</summary>
    RememberLast,

    /// <summary>Assign the first three available familiars in the Team Composition list.</summary>
    FirstInRoster,
}
