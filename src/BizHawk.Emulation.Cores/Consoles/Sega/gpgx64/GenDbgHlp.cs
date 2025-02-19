﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;

using BizHawk.Common;

namespace BizHawk.Emulation.Cores.Consoles.Sega.gpgx
{
	/*
	 * how to use:
	 * 0) get https://code.google.com/p/pdbparse/
	 * 1) set modulename to the name of the dll file.
	 * 2) set symbolname to the name of a file that you produced by executing the following command:
	 *    pdb_print_gvars.py [module pdb file] 0x00000000 > [output file]
	 * 3) set start to an address (relative to the beginning of the dll) to start scanning
	 * 4) set length to the byte length of the scan area
	 * 5) instantiate a GenDbWind, and use it to control the scanner while you manipulate the dll into various configurations.
	 * 
	 * ideas for modification:
	 * 1) unhardcode config parameters and allow modifying them through the interface
	 * 2) read section sizes and positions from the dll itself instead of the start\length params
	 * 3) support an ignore list of symbols
	 */

	public sealed class GenDbgHlp : IDisposable
	{
		// config
		private const string modulename = "libgenplusgx.dll";
		private const string symbolname = @"D:\encodes\bizhawksrc\genplus-gx\libretro\msvc\Debug\vars.txt";
		private const int start = 0x0c7d8000 - 0x0c540000;
		private const int length = 0x01082000;

		private bool disposed => DllBase == IntPtr.Zero;

		public void Dispose()
		{
			if (DllBase == IntPtr.Zero) return; // already freed
			OSTailoredCode.LinkedLibManager.FreeByPtr(DllBase);
			DllBase = IntPtr.Zero;
		}

		private IntPtr DllBase;

		private readonly List<Symbol> SymbolsByAddr = new List<Symbol>();
		private readonly IDictionary<string, Symbol> SymbolsByName = new Dictionary<string, Symbol>();

		private readonly byte[][] data = new byte[10][];

		public void SaveState(int statenum)
		{
			if (disposed) throw new ObjectDisposedException(nameof(GenDbgHlp));

			data[statenum] ??= new byte[length];

			Marshal.Copy(DllBase + start, data[statenum], 0, length);
			Console.WriteLine("State {0} saved", statenum);
		}


		public unsafe void Cmp(int statex, int statey)
		{
			if (disposed) throw new ObjectDisposedException(nameof(GenDbgHlp));
			List<Tuple<int, int>> bads = new List<Tuple<int, int>>();

			byte[] x = data[statex];
			byte[] y = data[statey];

			if (x == null || y == null)
			{
				Console.WriteLine("Missing State!");
				return;
			}

			bool inrange = false;
			int startsec = 0;

			fixed (byte* p0 = &x[0])
			fixed (byte* p1 = &y[0])
			{
				for (int i = 0; i < length; i++)
				{
					if (!inrange)
					{
						if (p0[i] != p1[i])
						{
							startsec = i;
							inrange = true;
						}
					}
					else
					{
						if (p0[i] == p1[i])
						{
							bads.Add(new Tuple<int, int>(startsec, i));
							inrange = false;
						}
					}
				}
			}
			if (inrange)
				bads.Add(new Tuple<int, int>(startsec, length));

			for (int i = 0; i < bads.Count; i++)
			{
				IntPtr addr = (IntPtr)(bads[i].Item1 + start);
				int len = bads[i].Item2 - bads[i].Item1;

				var ss = Find(addr, len);
				Console.WriteLine("0x{0:X8}[0x{1}]", (int)addr, len);
				foreach (var sym in ss)
					Console.WriteLine(sym);
				Console.WriteLine();
			}
			if (bads.Count == 0)
				Console.WriteLine("Clean!");
		}

		public GenDbgHlp()
		{
			using (StreamReader sr = new StreamReader(symbolname))
			{
				string line;
				while ((line = sr.ReadLine()) != null)
				{
					Symbol sym = Symbol.FromString(line);
					SymbolsByAddr.Add(sym);
					SymbolsByName.Add(sym.name, sym);
				}
				SymbolsByAddr.Sort();
			}
			DllBase = OSTailoredCode.LinkedLibManager.LoadOrThrow(modulename);
		}

		public List<Symbol> Find(IntPtr addr, int length)
		{
			if (disposed) throw new ObjectDisposedException(nameof(GenDbgHlp));
			Symbol min = new Symbol { addr = addr };
			Symbol max = new Symbol { addr = addr + length };

			int minidx = SymbolsByAddr.BinarySearch(min);
			if (minidx < 0)
			{
				minidx = ~minidx;
				// inexact matches return the first larger value, so find the next smallset one
				if (minidx > 0)
					minidx--;
			}
			int maxidx = SymbolsByAddr.BinarySearch(max);
			if (maxidx < 0)
			{
				maxidx = ~maxidx;
				if (maxidx > 0)
					maxidx--;
			}
			return SymbolsByAddr.GetRange(minidx, maxidx - minidx + 1);
		}

		public struct Symbol : IComparable<Symbol>
		{
			public IntPtr addr;
			public string section;
			public string name;

			public static Symbol FromString(string s)
			{
				string[] ss = s.Split(',');
				if (ss.Length != 4)
					throw new Exception();
				if (!ss[1].StartsWith("0x"))
					throw new Exception();
				Symbol ret = new Symbol
				{
					addr = (IntPtr)int.Parse(ss[1].Substring(2), System.Globalization.NumberStyles.AllowHexSpecifier),
					section = ss[3],
					name = ss[0]
				};
				return ret;
			}

			public int CompareTo(Symbol other)
			{
				return (int)this.addr - (int)other.addr;
			}

			public override string ToString() => $"0x{(int)addr:X8} {name} ({section})";
		}
	}
}
