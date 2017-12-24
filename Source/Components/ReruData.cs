using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Relua;
using static Relua.Data;

namespace Rerulsd {
	using static ReruData;

	static class ReruData {
		public static ArrayType[] MakeArray<ArrayType>(ArrayType Input, int Size) =>
			Enumerable.Repeat(Input, Size).ToArray();

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

		public static string Spaced(object Me, int Length) { // For proper spacing
			string Strm = Me.ToString();

			return Strm + new string(' ', Length < Strm.Length ? 1 : Length - Strm.Length);
		}

		public static StringBuilder ReruAppend(this StringBuilder Me, int Times, bool Line, string String) =>
			Me.Append($"{(Line ? "\n" : "")}{(Times == 0 ? "" : new string('\t', Times))}{String}"); // Lol.

		public static bool IsRegist(int Idx) => // Is this a register (not constant)?
			((Idx & 0x100) == 0);

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

	abstract class LuaResult {
		protected LuaProto Serial;
		protected List<LuaLocal> Regists;
		protected StringBuilder Result;

		protected List<LuaInstruct> Instrs = null;
		protected List<LuaConstant> Consts = null;
		protected List<LuaProto> Wrappeds = null;
		protected List<LuaLocal> Locals = null;
		protected List<string> Upvalues = null;

		protected bool State;
		protected bool Ready;

		public void Set(LuaProto Subject) {
			Instrs = Subject.Instructs;
			Consts = Subject.Constants;
			Wrappeds = Subject.Protos;
			Upvalues = Subject.Upvalues;
			Locals = Subject.Locals;

			Serial = null;
			State = false;
			Ready = false;

			Serial = Subject;

			LuaDeclarations(); // Pre-declare stuff
		}

		public string LocalAt(int Index) =>
			((Index >= 0) && (Index < Regists.Count)) ? Regists[Index].Name : Index.ToString();

		protected LuaConstant RegOrConst(int Place) =>
			IsRegist(Place) ? new LuaConstant(LuaType.FLUID, LocalAt(Place)) : Consts[Place - 0x100];

