using System.IO;
using System.Text;

namespace ProjectCatalyst.Util
{
	public static class IsoReader
	{
		private const int SectorSize = 2048;
		private const int PrimaryVolumeDescriptorSector = 16;

		public static void ExtractFile(string isoPath, string isoFilePath, string outputPath)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(isoPath);
			ArgumentException.ThrowIfNullOrWhiteSpace(isoFilePath);
			ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

			if (!File.Exists(isoPath))
				throw new FileNotFoundException("PS3 ISO was not found.", isoPath);

			using FileStream isoStream = new(isoPath, FileMode.Open, FileAccess.Read, FileShare.Read);

			IsoDirectoryRecord root = ReadPrimaryVolumeDescriptor(isoStream);

			string[] parts = isoFilePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length == 0)
				throw new ArgumentException("ISO file path cannot be empty.", nameof(isoFilePath));

			IsoDirectoryRecord current = root;

			for (int i = 0; i < parts.Length; i++)
			{
				string part = parts[i];

				IsoDirectoryRecord next = i == parts.Length - 1 ? FindFile(isoStream, current, part) : FindDirectory(isoStream, current, part);

				current = next;
			}

			Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);

			isoStream.Position = (long)current.Extent * SectorSize;
			
			using FileStream outputStream = new(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);

			CopyExactly(isoStream, outputStream, current.Size);
		}

		private static void CopyExactly(Stream source, Stream destination, long length)
		{
			byte[] buffer = new byte[64 * 1024];

			long remaining = length;

			while (remaining > 0)
			{
				int count = (int)Math.Min(buffer.Length, remaining);
				int read = source.Read(buffer, 0, count);

				if (read == 0)
					throw new EndOfStreamException("Unexpected end of ISO while extracting file.");

				destination.Write(buffer, 0, read);

				remaining -= read;
			}
		}

		public static IsoDirectoryRecord ReadPrimaryVolumeDescriptor(FileStream iso)
		{
			iso.Position = (long)PrimaryVolumeDescriptorSector * SectorSize;

			Span<byte> sector = stackalloc byte[SectorSize];

			ReadExactly(iso, sector);

			// ISO9660 Primary Volume Descriptor:
			//
			// Byte 0  = Volume Descriptor Type
			// Bytes 1-5 = "CD001"
			//
			if (sector[0] != 1 || !sector[1..6].SequenceEqual("CD001"u8))
				throw new InvalidDataException("The file is not a valid ISO9660 image.");

			// Root directory record begins at byte 156.
			return ParseDirectoryRecord(sector[156..]);
		}

		public static IsoDirectoryRecord FindDirectory(FileStream iso, IsoDirectoryRecord parent, string name)
		{
			foreach (IsoDirectoryRecord entry in ReadDirectory(iso, parent))
			{
				if (entry.IsDirectory && entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
					return entry;
			}

			throw new FileNotFoundException($"Directory '{name}' was not found in the ISO.");
		}

		public static IsoDirectoryRecord FindFile(FileStream iso, IsoDirectoryRecord parent, string name)
		{
			foreach (IsoDirectoryRecord entry in ReadDirectory(iso, parent))
			{
				string cleanName = entry.Name;

				// ISO9660 commonly stores files as PARAM.SFO;1
				int semicolonIndex = cleanName.IndexOf(';');

				if (semicolonIndex >= 0)
					cleanName = cleanName[..semicolonIndex];

				if (!entry.IsDirectory && cleanName.Equals(name, StringComparison.OrdinalIgnoreCase))
					return entry;
			}

			throw new FileNotFoundException($"File '{name}' was not found in the ISO.");
		}

		private static IEnumerable<IsoDirectoryRecord> ReadDirectory(FileStream iso, IsoDirectoryRecord directory)
		{
			long offset = (long)directory.Extent * SectorSize;

			long remaining = directory.Size;

			iso.Position = offset;

			byte[] buffer = new byte[Math.Min(directory.Size, 1024 * 1024)];

			while (remaining > 0)
			{
				int bytesToRead = (int)Math.Min(buffer.Length, remaining);

				int bytesRead = iso.Read(buffer, 0, bytesToRead);

				if (bytesRead == 0) throw new EndOfStreamException();

				int position = 0;

				while (position < bytesRead)
				{
					int recordLength = buffer[position];

					if (recordLength == 0)
					{
						int nextSector = ((position / SectorSize) + 1) * SectorSize;
						position = Math.Min(nextSector, bytesRead);
						continue;
					}

					if (position + recordLength > bytesRead)
						throw new InvalidDataException("Invalid ISO9660 directory record.");

					IsoDirectoryRecord record = ParseDirectoryRecord(buffer.AsSpan(position, recordLength));

					if (record.Name != "." && record.Name != "..") yield return record;

					position += recordLength;
				}

				remaining -= bytesRead;
			}
		}

		private static IsoDirectoryRecord ParseDirectoryRecord(ReadOnlySpan<byte> record)
		{
			if (record.Length < 34)
				throw new InvalidDataException("Invalid ISO9660 directory record.");

			int recordLength = record[0];

			uint extent = ReadUInt32Le(record[2..6]);

			uint size = ReadUInt32Le(record[10..14]);

			byte flags = record[25];

			int nameLength = record[32];

			if (33 + nameLength > recordLength)
				throw new InvalidDataException("Invalid ISO9660 filename.");

			string name = Encoding.ASCII.GetString(record.Slice(33, nameLength));

			return new IsoDirectoryRecord
			{
				Name = name,
				Extent = extent,
				Size = size,
				IsDirectory = (flags & 0x02) != 0
			};
		}

		private static uint ReadUInt32Le(ReadOnlySpan<byte> data)
		{
			return data[0] |
				   ((uint)data[1] << 8) |
				   ((uint)data[2] << 16) |
				   ((uint)data[3] << 24);
		}

		private static void ReadExactly(Stream stream, Span<byte> buffer)
		{
			int totalRead = 0;

			while (totalRead < buffer.Length)
			{
				int read = stream.Read(buffer[totalRead..]);
				if (read == 0) throw new EndOfStreamException();
				totalRead += read;
			}
		}

		public sealed class IsoDirectoryRecord
		{
			public required string Name { get; init; }

			public required uint Extent { get; init; }

			public required uint Size { get; init; }

			public required bool IsDirectory { get; init; }
		}
	}

	public sealed class IsoFileStream(FileStream iso, uint sector, uint length) : Stream
	{
		private readonly long _start = (long)sector * 2048;

		private long _position;

		public override bool CanRead => true;

		public override bool CanSeek => true;

		public override bool CanWrite => false;

		public override long Length { get; } = length;

		public override long Position
		{
			get => _position;

			set
			{
				if (value < 0 || value > Length)
					throw new ArgumentOutOfRangeException(nameof(value));

				_position = value;
			}
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			ArgumentNullException.ThrowIfNull(buffer);

			if (_position >= Length)
			{
				return 0;
			}

			int remaining = checked((int)Math.Min(count, Length - _position));

			iso.Position = _start + _position;

			int read = iso.Read(buffer, offset, remaining);

			_position += read;

			return read;
		}

		public override int Read(Span<byte> buffer)
		{
			if (_position >= Length) return 0;

			int remaining = checked((int)Math.Min(buffer.Length, Length - _position));

			iso.Position = _start + _position;

			int read = iso.Read(buffer[..remaining]);

			_position += read;

			return read;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			long newPosition = origin switch
			{
				SeekOrigin.Begin =>
					offset,

				SeekOrigin.Current =>
					_position + offset,

				SeekOrigin.End =>
					Length + offset,

				_ => throw new ArgumentOutOfRangeException(
					nameof(origin))
			};

			Position = newPosition;

			return _position;
		}

		public override void Flush() { }

		public override void SetLength(long value) => throw new NotSupportedException();

		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
	}
}
