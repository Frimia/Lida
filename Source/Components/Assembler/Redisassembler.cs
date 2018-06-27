using System;
using System.Collections.Generic;
using System.Text;
using SeeLua.Abstracted;
using static SeeLua.Abstracted.StaticsData;

// Rerudec.cs : Standalone disassembler and (wip) decompiler
namespace Lida {
	using static ReruData;

	class LuaDisassembly { // A *tiny* bit bloated but we didn't really need LuaResult anyways
		private LuaProto Proto;
		private StringMem Locals;
		private StringMem Upvalues;
		private StringBuilder Source;
		// Bring up our Proto data
		private List<LuaInstruct> Instrs;
		private List<LuaConstant> Consts;
		private List<LuaProto> Subprotos;

		private List<LuaDisassembly> Preprotos;

		private string GetKst(int Idx) {
			string Res = "ERR_NO_KST" + Idx;

			if (Idx < Consts.Count) {
				LuaConstant K = Consts[Idx];

				if (K.Type == LuaConstantType.STRING) {
					Res = $"\"{K.String.Sanitize()}\"";
				}
				else {
					Res = K.ToString();
				}
			}

			return Res;
		}

		public LuaDisassembly(LuaProto P) {
			String[] Locs = new String[P.Locals.Count];
			String[] Upvs = P.Upvalues.ToArray();

			Instrs = P.Instructs;
			Consts = P.Consts;
			Subprotos = P.Protos;

			Proto = P;

			for (int Idx = 0; Idx < Locs.Length; Idx++)
				Locs[Idx] = P.Locals[Idx].Name;

			Locals = new StringMem(Locs, "ARG", P.Numparams);
			Upvalues = new StringMem(Upvs, "UPVAL", int.MaxValue);
		}

		private string RegOrConst(int Place) =>
			IsRegist(Place) ? Locals[Place] : GetKst(Place - 0x100);
		
		void DeclareHeader(int Level) {
			string Name = Proto.Name.Sanitize();
			string Data = Proto.ToString().Substring(Name.Length + 2);

			Source.AlignedAppend(Level, true, $".function {Proto.LineBegin} {Proto.LineEnd} \"{Name}\" ;{Data}"); // Declaration

			if (Proto.Stack != 0) // Should NEVER be
				Source.AlignedAppend(Level, true, ".stack " + Proto.Stack);
			if (Proto.Numparams != 0)
				Source.AlignedAppend(Level, true, ".numparams " + Proto.Numparams);
			if (Proto.Nups != 0)
				Source.AlignedAppend(Level, true, ".nups " + Proto.Nups);
			if (Proto.Vararg != 0)
				Source.AlignedAppend(Level, true, ".vararg " + Proto.Vararg);

			Source.Remove(0, 1);
		}

		void DeclareProtoHeaders(int Level) {
			if (Subprotos.Count != 0) {
				Preprotos = new List<LuaDisassembly>(Subprotos.Count);

				Source.AppendLine();

				for (int Index = 0; Index < Subprotos.Count; Index++) {
					Preprotos.Add(new LuaDisassembly(Subprotos[Index]));

					Source.AlignedAppend(Level + 1, true, $"; function {Subprotos[Index]}");
				}
			}
		}

		void DeclareLocals(int Level) {
			if (Proto.Locals.Count != 0) {
				Source.AppendLine();

				for (int Index = 0; Index < Proto.Locals.Count; Index++) {
					int S = Proto.Locals[Index].Startpc,
						E = Proto.Locals[Index].Endpc;
					
					Source.AlignedAppend(
						Level,
						true,
						String.Format(
							".local \"{0}\" {1} {2}{3}",
							Locals[Index],
							S,
							E,
							Index < Proto.Numparams ? " ; argument" : ""
						)
					);
				}
			}
		}

		void DeclareUpvalues(int Level) {
			if (Upvalues.Size != 0) {
				Source.AppendLine();

				for (int Index = 0; Index < Upvalues.Size; Index++)
					Source.AlignedAppend(Level, true, $".upvalue \"{Upvalues[Index]}\"");
			}
		}