		private void LuaDeclarations() {
			int Progc,
				Progb;

			int StkSize = Serial.Stack;
			int InsSize = Instrs.Count;

			bool[] Skips = MakeArray(false, InsSize);
			bool[] Local = MakeArray(false, StkSize);
			bool[] Temp = MakeArray(false, StkSize);

			int[] Read = MakeArray(0, StkSize);
			int[] Write = MakeArray(0, StkSize);

			Regists = new List<LuaLocal>();

			#region ScanDeclars Scans over the source for declarations

			for (Progc = 0; Progc < InsSize; Progc++) { // Declarations
				LuaInstruct Inst = Instrs[Progc];

				if (Skips[Progc]) // Skipped
					continue;

				int A = Inst.A,
					B = Inst.B,
					C = Inst.C;

				switch (Inst.Opcode) {
					case LuaOpcode.MOVE:
						Write[A]++;
						Read[B]++;

						Local[Math.Min(A, B)] = true;

						break;
					case LuaOpcode.LOADK:
					case LuaOpcode.LOADBOOL:
					case LuaOpcode.GETUPVAL:
					case LuaOpcode.GETGLOBAL:
					case LuaOpcode.NEWTABLE:
						Write[A]++;

						break;
					case LuaOpcode.LOADNIL:
						for (int RegIdx = A; RegIdx <= B; RegIdx++)
							Write[RegIdx]++;

						break;
					case LuaOpcode.GETTABLE:
						Write[A]++;

						if (IsRegist(B))
							Read[B]++;
						if (IsRegist(C))
							Read[C]++;

						break;
					case LuaOpcode.SETGLOBAL:
					case LuaOpcode.SETUPVAL:
						Read[A]++;

						break;
					case LuaOpcode.SETTABLE:
					case LuaOpcode.ADD:
					case LuaOpcode.SUB:
					case LuaOpcode.MUL:
					case LuaOpcode.DIV:
					case LuaOpcode.MOD:
					case LuaOpcode.POW:
						Read[A]++;

						if (IsRegist(B))
							Read[B]++;
						if (IsRegist(C))
							Read[C]++;

						break;
					case LuaOpcode.SELF:
						Write[A]++;
						Write[A + 1]++;
						Read[B]++;

						if (IsRegist(C))
							Read[C]++;

						break;
					case LuaOpcode.UNM:
					case LuaOpcode.NOT:
					case LuaOpcode.LEN:
						Write[A]++;
						Read[B]++;

						break;
					case LuaOpcode.CONCAT:
						Write[A]++;

						for (int RegIdx = B; RegIdx <= C; RegIdx++) {
							Read[RegIdx]++;
							Temp[RegIdx] = true;
						}

						break;
					case LuaOpcode.SETLIST:
						Temp[A + 1] = true;

						break;
					case LuaOpcode.JMP:
						break; // Do nothing
					case LuaOpcode.EQ:
					case LuaOpcode.LT:
					case LuaOpcode.LE:
						if (IsRegist(B))
							Read[B]++;
						if (IsRegist(C))
							Read[C]++;

						break;
					case LuaOpcode.TEST:
						Read[A]++;

						break;
					case LuaOpcode.TESTSET:
						Write[A]++;
						Read[B]++;

						break;
					case LuaOpcode.CLOSURE:
						int NumUpvals = Wrappeds[B].NumUpvals;

						for (int RegIdx = 1; RegIdx <= NumUpvals; RegIdx++) {
							int Idx = Progc + RegIdx;

							if (Idx < Instrs.Count) { // Otherwise, from above stack
								if (Instrs[Idx].Opcode == LuaOpcode.MOVE)
									Local[Instrs[Idx].A] = true;

								Skips[Idx] = true;
							}
						}

						break;
					case LuaOpcode.CALL:
						if (C >= 2) {
							int Lim = A + C - 2;

							for (int RegIdx = A; RegIdx <= Lim; RegIdx++)
								Write[RegIdx]++;
						};

						goto case LuaOpcode.TAILCALL; // Just jumps down
					case LuaOpcode.TAILCALL:
						int LimB = A + B - 1;

						for (int RegIdx = A; RegIdx <= LimB; RegIdx++) {
							Read[A]++;
							Temp[A] = true;
						}

						if (C >= 2) {
							int LimC = A + C - 2;
							int Next = Progc + 1;

							while ((LimC >= A) && (Next < InsSize)) {
								LuaInstruct Ninstruct = Instrs[Next];

								if ((B == LimC) && (Ninstruct.Opcode == LuaOpcode.MOVE)) {
									Write[Ninstruct.A]++;
									Read[Ninstruct.B]++;

									Local[Ninstruct.A] = true;

									Skips[Next] = true;
								}

								LimC--;
								Next++;
							}
						}

						break;
					default:
						break;
				}
			}
			#endregion

			#region SetDeclars Outputs declarations to "Scoped"

			for (Progc = 0, Progb = 0; Progc < StkSize; Progc++) {
				string Name = "L";

				bool IsTemp = Temp[Progc],
					IsLocal = Local[Progc];

				if (Progc < Serial.NumArgs) {
					IsLocal = true;
					Name = "A";
				}

				/*if (!IsLocal && !IsTemp) {
					IsLocal = true;
					//int Reads = Read[Progc];

					//IsLocal = (Reads > 1 || Reads == 0);
				}*/

				if ((Read[Progc] != 0) || (Write[Progc] != 0))
					IsLocal = true;

				if (IsLocal) {
					LuaLocal Declaration = new LuaLocal {
						Arg = Name.Equals("A")
					};

					if (Progb < Locals.Count)
						Declaration.Name = Locals[Progb].Name;
					else
						Declaration.Name = Name + Progc + "_" + Progb;

					Progb++;
					Regists.Add(Declaration);
				}
			}

			#endregion
		}

		public abstract string GetSource(int Level);
	}
}
