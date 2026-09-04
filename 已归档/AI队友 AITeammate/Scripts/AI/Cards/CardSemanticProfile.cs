using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class CardSemanticProfile
{
    public IReadOnlyList<NormalizedEffectDescriptor> Effects { get; init; } = [];
}
