using Relua;
using System;
using System.Collections.Generic;
using System.Text;
using static Relua.Data;

namespace Rerulsd {
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

		public static string Sanitize(this string Dirty) { // Might be slow on long strings; I'll have to remake
			StringBuilder Result = new StringBuilder();
			string Repl;

			foreach (byte Byte in Dirty) {
				switch (Byte) {
					case ((byte) '"'):
						Repl = "\\\"";

						break;
					case ((byte) '\\'):
						Repl = "\\\\";

						break;
					case ((byte) '\b'):
						Repl = "\\b";

						break;
					case ((byte) '\n'):
						Repl = "\\n";

						break;
					case ((byte) '\r'):
						Repl = "\\r";

						break;
					case ((byte) '\t'):
						Repl = "\\t";

						break;
					default:
						char Real = (char) Byte;

						if (!Char.IsWhiteSpace(Real) && ((Byte < 33) || (Byte > 126)))
							Repl = "\\" + Byte;
						else
							Repl = Real.ToString();

						break;
				}

				Result.Append(Repl);
			}

			return Result.ToString();
		}
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

	abstract class LuaResult {
		protected LuaProto Proto;
		protected StringMem Locals;
		protected StringMem Upvalues;
		protected StringBuilder Source;
		// Bring up our Proto data
		protected List<LuaInstruct> Instrs;
		protected List<LuaConstant> Consts;
		protected List<LuaProto> Subprotos;

		protected LuaConstant RegOrConst(int Place) =>
			IsRegist(Place) ? new LuaConstant(LuaType.FLUID, Locals[Place]) : Proto.Constants[Place - 0x100];

		// Propagates information about the proto to its children
		private void Propagate() {
			if (Proto.Protos.Count == 0)
				return;

			List<LuaInstruct> Instrs = Proto.Instructs;

			for (int Idx = 0; Idx < Instrs.Count; Idx++) {
				LuaInstruct Inst = Instrs[Idx];

				switch (Inst.Opcode) {
					case LuaOpcode.CLOSURE: // Handle *external upvalues*
						LuaProto Sub = Proto.Protos[Inst.B];

						if (Sub.NumUpvals != 0) {
							List<String> Neups = new List<String>(Sub.Upvalues);

							for (int Upv = 0; Upv < Sub.NumUpvals; Upv++) { // Overwrite predefined upvalues, sync
								LuaInstruct Udef = Sub.Instructs[Upv];

								if (Udef.Opcode == LuaOpcode.MOVE)
									Neups[Upv] = Locals[Udef.B];
								else if (Udef.Opcode == LuaOpcode.GETUPVAL)
									Neups[Upv] = Upvalues[Udef.B];
							}

							Sub.Upvalues = Neups;
						}

						break;
				}
			}
		}

		public abstract string GetSource(int Level);
		// NOTE: The Proto MUST be initialized

		public LuaResult(LuaProto P) {
			String[] Locs = new String[P.Locals.Count];
			String[] Upvs = P.Upvalues.ToArray();

			Instrs = P.Instructs;
			Consts = P.Constants;
			Subprotos = P.Protos;

			Proto = P;

			for (int Idx = 0; Idx < Locs.Length; Idx++)
				Locs[Idx] = P.Locals[Idx].Name;

			Locals = new StringMem(Locs, "ARG", P.NumArgs);
			Upvalues = new StringMem(Upvs, "UPVAL", int.MaxValue);

			for (int Idx = 0; Idx < P.NumArgs; Idx++) {
				if (Idx < P.Locals.Count) {
					LuaLocal Loc = new LuaLocal {
						Name = Locals[Idx],
						Arg = true
					};

					P.Locals[Idx] = Loc;
				}
			}
		}
	}
}
