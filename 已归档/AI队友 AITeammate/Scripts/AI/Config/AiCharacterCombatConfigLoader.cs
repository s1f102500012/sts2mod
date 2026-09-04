using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Logging;
using GodotFileAccess = Godot.FileAccess;

namespace AITeammate.Scripts;

internal static class AiCharacterCombatConfigLoader
{
    private const string ConfigFileExtension = ".aiconfig";
    private static readonly object Sync = new();
    private static readonly Dictionary<string, AiCharacterCombatConfig> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    public static AiCharacterCombatConfig LoadForPlayer(Player player)
    {
        string characterId = ResolveCharacterId(player);
        string displayName = ResolveDisplayName(characterId);
        return LoadForCharacter(characterId, displayName);
    }

    public static string GetConfigDirectoryPath()
    {
        return Path.Combine(GetModRootPath(), "config", "ai-behavior");
    }

    private static AiCharacterCombatConfig LoadForCharacter(string characterId, string displayName)
    {
        lock (Sync)
        {
            if (Cache.TryGetValue(characterId, out AiCharacterCombatConfig? cached))
            {
                return cached;
            }

            AiCharacterCombatConfig baseConfig = ReadMergedConfig(
                GetConfigFilePath(AiCharacterCombatConfig.DefaultCharacterId),
                AiCharacterCombatConfig.CreateBuiltInDefault());

            AiCharacterCombatConfig resolved = characterId.Equals(AiCharacterCombatConfig.DefaultCharacterId, StringComparison.OrdinalIgnoreCase)
                ? baseConfig
                : ReadMergedConfig(
                    GetConfigFilePath(characterId),
                    baseConfig,
                    characterId,
                    displayName);

            Cache[characterId] = resolved;
            Log.Info(
                $"[AITeammate][Config] Loaded combat config character={resolved.CharacterId} survival={resolved.Combat.RiskProfile.SurvivalWeight:F2} defense={resolved.Combat.RiskProfile.DefenseWeight:F2} attack={resolved.Combat.RiskProfile.AttackWeight:F2} aggressiveness={resolved.Combat.RiskProfile.Aggressiveness:F2} lethalBonus={resolved.Combat.RiskProfile.LethalPriorityBonus} draw={resolved.Combat.ResourceWeights.DrawValueWhenPlayable} energy={resolved.Combat.ResourceWeights.EnergyGainValue}");
            return resolved;
        }
    }

    private static AiCharacterCombatConfig ReadMergedConfig(
        string path,
        AiCharacterCombatConfig fallback,
        string? expectedCharacterId = null,
        string? expectedDisplayName = null)
    {
        AiCharacterCombatConfigFile? file = TryReadConfigFile(path) ??
                                           TryReadResourceConfigFile(Path.GetFileName(path));
        return MergeWithFallback(file, fallback, expectedCharacterId, expectedDisplayName);
    }

    private static AiCharacterCombatConfig MergeWithFallback(
        AiCharacterCombatConfigFile? file,
        AiCharacterCombatConfig fallback,
        string? expectedCharacterId,
        string? expectedDisplayName)
    {
        int schemaVersion = file?.SchemaVersion is > 0
            ? file.SchemaVersion.Value
            : fallback.SchemaVersion;
        string characterId = NormalizeCharacterId(expectedCharacterId ?? file?.CharacterId ?? fallback.CharacterId);
        string displayName = expectedDisplayName
            ?? file?.DisplayName
            ?? fallback.DisplayName;

        return new AiCharacterCombatConfig
        {
            SchemaVersion = schemaVersion,
            CharacterId = characterId,
            DisplayName = displayName,
            Combat = fallback.Combat.Merge(file?.Combat),
            CardRewards = fallback.CardRewards.Merge(file?.CardRewards),
            Potions = fallback.Potions.Merge(file?.Potions),
            Shop = fallback.Shop.Merge(file?.Shop),
            Events = fallback.Events.Merge(file?.Events)
        };
    }

    private static AiCharacterCombatConfigFile? TryReadConfigFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Log.Warn($"[AITeammate][Config] Missing config file at {path}; using defaults.");
                return null;
            }

            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                Log.Warn($"[AITeammate][Config] Config file was empty at {path}; using defaults.");
                return null;
            }

            return JsonSerializer.Deserialize<AiCharacterCombatConfigFile>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate][Config] Failed to read config file at {path}; using defaults. {ex.Message}");
            return null;
        }
    }

    private static void EnsureConfigFilesExist()
    {
        string configDirectory = GetConfigDirectoryPath();
        Directory.CreateDirectory(configDirectory);

        foreach (AiCharacterCombatConfig builtIn in AiCharacterCombatConfigCatalog.BuiltInFiles)
        {
            string path = GetConfigFilePath(builtIn.CharacterId);
            if (File.Exists(path))
            {
                continue;
            }

            File.WriteAllText(path, JsonSerializer.Serialize(builtIn, JsonOptions));
            Log.Info($"[AITeammate][Config] Wrote default config file at {path}.");
        }
    }

    private static AiCharacterCombatConfigFile? TryReadResourceConfigFile(string fileName)
    {
        try
        {
            string path = $"res://sts2AITeammate/config/ai-behavior/{fileName}";
            if (!GodotFileAccess.FileExists(path))
            {
                Log.Warn($"[AITeammate][Config] Missing PCK config file at {path}; using defaults.");
                return null;
            }

            using GodotFileAccess file = GodotFileAccess.Open(path, GodotFileAccess.ModeFlags.Read);
            string json = file.GetAsText();
            if (string.IsNullOrWhiteSpace(json))
            {
                Log.Warn($"[AITeammate][Config] PCK config file was empty at {path}; using defaults.");
                return null;
            }

            return JsonSerializer.Deserialize<AiCharacterCombatConfigFile>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate][Config] Failed to read PCK config file {fileName}; using defaults. {ex.Message}");
            return null;
        }
    }

    private static string GetConfigFilePath(string characterId)
    {
        return Path.Combine(GetConfigDirectoryPath(), $"{NormalizeCharacterId(characterId)}{ConfigFileExtension}");
    }

    private static string ResolveCharacterId(Player player)
    {
        if (AiTeammateSessionRegistry.TryGetParticipant(player.NetId, out AiTeammateSessionParticipant participant) &&
            AiTeammatePlaceholderCharacters.TryGetByModelId(participant.Character.Id.Entry, out AiTeammatePlaceholderCharacter placeholder))
        {
            return placeholder.Id;
        }

        return AiCharacterCombatConfig.DefaultCharacterId;
    }

    private static string ResolveDisplayName(string characterId)
    {
        return AiTeammatePlaceholderCharacters.TryGetById(characterId, out AiTeammatePlaceholderCharacter placeholder)
            ? placeholder.DisplayName
            : AiCharacterCombatConfig.DefaultDisplayName;
    }

    private static string NormalizeCharacterId(string? characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            return AiCharacterCombatConfig.DefaultCharacterId;
        }

        return characterId.Trim().ToLowerInvariant();
    }

    private static string GetModRootPath()
    {
        string? assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(assemblyLocation))
        {
            return assemblyLocation;
        }

        return AppContext.BaseDirectory;
    }
}
