﻿namespace BizHawk.Client.Common
{
	public interface IRewindSettings
	{
		/// <summary>
		/// Gets a value indicating whether or not to compress savestates before storing them
		/// </summary>
		bool UseCompression { get; }

		/// <summary>
		/// Gets a value indicating whether or not to delta compress savestates before storing them
		/// </summary>
		/// <value></value>
		// TODO: This is in here for frontend reasons, but the buffer itself doesn't interact with this.
		bool UseDelta { get; }

		/// <summary>
		/// Buffer space to use in MB
		/// </summary>
		long BufferSize { get; }

		/// <summary>
		/// Specifies whether TargetFrameLength or TargetRewindFrequency is used.
		/// </summary>
		public bool ConsistentRewindFrequency { get; }
		
		/// <summary>
		/// Desired frame length (number of emulated frames you can go back before running out of buffer)
		/// </summary>
		int TargetFrameLength { get; }
		/// <summary>
		/// Desired rewind frequency (number of emulated frames you can go back per rewind)
		/// </summary>
		int TargetRewindFrequency { get; }

		public enum BackingStoreType
		{
			Memory,
			TempFile,
		}

		public BackingStoreType BackingStore { get; }
	}

	public class RewindConfig : IRewindSettings
	{
		public bool UseCompression { get; set; }
		public bool UseDelta { get; set; }
		public bool Enabled { get; set; } = true;
		public long BufferSize { get; set; } = 512; // in mb
		public bool ConsistentRewindFrequency { get; set; } = true;
		public int TargetFrameLength { get; set; } = 600;
		public int TargetRewindFrequency { get; set; } = 5;
		public IRewindSettings.BackingStoreType BackingStore { get; set; } = IRewindSettings.BackingStoreType.Memory;
	}
}
