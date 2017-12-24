using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Relua;
using static Relua.Data;

// Rerudec.cs : Standalone disassembler and (wip) decompiler
namespace Rerulsd {
	using static ReruData;
	
	class LuaDisassembly : LuaResult {
		public LuaDisassembly(LuaProto Subject) {
			if (Subject != null)
				Set(Subject);
		}

		void DeclareHeader(int Level) {
			string Parent = Serial.Parent == null ? "None" : Serial.Parent.Name;

			Result.ReruAppend(Level, true, $".function {Serial.Stack} {Serial.NumArgs} {Serial.Vararg} \"{Serial.Name}\""); // Declaration
			Result.ReruAppend(Level, true, $"; function {Serial}\n");
			Result.ReruAppend(Level, true, $"; parent {Parent}");
			Result.ReruAppend(Level, true, $"; params {Serial.NumArgs}, upvalues {Serial.NumUpvals}, {(Serial.Vararg == 0 ? "not" : "is")} vararg\n");

			Result.Remove(0, 1); // Newline
		}

		void DeclareProtoHeaders(int Level) {
			if (Wrappeds.Count != 0) {
				Result.ReruAppend(Level, true, "; Sub-function(s) list");

				for (int Index = 0; Index < Wrappeds.Count; Index++)
					Result.ReruAppend(Level, true, $"; function {Wrappeds[Index]}");

				Result.AppendLine();
			}
		}

		void DeclareLocals(int Level, int NumLocals) {
			for (int Index = 0; Index < NumLocals; Index++) {
				LuaLocal Local = Regists[Index];

				Result.ReruAppend(Level, true, Spaced($".local \"{Local.Name}\"", 24) + $"; {Index}, {(Local.Arg ? "argument" : "normal")}");
			}
		}

		void DeclareUpvalues(int Level) {
			for (int Index = 0; Index < Serial.NumUpvals; Index++)
				Result.ReruAppend(Level, true, Spaced($".upvalue \"{Upvalues[Index]}\"", 24) + $"; {Index}");
		}

		void DeclareConstants(int Level) {
			for (int Index = 0; Index < Consts.Count; Index++)
				Result.ReruAppend(Level, true, Spaced($".const {Consts[Index]}", 24) + $"; {Index}");
		}

		void DeclareProtos(int Level) {
			for (int Index = 0; Index < Wrappeds.Count; Index++) {
				LuaProto Wrap = Wrappeds[Index];
				string Source = new LuaDisassembly(Wrap).GetSource(Level + 1);
				
				Result.ReruAppend(0, true, $"\n{Source}");
			}
		}

