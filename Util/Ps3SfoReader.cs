using System.IO;
using System.Text;

namespace ProjectCatalyst.Util
{
	public sealed record Ps3GameMetadata
	{
		public required string TitleId { get; init; }

		public required string Title { get; init; }

		public string? Version { get; init; }

		public string? AppVersion { get; init; }

		public string? Ps3SystemVersion { get; init; }

		public string? Category { get; init; }
	}

	public static class Ps3SfoReader
	{
		public static Ps3GameMetadata ReadMetadata(string isoPath)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(isoPath);

			using FileStream stream = new(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			using Stream sfoStream = OpenParamSfo(stream);

			return ParamSfoReader.Read(sfoStream);
		}

		private static Stream OpenParamSfo(FileStream iso)
		{
			IsoReader.IsoDirectoryRecord root = IsoReader.ReadPrimaryVolumeDescriptor(iso);

			IsoReader.IsoDirectoryRecord ps3Game = IsoReader.FindDirectory(
				iso,
				root,
				"PS3_GAME");

			IsoReader.IsoDirectoryRecord paramSfo = IsoReader.FindFile(
				iso,
				ps3Game,
				"PARAM.SFO");

			return new IsoFileStream(
				iso,
				paramSfo.Extent,
				paramSfo.Size);
		}
	}
	
	public static class ParamSfoReader
	{
		private const uint SfoVersion = 0x00000101;

		private const ushort FormatUtf8 = 0x0204;
		private const ushort FormatUtf8Special = 0x0004;
		private const ushort FormatInt32 = 0x0404;

		public static Ps3GameMetadata Read(Stream stream)
		{
			ArgumentNullException.ThrowIfNull(stream);

			using BinaryReader reader = new(
				stream,
				Encoding.UTF8,
				leaveOpen: true);

			ReadHeader(
				reader,
				out uint keyTableOffset,
				out uint dataTableOffset,
				out uint entryCount);

			List<SfoEntry> entries = new((int)entryCount);

			for (int i = 0; i < entryCount; i++)
			{
				ushort keyOffset = reader.ReadUInt16();
				ushort format = reader.ReadUInt16();
				uint dataLength = reader.ReadUInt32();
				uint dataMaxLength = reader.ReadUInt32();
				uint dataOffset = reader.ReadUInt32();

				entries.Add(
					new SfoEntry(
						keyOffset,
						format,
						dataLength,
						dataMaxLength,
						dataOffset
					)
				);
			}

			Dictionary<string, object?> values = new(StringComparer.OrdinalIgnoreCase);

			foreach (SfoEntry entry in entries)
			{
				long keyPosition = checked((long)keyTableOffset + entry.KeyOffset);

				stream.Position = keyPosition;

				string key = ReadNullTerminatedString(reader);

				long dataPosition = checked((long)dataTableOffset + entry.DataOffset);

				stream.Position = dataPosition;

				object? value = ReadValue(reader, entry.Format, entry.DataLength, entry.DataMaxLength);

				values[key] = value;
			}

			string? titleId = GetString(values, "TITLE_ID");
			string? title = GetString(values, "TITLE");

			if (string.IsNullOrWhiteSpace(titleId))
				throw new InvalidDataException("PARAM.SFO does not contain TITLE_ID.");

			if (string.IsNullOrWhiteSpace(title))
				throw new InvalidDataException("PARAM.SFO does not contain TITLE.");

			return new Ps3GameMetadata
			{
				TitleId = titleId,
				Title = title,
				Version = GetString(values, "VERSION"),
				AppVersion = GetString(values, "APP_VER"),
				Ps3SystemVersion = GetString(values, "PS3_SYSTEM_VER"),
				Category = GetString(values, "CATEGORY")
			};
		}

		private static void ReadHeader(BinaryReader reader, out uint keyTableOffset, out uint dataTableOffset, out uint entryCount)
		{
			uint magic = reader.ReadUInt32();

			const uint expectedMagic = 0x46535000;

			if (magic != expectedMagic)
				throw new InvalidDataException("Invalid PARAM.SFO magic.");

			uint version = reader.ReadUInt32();

			if (version != SfoVersion)
				throw new InvalidDataException($"Unsupported PARAM.SFO version: 0x{version:X8}.");

			keyTableOffset = reader.ReadUInt32();
			dataTableOffset = reader.ReadUInt32();
			entryCount = reader.ReadUInt32();

			switch (entryCount)
			{
				case 0:
					throw new InvalidDataException("PARAM.SFO contains no entries.");
				case > 10_000:
					throw new InvalidDataException($"PARAM.SFO contains an unreasonable number of entries: {entryCount}.");
			}
		}

		private static object? ReadValue(BinaryReader reader, ushort format, uint dataLength, uint dataMaxLength)
		{
			if (dataLength > dataMaxLength)
				throw new InvalidDataException("PARAM.SFO data length exceeds maximum length.");

			switch (format)
			{
				case FormatUtf8:
				case FormatUtf8Special:
					{
						byte[] data = reader.ReadBytes(checked((int)dataLength));

						return DecodeUtf8(data);
					}

				case FormatInt32:
					{
						if (dataLength != 4)
							throw new InvalidDataException($"Invalid INT32 SFO field length: {dataLength}.");

						return reader.ReadUInt32();
					}

				default:
					{
						// Unknown SFO type.
						//
						// We still consume the data so parsing can continue.
						reader.ReadBytes(checked((int)dataLength));

						return null;
					}
			}
		}

		private static string DecodeUtf8(byte[] data)
		{
			int length = Array.IndexOf(data, (byte)0);

			if (length < 0) length = data.Length;

			return Encoding.UTF8.GetString(data, 0, length);
		}

		private static string ReadNullTerminatedString(BinaryReader reader)
		{
			using MemoryStream buffer = new();

			while (true)
			{
				byte value = reader.ReadByte();

				if (value == 0) break;

				buffer.WriteByte(value);
			}

			return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, checked((int)buffer.Length));
		}

		private static string? GetString(Dictionary<string, object?> values, string key) => values.TryGetValue(key, out object? value) ? value?.ToString() : null;

		private sealed record SfoEntry(ushort KeyOffset, ushort Format, uint DataLength, uint DataMaxLength, uint DataOffset);
	}
}
