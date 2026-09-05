using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using BetterCharacterRelics;
using BetterCharacterRelics.Loader;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.HoverTips;

internal static class Program
{
    private static int _checks;

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args[0] == "--baseline")
            {
                var context = new AssemblyLoadContext("baseline", true);
                context.Resolving += (_, name) => AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(assembly => assembly.GetName().Name == name.Name && name.Name != "BetterCharacterRelics");
                Assembly baseline = context.LoadFromAssemblyPath(Path.GetFullPath(args[1]));
                baseline.GetType("BetterCharacterRelics.ModEntry")!.GetMethod("InstallHooks", BindingFlags.Static | BindingFlags.NonPublic)!
                    .Invoke(null, [new Harmony(ModEntry.HarmonyId)]);
                File.WriteAllLines(args[2], RuntimeSnapshot());
                Console.WriteLine("Baseline runtime patch table exported.");
                return 0;
            }

            string root = Path.GetFullPath(args[0]);
            string directory = Path.Combine(root, "tests", "snapshots", ModEntry.CompatibilityTarget);
            bool freeze = args.Contains("--accept-snapshots");
            var declarations = Patching.DeclaredTypes.Select(Patching.Describe).ToArray();
            var frozen = VanillaGuard.ReadFrozen();
            Check(frozen.Count == declarations.Length, "Embedded guard entry count differs from patch count");
            foreach (var declaration in declarations)
                Check(frozen.TryGetValue(VanillaGuard.Key(declaration.Target), out var fingerprint)
                    && fingerprint == VanillaGuard.Fingerprint(declaration.Target),
                    "Embedded guard cannot resolve actual patch target: " + declaration.Metadata.Id);
            Check(declarations.Length == 21, "Unexpected patch count");
            Check(declarations.Select(item => item.Metadata.Id).Distinct().Count() == declarations.Length, "Duplicate patch IDs");
            Check(declarations.Count(item => item.Handler.ReturnType == typeof(bool)) == 6, "Expected six skipping prefixes");
            foreach (var declaration in declarations)
            {
                Patching.ValidateBaseRoute(declaration);
                Check(!declaration.Metadata.Optional, "All supported-target methods are required");
                Check(declaration.Target.DeclaringType?.Assembly == typeof(AbstractModel).Assembly, "Third-party patch target");
                Check(declaration.Settings.priority == (declaration.Handler.ReturnType == typeof(bool) ? Priority.Low : Priority.Normal), "Unexpected patch priority");
                Check(declaration.Type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .All(method => !method.IsDefined(typeof(HarmonyPrepare)) && !method.IsDefined(typeof(HarmonyTargetMethod))), "Unsafe dynamic target discovery in tests");
                Type expectedReturn = declaration.Metadata.Id switch
                {
                    "watcher.localization" => typeof(void),
                    "watcher.hover" or "watcher.event-hover" => typeof(IEnumerable<IHoverTip>),
                    _ => declaration.Target.Name.StartsWith("get_") ? typeof(IEnumerable<DynamicVar>)
                        : declaration.Target.Name == "ModifyHandDraw" ? typeof(decimal) : typeof(Task)
                };
                Check(declaration.Target.ReturnType == expectedReturn, "Target return type drift");
            }
            Snapshot("patches.txt", declarations.Select(item => $"{item.Metadata.Id}|{VanillaGuard.Key(item.Target)}|{item.Handler.Name}|{item.Settings.priority}|before={string.Join(",", item.Settings.before ?? [])}|after={string.Join(",", item.Settings.after ?? [])}|{item.Metadata.Content}|optional={item.Metadata.Optional}"));
            Snapshot("vanilla-il.txt", declarations.Select(item => $"{VanillaGuard.Key(item.Target)}={VanillaGuard.Fingerprint(item.Target)}"));

            var ownTypes = typeof(ModEntry).Assembly.GetTypes();
            Check(ownTypes.All(type => !typeof(AbstractModel).IsAssignableFrom(type)), "Model discovery contract changed: loader needs review");
            Snapshot("mutable-statics.txt", ownTypes.Where(type => !type.IsDefined(typeof(CompilerGeneratedAttribute))).SelectMany(type => type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                .Where(field => !field.IsLiteral && (!field.IsInitOnly || !field.FieldType.IsValueType && field.FieldType != typeof(string)))
                .Select(field => $"{field.DeclaringType!.FullName}.{field.Name}:{field.FieldType.FullName}"));
            Snapshot("saved-properties.txt", ownTypes.SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
                .Where(property => property.CustomAttributes.Any(attribute => attribute.AttributeType.Name == "SavedPropertyAttribute"))
                .Select(property => $"{property.DeclaringType!.FullName}.{property.Name}"));
            var loaderFields = typeof(LoaderBootstrap).GetFields(BindingFlags.NonPublic | BindingFlags.Static).Where(field => !field.IsLiteral);
            Snapshot("loader-statics.txt", loaderFields.Select(field => $"{field.DeclaringType!.FullName}.{field.Name}:{field.FieldType.FullName}"));

            // 与旧前缀的最终公式对照，覆盖额外回合、无战斗和其它模组追加值。
            foreach (int rounds in new[] { 1, 3 })
            foreach (int? round in new int?[] { null, 1, 2, 3, 4, 9 })
            foreach (int turn in new[] { 1, 2, 3, 4, 12 })
            foreach (decimal count in new[] { -1m, 0m, 5m, 7.5m })
            foreach (decimal external in new[] { 0m, 2m, -0.5m })
            {
                decimal vanilla = count + (turn <= rounds ? 3m : 0m);
                decimal oldResult = count + (round.HasValue && round <= rounds ? 3m : 0m);
                Check(RelicRules.AdjustDraw(vanilla + external, round, turn, rounds, 3m, rounds) == oldResult + external,
                    $"Draw behavior changed: round={round}, turn={turn}, rounds={rounds}");
            }
            foreach (int? round in new int?[] { null, 1, 4 })
            foreach (int turn in new[] { 1, 10 })
                Check(RelicRules.AdjustDraw(5m + (turn <= 2.5m ? 8m : 0m), round, turn, 2.5m, 8m, 3)
                    == 5m + (round.HasValue && round <= 3 ? 3m : 0m), "Dynamic variable correction drift");

            IEnumerable<DynamicVar> variables = [new CardsVar(2), new DynamicVar("ForeignKey", 19m)];
            RelicEffects.RingOfTheSnakeCanonicalVarsPostfix(ref variables);
            Check(variables.Single(value => value.Name == "Cards").BaseValue == 3m, "Snake draw variable");
            Check(variables.Single(value => value.Name == "ForeignKey").BaseValue == 19m, "Foreign variable was erased");
            Check(LoaderBootstrap.SelectTarget(new Version(0, 107, 1)) == "0.107.1", "Legacy selection");
            Check(LoaderBootstrap.SelectTarget(new Version(0, 110, 0)) == "0.107.1", "Selection must not round up");
            Check(LoaderBootstrap.SelectTarget(new Version(0, 111, 0)) == "0.111.0", "Current selection");
            Check(LoaderBootstrap.SelectTarget(null) == "0.111.0", "Unknown selection");
            Check(LoaderBootstrap.ParseVersion("v0.111.0-beta+1") == new Version(0, 111, 0), "Version suffix parsing");
            try { LoaderBootstrap.SelectTarget(new Version(0, 106, 0)); throw new Exception("Old host accepted"); }
            catch (NotSupportedException) { _checks++; }

            Check(!WatcherSupport.AvailableSpecifications(AppDomain.CurrentDomain.GetAssemblies()).Any(), "Absent optional mod must stay inactive");
            foreach (var spec in WatcherSupport.Specifications)
            {
                Check(WatcherSupport.Match("RELIC", spec.RelicEntry, spec.RelicType, "Watcher") == spec, "Watcher identity rejected");
                Check(WatcherSupport.Match("POWER", spec.RelicEntry, spec.RelicType, "Watcher") == null, "Wrong category accepted");
                Check(WatcherSupport.Match("RELIC", spec.RelicEntry, spec.RelicType, "Foreign") == null, "Foreign assembly accepted");
                Check(WatcherSupport.Match("RELIC", spec.RelicEntry, "Foreign." + spec.RelicType, "Watcher") == null, "Foreign type accepted");
                Check(WatcherSupport.Match("RELIC", "OTHER_" + spec.RelicEntry, spec.RelicType, "Watcher") == null, "Foreign ID accepted");
                Check(spec.Mantra == (spec.RelicEntry.EndsWith("PURE_WATER") ? 2 : 5), "Mantra amount differs from requested effect");
            }
            for (int mask = 0; mask < 64; mask++)
                Check(WatcherSupport.CanGrant((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0,
                    (mask & 8) != 0, (mask & 16) != 0, (mask & 32) != 0) == (mask == 3), "Inactive relic can grant Mantra");
            string[] chinese =
            [
                "在每场战斗开始时，将[blue]1[/blue]张[gold]奇迹[/gold]放入你的手牌，并获得[blue]2[/blue]层[gold]真言[/gold]。",
                "在每场战斗开始时，将[blue]3[/blue]张[gold]奇迹[/gold]放入你的手牌，并获得[blue]5[/blue]层[gold]真言[/gold]。"
            ];
            string[] keys = ["pure_water.description", "holy_water.description"];
            foreach (string locale in new[] { "zhs", "eng", "jpn" })
            {
                var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root,
                    "assets", "localization", locale, WatcherSupport.LocalizationFile + ".json")))!;
                Check(strings.Count == 2, "Unexpected Watcher localization keys");
                for (int i = 0; i < keys.Length; i++)
                {
                    string value = strings[keys[i]];
                    Check(string.Join("", Regex.Matches(value, @"\[/?\w+\]").Select(match => match.Value))
                        == "[blue][/blue][gold][/gold][blue][/blue][gold][/gold]", "Wrong or unbalanced color tags");
                    if (locale == "zhs") Check(value == chinese[i], "Requested Chinese wording changed");
                }
            }

            foreach (string file in new[] { "relics.json", WatcherSupport.LocalizationFile + ".json" })
            {
                var english = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "assets/localization/eng", file)))!;
                var japanese = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(root, "assets/localization/jpn", file)))!;
                Check(english.Keys.ToHashSet().SetEquals(japanese.Keys), "Incomplete Japanese localization");
                foreach (var (key, value) in english)
                {
                    Check(Regex.Matches(value, @"\[/?\w+\]").Select(match => match.Value)
                        .SequenceEqual(Regex.Matches(japanese[key], @"\[/?\w+\]").Select(match => match.Value)), "Japanese color tag mismatch: " + key);
                    Check(Regex.Matches(value, @"\{[^{}]+\}").Select(match => match.Value).Order()
                        .SequenceEqual(Regex.Matches(japanese[key], @"\{[^{}]+\}").Select(match => match.Value).Order()), "Japanese placeholder mismatch: " + key);
                }
            }

            int contract = Array.IndexOf(args, "--watcher-contract");
            if (contract >= 0)
            {
                string providerPath = Path.GetFullPath(args[contract + 1]);
                string baseLibPath = Path.GetFullPath(args[contract + 2]);
                AssemblyLoadContext.Default.Resolving += (_, name) => name.Name == "BaseLib"
                    ? AssemblyLoadContext.Default.LoadFromAssemblyPath(baseLibPath) : null;
                Assembly provider = AssemblyLoadContext.Default.LoadFromAssemblyPath(providerPath);
                var available = WatcherSupport.AvailableSpecifications([provider]).ToArray();
                Check(available.Length == 2, "Watcher provider no longer inherits the required combat hook or Mantra type changed");
                Check(available.Select(spec => spec.Mantra).Order().SequenceEqual(new[] { 2, 5 }), "Incomplete provider support");
                Console.WriteLine($"Watcher contract: {providerPath}; {string.Join(", ", available.Select(spec => spec.RelicEntry))}");
            }

            if (args.Contains("--runtime-patches"))
            {
                var harmony = new Harmony(ModEntry.HarmonyId);
                foreach (var declaration in declarations)
                    Check(harmony.CreateClassProcessor(declaration.Type).Patch()?.Count == 1, $"Patch failed: {declaration.Metadata.Id}");
                File.WriteAllLines(Path.Combine(root, "tests", "current-runtime-" + ModEntry.CompatibilityTarget + ".txt"), RuntimeSnapshot());
                CoreFocusTests.Run(Check);
            }
            Console.WriteLine($"PASS {_checks} checks; target={ModEntry.CompatibilityTarget}; models=0; saved properties=0.");
            return 0;

            void Snapshot(string name, IEnumerable<string> lines)
            {
                string text = string.Join("\n", lines.Order(StringComparer.Ordinal)) + "\n";
                string path = Path.Combine(directory, name);
                if (freeze)
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(path, text);
                    Console.WriteLine($"Accepted {path}");
                }
                else Check(File.ReadAllText(path).Replace("\r\n", "\n") == text, $"Snapshot mismatch: {path}");
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static IEnumerable<string> RuntimeSnapshot()
    {
        foreach (var target in Harmony.GetAllPatchedMethods().OrderBy(VanillaGuard.Key, StringComparer.Ordinal))
        {
            var info = Harmony.GetPatchInfo(target)!;
            foreach (var patch in info.Prefixes.Concat(info.Postfixes).Where(patch => patch.owner == ModEntry.HarmonyId))
                yield return $"{VanillaGuard.Key(target)}|{(info.Prefixes.Contains(patch) ? "Prefix" : "Postfix")}|{patch.priority}|before={string.Join(",", patch.before)}|after={string.Join(",", patch.after)}|il={VanillaGuard.Fingerprint((MethodInfo)target)}";
        }
    }
}
