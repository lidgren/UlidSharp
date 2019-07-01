using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace UlidSharp
{
	[StructLayout(LayoutKind.Sequential)]
	public readonly struct Ulid : IEquatable<Ulid>, IComparable<Ulid>
	{
		public const int SizeInBytes = 16;

		public static readonly Ulid Empty = new Ulid(0, 0);

		private static readonly long s_timeOffset = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)TicksToMilliSeconds(Stopwatch.GetTimestamp());

		public readonly ulong Low;
		public readonly ulong High;

		private const string ENCODE = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
		private static readonly byte[] DECODE = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 255, 255, 255, 255, 255, 255, 255, 10, 11, 12, 13, 14, 15, 16, 17, 255, 18, 19, 255, 20, 21, 255, 22, 23, 24, 25, 26, 255, 27, 28, 29, 30, 31, 255, 255, 255, 255, 255, 255, 10, 11, 12, 13, 14, 15, 16, 17, 255, 18, 19, 255, 20, 21, 255, 22, 23, 24, 25, 26, 255, 27, 28, 29, 30, 31 };

		public Ulid(ulong low, ulong high)
		{
			Low = low;
			High = high;
		}

		public Ulid(ReadOnlySpan<byte> fromBytes)
		{
#if DEBUG
			if (fromBytes.Length < 16)
				throw new ArgumentException("bytes too few");
#endif
			High = BinaryPrimitives.ReadUInt64LittleEndian(fromBytes.Slice(0, 8));
			Low = BinaryPrimitives.ReadUInt64LittleEndian(fromBytes.Slice(8, 8));
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Ulid Create()
		{
			return Create(ref s_rndState);
		}

		public static Ulid Create(ref ulong rndState)
		{
			var time = s_timeOffset + TicksToMilliSeconds(Stopwatch.GetTimestamp());
			return new Ulid(
				NextUInt64(ref rndState),
				((ulong)time << 16) | (NextUInt64(ref rndState) & 0xFFFF)
			);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Ulid Create(long stopwatchTicks)
		{
			return Create(stopwatchTicks, ref s_rndState);
		}

		public static Ulid Create(long stopwatchTicks, ref ulong rndState)
		{
			var time = s_timeOffset + TicksToMilliSeconds(stopwatchTicks);

			return new Ulid(
				NextUInt64(ref rndState),
				((ulong)time << 16) | (NextUInt64(ref rndState) & 0xFFFF)
			);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Ulid Create(in DateTimeOffset dto)
		{
			return Create(dto, ref s_rndState);
		}

		public static Ulid Create(in DateTimeOffset dto, ref ulong rndState)
		{
			var time = dto.ToUnixTimeMilliseconds();
			return new Ulid(
				NextUInt64(ref rndState),
				((ulong)time << 16) | (NextUInt64(ref rndState) & 0xFFFF)
			);
		}

		public static Ulid Create(ReadOnlySpan<char> fromString)
		{
#if DEBUG
			if (fromString.Length < 26)
				throw new ArgumentException("string too short");

			for (int i = 0; i < 26; i++)
			{
				var c = fromString[i];
				if (DECODE[c] == 255)
					throw new Exception("Bad char in Ulid string: " + c);
			}
#endif
			ulong high =
				(ulong)DECODE[fromString[0]] << 61 |
				((ulong)DECODE[fromString[1]] << 56) |
				((ulong)DECODE[fromString[2]] << 51) |
				((ulong)DECODE[fromString[3]] << 46) |
				((ulong)DECODE[fromString[4]] << 41) |
				((ulong)DECODE[fromString[5]] << 36) |
				((ulong)DECODE[fromString[6]] << 31) |
				((ulong)DECODE[fromString[7]] << 26) |
				((ulong)DECODE[fromString[8]] << 21) |
				((ulong)DECODE[fromString[9]] << 16) |
				((ulong)DECODE[fromString[10]] << 11) |
				((ulong)DECODE[fromString[11]] << 6) |
				((ulong)DECODE[fromString[12]] << 1);

			ulong straddle = DECODE[fromString[13]];
			high |= (straddle >> 4); // 1 bit from straddle to high

			ulong low = (ulong)straddle << 60 | // keep 4 bits from straddle
				((ulong)DECODE[fromString[14]] << 55) |
				((ulong)DECODE[fromString[15]] << 50) |
				((ulong)DECODE[fromString[16]] << 45) |
				((ulong)DECODE[fromString[17]] << 40) |
				((ulong)DECODE[fromString[18]] << 35) |
				((ulong)DECODE[fromString[19]] << 30) |
				((ulong)DECODE[fromString[20]] << 25) |
				((ulong)DECODE[fromString[21]] << 20) |
				((ulong)DECODE[fromString[22]] << 15) |
				((ulong)DECODE[fromString[23]] << 10) |
				((ulong)DECODE[fromString[24]] << 5) |
				((ulong)DECODE[fromString[25]]);

			return new Ulid(low, high);
		}

		public void AsString(Span<char> into)
		{
			into[0] = ENCODE[(int)(High >> 61)];
			into[1] = ENCODE[(int)((High >> 56) & 0b11111)];
			into[2] = ENCODE[(int)((High >> 51) & 0b11111)];
			into[3] = ENCODE[(int)((High >> 46) & 0b11111)];
			into[4] = ENCODE[(int)((High >> 41) & 0b11111)];
			into[5] = ENCODE[(int)((High >> 36) & 0b11111)];
			into[6] = ENCODE[(int)((High >> 31) & 0b11111)];
			into[7] = ENCODE[(int)((High >> 26) & 0b11111)];
			into[8] = ENCODE[(int)((High >> 21) & 0b11111)];
			into[9] = ENCODE[(int)((High >> 16) & 0b11111)];
			into[10] = ENCODE[(int)((High >> 11) & 0b11111)];
			into[11] = ENCODE[(int)((High >> 6) & 0b11111)];
			into[12] = ENCODE[(int)((High >> 1) & 0b11111)];

			var straddle = ((High & 0b1) << 4) | ((Low >> 60) & 0b1111);
			into[13] = ENCODE[(int)straddle];

			into[14] = ENCODE[(int)((Low >> 55) & 0b11111)];
			into[15] = ENCODE[(int)((Low >> 50) & 0b11111)];
			into[16] = ENCODE[(int)((Low >> 45) & 0b11111)];
			into[17] = ENCODE[(int)((Low >> 40) & 0b11111)];
			into[18] = ENCODE[(int)((Low >> 35) & 0b11111)];
			into[19] = ENCODE[(int)((Low >> 30) & 0b11111)];
			into[20] = ENCODE[(int)((Low >> 25) & 0b11111)];
			into[21] = ENCODE[(int)((Low >> 20) & 0b11111)];
			into[22] = ENCODE[(int)((Low >> 15) & 0b11111)];
			into[23] = ENCODE[(int)((Low >> 10) & 0b11111)];
			into[24] = ENCODE[(int)((Low >> 5) & 0b11111)];
			into[25] = ENCODE[(int)(Low & 0b11111)];
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void AsBytes(Span<byte> into)
		{
			into[0] = (byte)High;
			into[1] = (byte)(High >> 8);
			into[2] = (byte)(High >> 16);
			into[3] = (byte)(High >> 24);
			into[4] = (byte)(High >> 32);
			into[5] = (byte)(High >> 40);
			into[6] = (byte)(High >> 48);
			into[7] = (byte)(High >> 56);
			into[8] = (byte)Low;
			into[9] = (byte)(Low >> 8);
			into[10] = (byte)(Low >> 16);
			into[11] = (byte)(Low >> 24);
			into[12] = (byte)(Low >> 32);
			into[13] = (byte)(Low >> 40);
			into[14] = (byte)(Low >> 48);
			into[15] = (byte)(Low >> 56);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetTime(out DateTimeOffset time)
		{
			time = DateTimeOffset.FromUnixTimeMilliseconds((long)(High >> 16));
		}

		public override string ToString()
		{
			return String.Create<Ulid>(26, this, (span, ulid) =>
			{
				ulid.AsString(span);
			});
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override bool Equals(object obj)
		{
			if (obj == null || !(obj is Ulid))
				return false;
			var other = (Ulid)obj;
			return other.Low == Low && other.High == High;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(Ulid other)
		{
			return other.Low == Low && other.High == High;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Equals(in Ulid other)
		{
			return other.Low == Low && other.High == High;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator == (in Ulid x, in Ulid y)
		{
			return (x.Low == y.Low && x.High == y.High);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator != (in Ulid x, in Ulid y)
		{
			return (x.Low != y.Low || x.High != y.High);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override int GetHashCode()
		{
			// Low is all random bits; can be used verbatim
			// But the time part of High needs to be mixed a bit
			unchecked
			{
				// simple mixing since we're xoring with random bits anyway
				ulong mixed = High;
				mixed ^= mixed >> 47;
				mixed *= 0xc6a4a7935bd1e995ul;
				mixed ^= mixed >> 47;

				ulong hash = mixed ^ Low;

				// xor fold to 32 bits
				return (int)((uint)hash ^ (uint)(hash >> 32));
			}
		}

		public int CompareTo(Ulid other)
		{
			if (High < other.High)
				return -1;
			if (High > other.High)
				return 1;
			if (Low < other.Low)
				return -1;
			if (Low > other.Low)
				return 1;
			return 0;
		}

		//
		// PRNG based on XorShift64*
		//
		private static ulong s_rndState;
		private static double s_dInvMilliFreq;

		static Ulid()
		{
			// random seed; hash some entropy
			var rnd = new byte[8];
			System.Security.Cryptography.RandomNumberGenerator.Fill(rnd);

			var h1 = (ulong)rnd[0] | (ulong)rnd[1] << 8 | (ulong)rnd[2] << 16 | (ulong)rnd[3] << 24 | (ulong)rnd[4] << 32 | (ulong)rnd[5] << 40 | (ulong)rnd[6] << 48 | (ulong)rnd[7] << 56;
			var h2 = Guid.NewGuid().GetHashCode() | ((Process.GetCurrentProcess().Id) << 32);
			var h3 = Environment.WorkingSet;
			var h4 = Environment.TickCount;
			var h5 = Stopwatch.GetTimestamp();
			s_rndState = (ulong)h1 ^ (ulong)h2 ^ (ulong)h3 ^ (ulong)h4 ^ (ulong)h5;

			s_dInvMilliFreq = 1000.0 / (double)Stopwatch.Frequency;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static double TicksToMilliSeconds(long stopwatchTicks)
		{
			return (double)stopwatchTicks * s_dInvMilliFreq;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static ulong NextUInt64(ref ulong state)
		{
			var x = state;
			x ^= x >> 12;
			x ^= x << 25;
			x ^= x >> 27;
			state = x;
			return x * 0x2545F4914F6CDD1D;
		}
	}
}