		void DeclareConstants(int Level) {
			if (Consts.Count != 0) {
				Source.AppendLine();

				for (int Index = 0; Index < Consts.Count; Index++)
					Source.AlignedAppend(Level, true, $".const {GetKst(Index)}");
			}
		}

		void DeclareProtos(int Level) {
			if (Preprotos == null)
				return;

			Source.AppendLine();

			for (int Index = 0; Index < Preprotos.Count; Index++) {
				string Sub = Preprotos[Index].ReadProto(Level + 1);
				
				Source.AlignedAppend(0, true, Sub);
			}
		}

		void DeclareInstructions(int Level, StringBuilder Text) {
			int LA = 3,
				LB = 3,
				LC = 5;

			for (int Index = 0; Index < Instrs.Count; Index++) {
				LuaInstruct Instr = Instrs[Index];
				string Parse = null;

				int NA = Instr.A,
					NB = Instr.B,
					NC = Instr.C;
				
				LA = Math.Max(LA, NA.ToString().Length + 2);
				LB = Math.Max(LB, NB.ToString().Length + 2);
				LC = Math.Max(LC, NC.ToString().Length + 4);

				string Line = Index < Proto.Lines.Count ? "(" + Proto.Lines[Index].ToString().PadLeft(3, '0') + ") " : String.Empty;
				string Raw = String.Format("/0x{0:X2}/ {1}{2}{3}{4}{5}",
					Index,
					Line,
					Instr.Opcode.ToString().PadRight(12),
					((NA == 0) ? "-" : $"{NA}").PadRight(LA),
					((NB == 0) ? "-" : $"{NB}").PadRight(LB),
					((NC == 0) ? "-" : $"{NC}").PadRight(LC)
					);

				switch (Instr.Opcode) {
					case LuaOpcode.MOVE:
						Parse = $"{Locals[NA]} := {Locals[NB]}";

						break;
					case LuaOpcode.LOADK:
						Parse = $"{Locals[NA]} := {GetKst(NB)}";

						break;
					case LuaOpcode.LOADBOOL:
						Parse = $"{Locals[NA]} := {(NB != 0).ToString().ToLower()}";

						break;
					case LuaOpcode.LOADNIL:
						if (NA == NB)
							Parse = $"{Locals[NA]} := nil";
						else
							Parse = $"R({NA}) to R({NB}) := nil";

						break;
					case LuaOpcode.GETUPVAL:
						Parse = $"{Locals[NA]} := Upvalue[\"{Upvalues[NB]}\"]";

						break;
					case LuaOpcode.GETGLOBAL:
						Parse = $"{Locals[NA]} := Gbl[{GetKst(NB)}]";

						break;
					case LuaOpcode.GETTABLE:
						Parse = $"{Locals[NA]} := {Locals[NB]}[{RegOrConst(NC)}]";

						break;
					case LuaOpcode.SETGLOBAL:
						Parse = $"Gbl[{GetKst(NB)}] = {Locals[NA]}";

						break;
					case LuaOpcode.SETUPVAL:
						Parse = $"Upvalue[\"{Upvalues[NB]}\"] := {Locals[NA]}";

						break;
					case LuaOpcode.SETTABLE:
						Parse = $"{Locals[NA]}[{RegOrConst(NB)}] := {RegOrConst(NC)}";

						break;
					case LuaOpcode.NEWTABLE:
						Parse = $"{Locals[NA]} = Array : {NB}, Hash : {NC}";

						break;
					case LuaOpcode.SELF:
						Parse = $"{Locals[NA]} := {Locals[NB]}[{RegOrConst(NC)}]";

						break;
					case LuaOpcode.UNM:
					case LuaOpcode.NOT:
					case LuaOpcode.LEN:
						Parse = $"{Locals[NA]} := {SymbolOf(Instr.Opcode)}{Locals[NB]}";

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
						Parse = $"{Locals[NA]} := {RegOrConst(NB)}{SymbolOf(Instr.Opcode)}{RegOrConst(NC)}";

						break;
					case LuaOpcode.JMP:
						Parse = $"Jump {((NB > 0) ? "forward" : "back")} to PC(0x{Index + NB + 1:X2})";

						break;
					case LuaOpcode.TEST:
						LuaInstruct Next = Instrs[Index + 2];

						if (Next.Opcode == LuaOpcode.TEST)
							Parse = $"{Locals[NA]} {(NC == 0 ? "and" : "or")} PC(0x{Index + 2:X2})";
						else
							Parse = $"if not {Locals[NA]}";

						break;
					case LuaOpcode.TESTSET:
						LuaInstruct SetNext = Instrs[Index + 2];

						Parse = $"{Locals[NA]} = {Locals[NB]} {(NC == 0 ? "and" : "or")} PC(0x{Index + 2:X2})";

						break;
					case LuaOpcode.CALL:
						Parse = $"Call {Locals[NA]}";

						if (NB == 0)
							Parse += $", params R({NA + 1}) to top";
						else if (NB == 1)
							Parse += ", no params";
						else if (NB == 2)
							Parse += $", param {Locals[NA + 1]}";
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
						Parse = $"Tailcall {Locals[NA]}";

						break;
					case LuaOpcode.RETURN:
						if (NB == 1)
							Parse = "Return nothing";
						else if (NB == 2)
							Parse = $"Return {Locals[NA]}";
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
						Parse = $"List at {Locals[NA]}";

						if (NC == 0) {
							Index++;
							
							Parse = Parse
								+ ", as extended setlist\n"
								+ new string('\t', Level + 1)
								+ ".raw "
								+ Instrs[Index].Instr;
						}

						break;
					case LuaOpcode.CLOSE:
						Parse = $"Close upvalues R({NA})+";

						break;
					case LuaOpcode.CLOSURE:
						LuaProto Clos = Subprotos[NB];
						StringBuilder ProtoIfn = new StringBuilder();
						int Skips = 0;

						for (int Idx = 0; Idx < Clos.Nups; Idx++) {
							LuaInstruct Upval = Instrs[Index + Idx + 1];
							string Type;

							if (Upval.Opcode == LuaOpcode.MOVE)
								Type = $"; (0x{Index + Idx + 1:X2}) > Local upvalue ({Upval.B}) \"{Locals[Upval.B]}\"";
							else if (Upval.Opcode == LuaOpcode.GETUPVAL)
								Type = $"; (0x{Index + Idx + 1:X2}) > Upvalue ({Upval.B}) \"{Upvalues[Upval.B]}\"";
							else
								break;

							Skips++;

							ProtoIfn.AlignedAppend(Level + 1, true, Type);
						}

						if (Skips != 0)
							ProtoIfn.AppendLine();

						if ((Index + Skips + 1) < Instrs.Count) {
							LuaInstruct Namer = Instrs[Index + Skips + 1];
							string Name = Locals[NA];

							if (Namer.A == NA) {
								if (Namer.Opcode == LuaOpcode.SETGLOBAL)
									Name = GetKst(Namer.B);
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
							Parse = $"... ({NB - 1})";

						break;
					default:
						break;
				}

				if (String.IsNullOrEmpty(Parse))
					Raw = Raw.TrimEnd(' '); // Trim off excess
				else
					Raw = $"{Raw.PadRight(30)}; {Parse}";

				Text.AlignedAppend(Level, true, Raw);
			}
		}

		// Disassembler state : Complete
		string ReadProto(int Level) {
			int NumLocals = Proto.Stack;
			StringBuilder Text = new StringBuilder(Instrs.Count);

			Source = new StringBuilder();

			// This is done first so it helps out with the actual definitions
			DeclareInstructions(Level, Text);
			DeclareHeader(Level);
			DeclareProtoHeaders(Level);
			DeclareLocals(Level);
			DeclareUpvalues(Level);
			DeclareConstants(Level);
			DeclareProtos(Level);

			Source.AppendLine();
			Source.Append(Text);
			Source.AlignedAppend(Level, true, ".end\n");

			return Source.ToString();
		}

		public string GetSource() {
			return String.Format(".version {0}\n{1}", Proto.Version, ReadProto(0));
		}
	}
}