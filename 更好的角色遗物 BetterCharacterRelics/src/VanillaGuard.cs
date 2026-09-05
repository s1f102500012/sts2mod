using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using MegaCrit.Sts2.Core.Logging;

namespace BetterCharacterRelics;

internal static class VanillaGuard
{
    internal static string Key(MethodBase method) => $"{method.DeclaringType!.FullName}::{method.Name}({string.Join(",", method.GetParameters().Select(p => p.ParameterType.FullName))})";

    internal static string Fingerprint(MethodInfo method)
    {
        // async 入口只创建状态机；结算逻辑在 MoveNext，必须一同冻结。
        MethodInfo? moveNext = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
            .GetMethod("MoveNext", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        string bodies = string.Join("|", new[] { method, moveNext }.Where(item => item != null)
            .Select(item => Convert.ToHexString(item!.GetMethodBody()?.GetILAsByteArray() ?? [])));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bodies)));
    }

    internal static Dictionary<string, string> ReadFrozen()
    {
        using Stream stream = typeof(ModEntry).Assembly.GetManifestResourceStream("vanilla-il.txt")
            ?? throw new InvalidOperationException("Missing embedded vanilla IL snapshot");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(line =>
            {
                // 泛型参数的 FullName 包含 Version=、Culture= 等程序集标识；只有末尾的等号分隔 IL 哈希。
                int separator = line.LastIndexOf('=');
                if (separator <= 0 || separator == line.Length - 1)
                    throw new InvalidDataException("Malformed vanilla IL snapshot entry");
                return new KeyValuePair<string, string>(line[..separator], line[(separator + 1)..]);
            }).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    internal static void Verify(MethodInfo target)
    {
        if (!ReadFrozen().TryGetValue(Key(target), out string? expected))
            throw new InvalidOperationException($"Missing frozen target: {Key(target)}");
        if (expected != Fingerprint(target))
            Log.Warn($"[BetterCharacterRelics][Guard] DRIFT {Key(target)}; target={ModEntry.CompatibilityTarget}");
        else
            Log.Info($"[BetterCharacterRelics][Guard] OK {Key(target)}");
    }
}
