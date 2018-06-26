using System;
using System.Collections.Generic;
using System.Text;
using static SeeLua.Abstracted.StaticsData;

namespace Lida {
	static class ReruData {
		public static string SymbolOf(LuaOpcode Me) {
			switch (Me) {
				case LuaOpcode.ADD:
					return " + ";
				case LuaOpcode.SUB:
					return " - ";
				case LuaOpcode.MUL:
					return " * ";
				case LuaOpcode.DIV:
					return " / ";
				case LuaOpcode.MOD:
					return " % ";
				case LuaOpcode.POW:
					return " ^ ";
				case LuaOpcode.UNM:
					return "-";
				case LuaOpcode.NOT:
					return "not ";
				case LuaOpcode.LEN:
					return "#";
				case LuaOpcode.CONCAT:
					return " .. ";
				case LuaOpcode.EQ:
					return " == ";
				case LuaOpcode.LT:
					return " < ";
				case LuaOpcode.LE:
					return " <= ";
				default:
					break;
			}

			return " ? ";
		}

		public static StringBuilder AlignedAppend(this StringBuilder Me, int Times, bool Line, string String) =>
			Me.Append((Line ? "\n" : "").PadRight(Times + 1, '\t') + String); // Lol.
	}

	class StringMem {
		private Dictionary<int, string> Mem;
		private string Pref;
		private int Off;

		public int Size {
			get => Mem.Count;
		}

		public string See(int Register, bool Caches) {
			Mem.TryGetValue(Register, out string Ret);
			
			if (Ret == null) {
				if (Register < Off)
					Ret = Pref;
				else
					Ret = "VAR";

				Ret = Ret + "_" + Register + "_";

				if (Caches)
					Mem[Register] = Ret;
			}
			
			return Ret;
		}

		public string this[int Register] {
			get => See(Register, true);
		}

		public StringMem(String[] L, string Prefix, int Offset = 0) {
			Mem = new Dictionary<int, string>(L.Length);

			for (int Idx = 0; Idx < L.Length; Idx++)
				Mem[Idx] = L[Idx];

			Pref = Prefix;
			Off = Offset;
		}
	}
}
