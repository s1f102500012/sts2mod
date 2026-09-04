namespace IntegratedStrategyEvents;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal sealed class IntegratedStrategyPatchAttribute(string id, string feature, string scope) : Attribute
{
	public string Id { get; } = id;
	public string Feature { get; } = feature;
	public string Scope { get; } = scope;
	public bool Optional { get; set; }
	// 多目标补丁显式登记类型，不执行 TargetMethods 来发现目标。
	public Type[] AdditionalTargets { get; set; } = [];
}
