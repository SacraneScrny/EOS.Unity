using UnityEngine;

namespace EOS.Unity
{
    /// <summary>Marks a string field as an incarnation id so the inspector draws a dropdown of ids from <c>incarnations.json</c> next to the text field (same picker as the <see cref="EntityPreset"/> inspector).</summary>
    public sealed class IncarnationIdAttribute : PropertyAttribute { }
}
