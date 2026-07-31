namespace HextechRunes;

internal static class HextechDataPaths
{
	internal static string GetDataDirectory()
	{
		try
		{
			string godotUserDir = Godot.OS.GetUserDataDir();
			if (!string.IsNullOrWhiteSpace(godotUserDir))
			{
				return Path.Combine(godotUserDir, ModInfo.Id);
			}
		}
		catch
		{
			// Godot 用户目录不可用时仍需落到普通的每用户数据目录。
		}

		string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		if (string.IsNullOrWhiteSpace(baseDir))
		{
			baseDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		}

		return Path.Combine(baseDir, "SlayTheSpire2", ModInfo.Id);
	}

	internal static string GetFilePath(string fileName)
	{
		return Path.Combine(GetDataDirectory(), fileName);
	}
}
