using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Platform;

namespace AITeammate.Scripts;

internal static class AiTeammateParticipantNames
{
    private static readonly object RandomLock = new();
    private static readonly Random NameRandom = new();
    private static readonly Dictionary<string, int> AssignedNameIndices = new(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string[]> ChineseAiNames = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["IRONCLAD"] =
        [
            "老练战士", "重甲战士", "持剑战士", "盾牌战士", "红甲战士", "铁拳战士",
            "火把战士", "背包战士", "急性子战士", "稳重战士", "前排战士", "粗犷战士"
        ],
        ["SILENT"] =
        [
            "小刀猎手", "毒药猎手", "兜帽猎手", "轻步猎手", "短刀猎手", "冷静猎手",
            "背刺猎手", "夜行猎手", "谨慎猎手", "快手猎手", "飞刀猎手", "独行猎手"
        ],
        ["DEFECT"] =
        [
            "蓝球机器人", "电球机器人", "冰球机器人", "充能机器人", "旧壳机器人", "修补机器人",
            "短路机器人", "巡逻机器人", "打扫机器人", "慢热机器人", "过载机器人", "备用机器人"
        ],
        ["REGENT"] =
        [
            "普通储君", "认真储君", "铸造储君", "月球储君", "星星储君", "皇冠储君",
            "护卫储君", "算牌储君", "稳健储君", "冒失储君", "后排储君", "练习储君"
        ],
        ["NECROBINDER"] =
        [
            "骨头契约师", "墓地契约师", "骷髅契约师", "召唤契约师", "灰袍契约师", "守墓契约师",
            "虚空契约师", "低语契约师", "慢性子契约师", "后勤契约师", "阴影契约师", "借力契约师"
        ]
    };

    private static readonly IReadOnlyDictionary<string, string[]> EnglishAiNames = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["IRONCLAD"] =
        [
            "Old Warrior", "Armored Warrior", "Sword Warrior", "Shield Warrior", "Red Warrior", "Fist Warrior",
            "Torch Warrior", "Pack Warrior", "Hasty Warrior", "Steady Warrior", "Frontline Warrior", "Rough Warrior"
        ],
        ["SILENT"] =
        [
            "Knife Hunter", "Poison Hunter", "Hooded Hunter", "Lightfoot Hunter", "Shortblade Hunter", "Calm Hunter",
            "Backstab Hunter", "Night Hunter", "Careful Hunter", "Quickhand Hunter", "Throwing Hunter", "Solo Hunter"
        ],
        ["DEFECT"] =
        [
            "Blue Bot", "Lightning Bot", "Frost Bot", "Charge Bot", "Old Bot", "Patch Bot",
            "Shorted Bot", "Patrol Bot", "Cleanup Bot", "Slow Bot", "Overload Bot", "Spare Bot"
        ],
        ["REGENT"] =
        [
            "Plain Regent", "Serious Regent", "Forge Regent", "Moon Regent", "Star Regent", "Crown Regent",
            "Guard Regent", "Counting Regent", "Steady Regent", "Careless Regent", "Backline Regent", "Practice Regent"
        ],
        ["NECROBINDER"] =
        [
            "Bone Binder", "Grave Binder", "Skull Binder", "Summon Binder", "Gray Binder", "Graveyard Binder",
            "Void Binder", "Whisper Binder", "Slow Binder", "Support Binder", "Shadow Binder", "Borrowed Binder"
        ]
    };

    public static string HostDisplayName(ulong playerId)
    {
        try
        {
            PlatformType platform = PlatformUtil.PrimaryPlatform;
            if (platform != PlatformType.None)
            {
                string name = PlatformUtil.GetPlayerName(platform, playerId);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to resolve host platform name: {ex.Message}");
        }

        return AiTeammateLocalization.Tr("session.host_player");
    }

    public static string AiDisplayName(CharacterModel character, int sameCharacterIndex, int fallbackSlotIndex)
    {
        return AiDisplayName(character, (ulong)Math.Max(1, fallbackSlotIndex), null, fallbackSlotIndex);
    }

    public static string AiDisplayName(
        CharacterModel character,
        ulong playerId,
        ISet<string>? usedDisplayNames,
        int fallbackSlotIndex)
    {
        string characterId = character.Id.Entry;
        bool useChinese = AiTeammateLocalization.IsCurrentChineseLanguage();
        IReadOnlyDictionary<string, string[]> table = useChinese ? ChineseAiNames : EnglishAiNames;

        if (table.TryGetValue(characterId, out string[]? names) && names.Length > 0)
        {
            string assignmentKey = $"{(useChinese ? "zh" : "en")}:{characterId}:{playerId}";
            int index = GetOrAssignNameIndex(assignmentKey, names.Length);
            if (usedDisplayNames == null || !usedDisplayNames.Contains(names[index]))
            {
                usedDisplayNames?.Add(names[index]);
                return names[index];
            }

            int? unusedIndex = ChooseUnusedNameIndex(names, usedDisplayNames);
            if (unusedIndex.HasValue)
            {
                RememberNameIndex(assignmentKey, unusedIndex.Value);
                usedDisplayNames.Add(names[unusedIndex.Value]);
                return names[unusedIndex.Value];
            }

            string fallbackDuplicateName = $"{names[index]}{fallbackSlotIndex}";
            usedDisplayNames?.Add(fallbackDuplicateName);
            return fallbackDuplicateName;
        }

        string fallbackName = LocalizedCharacterName(character);
        string displayName = $"{fallbackName}{fallbackSlotIndex}";
        usedDisplayNames?.Add(displayName);
        return displayName;
    }

    private static int GetOrAssignNameIndex(string assignmentKey, int count)
    {
        lock (RandomLock)
        {
            if (AssignedNameIndices.TryGetValue(assignmentKey, out int existingIndex) &&
                existingIndex >= 0 &&
                existingIndex < count)
            {
                return existingIndex;
            }

            int index = NameRandom.Next(count);
            AssignedNameIndices[assignmentKey] = index;
            return index;
        }
    }

    private static void RememberNameIndex(string assignmentKey, int index)
    {
        lock (RandomLock)
        {
            AssignedNameIndices[assignmentKey] = index;
        }
    }

    private static int? ChooseUnusedNameIndex(IReadOnlyList<string> names, ISet<string> usedDisplayNames)
    {
        List<int> availableIndices = [];
        for (int index = 0; index < names.Count; index++)
        {
            if (!usedDisplayNames.Contains(names[index]))
            {
                availableIndices.Add(index);
            }
        }

        if (availableIndices.Count == 0)
        {
            return null;
        }

        lock (RandomLock)
        {
            return availableIndices[NameRandom.Next(availableIndices.Count)];
        }
    }

    public static string LocalizedCharacterName(CharacterModel character)
    {
        if (AiTeammatePlaceholderCharacters.TryGetByModelId(character.Id.Entry, out AiTeammatePlaceholderCharacter placeholder))
        {
            return AiTeammateLocalization.CharacterName(placeholder);
        }

        try
        {
            return character.Title.GetFormattedText();
        }
        catch (Exception ex)
        {
            Log.Warn($"[AITeammate] Failed to resolve localized character name for {character.Id.Entry}: {ex.Message}");
            return character.Id.Entry;
        }
    }
}
