using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace AITeammate.Scripts;

internal readonly record struct AiTeammatePlaceholderCharacter(
    string Id,
    string DisplayName,
    string TexturePath,
    string ModelId)
{
    public CharacterModel ResolveModel()
    {
        return Id switch
        {
            "ironclad" => ModelDb.Character<Ironclad>(),
            "silent" => ModelDb.Character<Silent>(),
            "defect" => ModelDb.Character<Defect>(),
            "regent" => ModelDb.Character<Regent>(),
            "necrobinder" => ModelDb.Character<Necrobinder>(),
            _ => throw new InvalidOperationException($"Unknown AI teammate character id: {Id}")
        };
    }
}

internal static class AiTeammatePlaceholderCharacters
{
    private static readonly Dictionary<string, AiTeammatePlaceholderCharacter> CharactersById = new(StringComparer.Ordinal);
    public static readonly AiTeammatePlaceholderCharacter[] All =
    {
        new("ironclad", "Ironclad", "res://images/packed/character_select/char_select_ironclad.png", "IRONCLAD"),
        new("silent", "Silent", "res://images/packed/character_select/char_select_silent.png", "SILENT"),
        new("defect", "Defect", "res://images/packed/character_select/char_select_defect.png", "DEFECT"),
        new("regent", "Regent", "res://images/packed/character_select/char_select_regent.png", "REGENT"),
        new("necrobinder", "Necrobinder", "res://images/packed/character_select/char_select_necrobinder.png", "NECROBINDER")
    };

    static AiTeammatePlaceholderCharacters()
    {
        foreach (AiTeammatePlaceholderCharacter character in All)
        {
            CharactersById[character.Id] = character;
        }
    }

    public static bool TryGetById(string id, out AiTeammatePlaceholderCharacter character)
    {
        return CharactersById.TryGetValue(id, out character);
    }

    public static bool TryGetByModelId(string? modelId, out AiTeammatePlaceholderCharacter character)
    {
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            foreach (AiTeammatePlaceholderCharacter option in All)
            {
                if (string.Equals(option.ModelId, modelId, StringComparison.OrdinalIgnoreCase))
                {
                    character = option;
                    return true;
                }
            }
        }

        character = default;
        return false;
    }

    public static Texture2D? LoadTexture(string texturePath)
    {
        // These UI screens are recreated across submenu teardown/reopen cycles.
        // Returning a fresh resource here avoids reusing a disposed Godot texture instance.
        return ResourceLoader.Load<Texture2D>(texturePath);
    }
}
