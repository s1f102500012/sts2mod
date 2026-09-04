using System;
using System.Collections.Generic;

namespace AITeammate.Scripts;

internal sealed class CombatCardStateStore
{
    private readonly Dictionary<string, CardStateOverlay> _overlays = new(StringComparer.Ordinal);

    public CardStateOverlay GetOrCreate(string cardInstanceId)
    {
        if (!_overlays.TryGetValue(cardInstanceId, out CardStateOverlay? overlay))
        {
            overlay = new CardStateOverlay();
            _overlays[cardInstanceId] = overlay;
        }

        return overlay;
    }

    public bool TryGet(string cardInstanceId, out CardStateOverlay? overlay)
    {
        return _overlays.TryGetValue(cardInstanceId, out overlay);
    }

    public void Clear(string cardInstanceId)
    {
        _overlays.Remove(cardInstanceId);
    }

    public void ClearAll()
    {
        _overlays.Clear();
    }
}