		// Disassembler state : Complete
		public override string GetSource(int Level) {
			int NumLocals = Regists.Count;

			Result = new StringBuilder();

			DeclareHeader(Level);
			DeclareProtoHeaders(Level);
			DeclareLocals(Level, NumLocals);
			DeclareUpvalues(Level);
			DeclareConstants(Level);
			DeclareProtos(Level);

			Result.AppendLine();

			for (int Index = 0; Index < Instrs.Count; Index++) {
				LuaInstruct Instr = Instrs[Index];
				string Parse = null;

				int NA = Instr.A,
					NB = Instr.B,
					NC = Instr.C;

				string A = LocalAt(NA),
					B = LocalAt(NB),
					C = LocalAt(NC);

				string Raw = $"/0x{Index:X2}/ ({Serial.Lines[Index]:D4}) {Spaced(Instr.Opcode, 12)}{Spaced((NA == 0) ? "-" : $"{NA}", 3)} {Spaced((NB == 0) ? "-" : $"{NB}", 3)} {Spaced((NC == 0) ? "-" : $"{NC}", 3)}";

				switch (Instr.Opcode) {
					case LuaOpcode.MOVE:
						Parse = $"{A} := {B}";

						break;
					case LuaOpcode.LOADK:
						Parse = $"{A} := {Consts[NB]}";

						break;
					case LuaOpcode.LOADBOOL:
						Parse = $"{A} := {(NB != 0).ToString().ToLower()}";

						break;
					case LuaOpcode.LOADNIL:
						if (A == B)
							Parse = $"{A} := nil";
						else
							Parse = $"R({NA}) to R({NB}) := nil";

						break;
					case LuaOpcode.GETUPVAL:
						Parse = $"{A} := Upvalue[\"{Upvalues[NB]}\"]";

						break;
					case LuaOpcode.GETGLOBAL:
						Parse = $"{A} := Gbl[{Consts[NB]}]";

						break;
					case LuaOpcode.GETTABLE:
						Parse = $"{A} := {B}[{RegOrConst(NC)}]";

						break;
					case LuaOpcode.SETGLOBAL:
						Parse = $"Gbl[{Consts[NB]}] = {A}";

						break;
					case LuaOpcode.SETUPVAL:
						Parse = $"Upvalue[\"{Upvalues[NB]}\"] := {A}";

						break;
					case LuaOpcode.SETTABLE:
						Parse = $"{A}[{RegOrConst(NB)}] := {RegOrConst(NC)}";

						break;
					case LuaOpcode.NEWTABLE:
						Parse = $"{A} = Array : {NB}, Hash : {NC}";

						break;
					case LuaOpcode.SELF:
						Parse = $"{A} := {B}[{RegOrConst(NC)}]";

						break;
					case LuaOpcode.UNM:
					case LuaOpcode.NOT:
					case LuaOpcode.LEN:
						Parse = $"{A} := {SymbolOf(Instr.Opcode)}{B}";

						break;
					case LuaOpcode.ADD:
					case LuaOpcode.SUB:
					case LuaOpcode.MUL:
					case LuaOpcode.DIV:
					case LuaOpcode.MOD:
					case LuaOpcode.POW:
					case LuaOpcode.CONCAT:
					case LuaOpcode.EQ:
					case LuaOpcode.LT:
					case LuaOpcode.LE:
						Parse = $"{A} := {RegOrConst(NB)}{SymbolOf(Instr.Opcode)}{RegOrConst(NC)}";

						break;
					case LuaOpcode.JMP:
						Parse = $"Jump {((NB > 0) ? "forward" : "back")} to PC(0x{Index + NB + 1:X2})";

						break;
					case LuaOpcode.TEST:
						LuaInstruct Next = Instrs[Index + 2];

						if (Next.Opcode == LuaOpcode.TEST)
							Parse = $"{A} {(NC == 0 ? "and" : "or")} PC(0x{Index + 2:X2})";
						else
							Parse = $"if not {A}";

						break;
					case LuaOpcode.TESTSET:
						LuaInstruct SetNext = Instrs[Index + 2];

						Parse = $"{A} = {B} {(NC == 0 ? "and" : "or")} PC(0x{Index + 2:X2})";

						break;
					case LuaOpcode.CALL:
						Parse = $"Call {A}";

						if (NB == 0)
							Parse += $", params R({NA + 1}) to top";
						else if (NB == 1)
							Parse += ", no params";
						else if (NB == 2)
							Parse += $", param {LocalAt(NA + 1)}";
						else
							Parse += $", params R({NA + 1}) to R({NA + NB - 1})";

						if (NC == 0)
							Parse += ", multiple returns";
						else if (NC == 1)
							Parse += ", no returns";
						else
							Parse += $", returns {NC - 1} item(s)";

						break;
					case LuaOpcode.TAILCALL:
						Parse = $"Tailcall {A}";

						break;
					case LuaOpcode.RETURN:
						if (NB == 1)
							Parse = "Return nothing";
						else if (NB == 2)
							Parse = $"Return {A}";
						else if (NB > 1)
							Parse = $"Return R({NA}) to R({NA + NB - 2})";
						else
							Parse = $"Return R({NA}) to top";

						break;
					case LuaOpcode.FORLOOP:
						Parse = $"Loops to PC(0x{(Index + NB + 1):X2})";

						break;
					case LuaOpcode.FORPREP:
						Parse = $"Start loop to PC(0x{(Index + NB + 1):X2})";

						break;
					case LuaOpcode.TFORLOOP:
						Parse = "Jump out for-loop on exit";

						break;
					case LuaOpcode.SETLIST:
						Parse = $"List at {A}";

						break;
					case LuaOpcode.CLOSE:
						Parse = $"Close upvalues R({NA})+";

						break;
					case LuaOpcode.CLOSURE:
						LuaProto Clos = Wrappeds[NB];
						StringBuilder ProtoIfn = new StringBuilder();
						int Skips = 0;

						for (int Idx = 0; Idx < Clos.NumUpvals; Idx++) {
							LuaInstruct Upval = Instrs[Index + Idx + 1];
							string Type;

							if (Upval.Opcode == LuaOpcode.MOVE)
								Type = $"; (0x{Index + Idx + 1:X2}) > Local upvalue ({Upval.B}) \"{LocalAt(Upval.B)}\"";
							else if (Upval.Opcode == LuaOpcode.GETUPVAL)
								Type = $"; (0x{Index + Idx + 1:X2}) > Upvalue ({Upval.B}) \"{Upvalues[Upval.B]}\"";
							else
								break;

							Skips++;

							ProtoIfn.ReruAppend(Level + 1, true, Type);
						}

						if (Skips != 0)
							ProtoIfn.AppendLine();

						if ((Index + Skips + 1) < Instrs.Count) {
							LuaInstruct Namer = Instrs[Index + Skips + 1];
							string Name = A;

							if (Namer.A == NA) {
								if (Namer.Opcode == LuaOpcode.SETGLOBAL)
									Name = Consts[Namer.B].ToString();
								else if (Namer.Opcode == LuaOpcode.SETUPVAL)
									Name = Upvalues[Namer.B];
							}
							
							ProtoIfn.Insert(0, $"{Name} := {Clos}");
						}

						Parse = ProtoIfn.ToString();

						break;
					case LuaOpcode.VARARG:
						if (NB == 0)
							Parse = "... (?)";
						else
							Parse = $"... ({NB})";

						break;
					default:
						break;
				}

				if (String.IsNullOrEmpty(Parse))
					Raw = Raw.TrimEnd(' '); // Trim off excess
				else
					Raw = $"{Spaced(Raw, 30)}; {Parse}";

				Result.ReruAppend(Level, true, Raw);
			}

			Result.ReruAppend(Level, true, $".end ; {Serial}");

			return Result.ToString();
		}
	}
}