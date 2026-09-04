using System;
using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class CardDefinitionRepository
{
    private readonly Dictionary<string, CardDefinition> _definitions = new(StringComparer.Ordinal);

    public bool TryGet(string cardId, out CardDefinition? definition)
    {
        return _definitions.TryGetValue(cardId, out definition);
    }

    public void Upsert(CardDefinition definition)
    {
        _definitions[definition.CardId] = definition;
    }
}
