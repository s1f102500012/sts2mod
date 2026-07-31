namespace HextechRunes;

internal static class HextechStableModelIdListCodec
{
	public const int Version = -3;
	public const int MaxCount = 64;
	public const int MaxSerializedLength = 128;

	public static void Append(List<int> payload, IEnumerable<ModelId> modelIds)
	{
		ModelId[] ids = modelIds.ToArray();
		if (ids.Length > MaxCount)
		{
			throw new ArgumentOutOfRangeException(
				nameof(modelIds),
				ids.Length,
				$"ModelId payload count must not exceed {MaxCount}.");
		}

		string[] serializedIds = new string[ids.Length];
		for (int i = 0; i < ids.Length; i++)
		{
			string serialized = ids[i].ToString();
			if (serialized.Length > MaxSerializedLength)
			{
				throw new ArgumentException(
					$"Serialized ModelId length must not exceed {MaxSerializedLength}: {serialized.Length}.",
					nameof(modelIds));
			}

			serializedIds[i] = serialized;
		}

		payload.Add(Version);
		payload.Add(ids.Length);
		foreach (string serialized in serializedIds)
		{
			payload.Add(serialized.Length);
			foreach (char ch in serialized)
			{
				payload.Add(ch);
			}
		}
	}

	public static bool TryDecode(IReadOnlyList<int> payload, int cursor, out List<ModelId> modelIds, out int nextCursor)
	{
		modelIds = [];
		nextCursor = cursor;
		if (!HasRemaining(payload, cursor, 1) || payload[cursor] != Version)
		{
			return false;
		}

		cursor++;
		if (!HasRemaining(payload, cursor, 1))
		{
			return false;
		}

		int count = payload[cursor++];
		if (count < 0 || count > MaxCount)
		{
			return false;
		}

		for (int i = 0; i < count; i++)
		{
			if (!HasRemaining(payload, cursor, 1))
			{
				return false;
			}

			int length = payload[cursor++];
			if (length < 0 || length > MaxSerializedLength || !HasRemaining(payload, cursor, length))
			{
				return false;
			}

			char[] chars = new char[length];
			for (int j = 0; j < length; j++)
			{
				int value = payload[cursor + j];
				if (value < char.MinValue || value > char.MaxValue)
				{
					return false;
				}

				chars[j] = (char)value;
			}

			try
			{
				modelIds.Add(ModelId.Deserialize(new string(chars)));
			}
			catch
			{
				modelIds.Clear();
				return false;
			}

			cursor += length;
		}

		nextCursor = cursor;
		return true;
	}

	private static bool HasRemaining(IReadOnlyList<int> payload, int cursor, int count)
	{
		return cursor >= 0
			&& count >= 0
			&& cursor <= payload.Count
			&& count <= payload.Count - cursor;
	}
}
