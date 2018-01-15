using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

/* Developer Note
 *	This decompiler is in no way usable in its current condition,
 *	but you are welcome to use it via `soft` command.
 *	Excuse any bugs, as most of the debug code is still present.
 *	
 *	Side note: a few opcodes I still have not implemented
 *	Known bugs:
 *		The usage of the SELF opcode (for : calls)
 *		will cause Rerudec to error because of no
 *		implementation code for the register sets
 */

namespace Rerulsd {
	using Relua;
	using static Relua.Data;
	using static ReruData;
	using static LuaSoftData;

	static class LuaSoftData {
		public class StructKst {
			public enum Stype {
				FLUID,
				STRING, BOOLEAN,
				INTEGER, DOUBLE
			}

			public Stype Type;
			public int Integer;
			public bool Boolean;
			public double Double;
			public string String;

			public static implicit operator StructKst(string Data) =>
				new StructKst { Type = Stype.STRING, String = Data };

			public static implicit operator StructKst(bool Data) =>
				new StructKst { Type = Stype.BOOLEAN, Boolean = Data };

			public static implicit operator StructKst(int Data) =>
				new StructKst { Type = Stype.INTEGER, Integer = Data };

			public static implicit operator StructKst(double Data) =>
				new StructKst { Type = Stype.DOUBLE, Double = Data };

			public static implicit operator StructKst(LuaConstant Data) {
				switch (Data.Type) {
					case LuaType.NIL:
						return "nil";
					case LuaType.BOOL:
						return Data.Boolean;
					case LuaType.NUMBER:
						return Data.Double;
					case LuaType.FLUID:
						return new StructKst { String = Data.String, Type = Stype.FLUID };
					case LuaType.STRING:
						return Data.String;
					default:
						return string.Empty;
				}
			}

			public override string ToString() {
				switch (Type) {
					case Stype.FLUID:
						return String;
					case Stype.STRING:
						return "\"" + String + "\"";
					case Stype.BOOLEAN:
						return Boolean.ToString().ToLower();
					case Stype.INTEGER:
						return Integer.ToString();
					case Stype.DOUBLE:
						return Double.ToString();
					default:
						return string.Empty;
				}
			}
		}

		public class LuaStruct { // Used for information about tokens
			public int Result;
			public byte Depth;
			public bool Local;
			public LuaOpcode Op;
			public List<StructKst> Ksts;

			public LuaStruct(LuaOpcode Opcode) {
				Result = 0;
				Depth = 0;
				Local = false;
				Ksts = new List<StructKst>();
				Op = Opcode;
			}
			
			public bool Next() =>
				Ksts.Count != 0;

			public StructKst Get() {
				StructKst Kst = Ksts[Ksts.Count - 1];

				Ksts.RemoveAt(Ksts.Count - 1);

				return Kst;
			}

			public StructKst Set(StructKst Kst) {
				Ksts.Add(Kst);

				return Kst;
			}
		}
	}

	class LuaSoftDecompile : LuaResult {
		private List<LuaStruct> TokenList;
		private List<List<byte>> Registers;
		private bool[] Skips;
		private bool HasStart;
		private int Level;

		// Should maybe use these lists? Make them per part orrr?
		public LuaSoftDecompile(LuaProto Subject) {
			if (Subject != null)
				Set(Subject);
		}

		int WhereWrote(int Reg, int Find = -1) {
			int Position = (Find == -1) ? Registers.Count - 1 : Find;

			/* This is confusing, let me explain
			 * First, we check if the Kst exists (dunno if .NET would already check for us)
			 * Then, we check if it has written to the register we want
			 * and afterwards we make sure that register has not been skipped
			 */
			bool Found(List<byte> Kst) {
				return (Kst != null)
					&& Kst.Contains((byte) Reg)
					&& !Skips[Registers.IndexOf(Kst)];
			}

			return Registers.FindLastIndex(Position, Found);
		}
		
		void Write(int Sloc, int Reg) {
			if (TokenList.Count > Sloc) {
				LuaStruct L = TokenList[Sloc];

				L.Local = L.Local || (WhereWrote(Reg) == -1);
			}

			if (Registers[Sloc] == null)
				Registers[Sloc] = new List<byte>(1);

			Registers[Sloc].Add((byte) Reg);
		}

		void AsTokens() {
			if (Serial.NumArgs != 0) {
				Registers.Add(new List<byte>(Serial.NumArgs));

				for (int Index = 0; Index < Serial.NumArgs; Index++)
					Write(0, Index);
			}
			
			for (int Index = 0; Index < Instrs.Count; Index++) {
				LuaInstruct Instr = Instrs[Index];
				LuaStruct Struct = new LuaStruct(Instr.Opcode) {
					Result = Instr.A,
					Depth = (byte) Level
				};

				int A = Instr.A,
					B = Instr.B,
					C = Instr.C;

				if ((Index != 0) || (Serial.NumArgs == 0))
					Registers.Add(null);

				TokenList.Add(Struct);

				switch (Instr.Opcode) {
					case LuaOpcode.MOVE:
						Struct.Set(LocalAt(B)).Type = StructKst.Stype.FLUID;

						Write(Index, A);
						break;
					case LuaOpcode.LOADK:
						Struct.Set(Consts[B]);

						Write(Index, A);
						break;
					case LuaOpcode.LOADBOOL:
						Struct.Set(B != 0);

						if (C != 0)
							Skips[Index] = true;

						Write(Index, A);
						break;
					case LuaOpcode.LOADNIL:
						for (int NilIndex = B; NilIndex >= A; NilIndex--) {
							Struct.Set(LocalAt(NilIndex));

							Write(Index, NilIndex);
						}

						Struct.Set(B - A + 1);
						
						break;
					case LuaOpcode.GETUPVAL:
						Struct.Set(Upvalues[B]);

						Write(Index, A);
						break;
					case LuaOpcode.GETGLOBAL:
						Struct.Set(Consts[B]);

						Write(Index, A);
						break;
					case LuaOpcode.GETTABLE:
						Struct.Set(RegOrConst(C));
						Struct.Set(LocalAt(B));

						Write(Index, A);
						break;
					case LuaOpcode.SETGLOBAL:
						Struct.Set(Consts[B]);

						break;
					case LuaOpcode.SETUPVAL:
						Struct.Set(Upvalues[B]);

						break;
					case LuaOpcode.SETTABLE:
						Struct.Set(RegOrConst(C));
						Struct.Set(RegOrConst(B));

						break;
					case LuaOpcode.NEWTABLE:
						Write(Index, A);

						break;
					//case LuaOpcode.SELF:
					//	Write(Index, A);
					//	Write(Index, A + 1);

					//	break;
					case LuaOpcode.ADD:
					case LuaOpcode.SUB:
					case LuaOpcode.MUL:
					case LuaOpcode.DIV:
					case LuaOpcode.MOD:
					case LuaOpcode.POW:
						Struct.Set(RegOrConst(C));
						Struct.Set(RegOrConst(B)); // These are reversed to preserve order

						Write(Index, A);
						break;
					case LuaOpcode.UNM:
					case LuaOpcode.NOT:
					case LuaOpcode.LEN:
						Struct.Set(RegOrConst(B));

						Write(Index, A);
						break;
					case LuaOpcode.CONCAT:
						for (int Idx = C; Idx >= B; Idx--)
							Struct.Set(LocalAt(Idx));

						Write(Index, A);
						break;
					/*case LuaOpcode.JMP:
						break;
					case LuaOpcode.EQ:
						break;
					case LuaOpcode.LT:
						break;
					case LuaOpcode.LE:
						break;
					case LuaOpcode.TEST:
						break;
					case LuaOpcode.TESTSET:
						break;*/
					case LuaOpcode.CALL:
						int Called = WhereWrote(A);

						if (B == 0) // Arguments
							Struct.Set(-1);
						else if (B == 1)
							Struct.Set(0);
						else {
							int Max = A + B - 1;
							int[] SkipA = new int[Max - A]; // Placeholder

							for (int Idx = Max; Idx > A; Idx--) {
								int LastArg = WhereWrote(Idx);
								
								SkipA[Idx - A - 1] = LastArg;
								Struct.Set(LastArg);
							}

							for (int Idx = 0; Idx < SkipA.Length; Idx++)
								Skips[SkipA[Idx]] = true; // Set all unset arguments

							Struct.Set(B - 1);
						}

						if (C == 0) // Return registers
							Struct.Set(-1);
						else if (C == 1)
							Struct.Set(0);
						else {
							int Max = A + C - 2;

							for (int Idx = Max; Idx >= A; Idx--) {
								Struct.Set(LocalAt(Idx));

								Write(Index, Idx);
							}

							Struct.Set(C - 1);
						}

						Skips[Called] = true;
						Struct.Set(Called); // TODO: SELF instruction handling

						break;
//					case LuaOpcode.TAILCALL:
//						break;
					case LuaOpcode.RETURN:
						if (B > 1) {
							for (int Idx = A + B - 2; Idx >= A; Idx--)
								Struct.Set(LocalAt(Idx));
						}
						else if (B == 0)
							Struct.Set(LocalAt(A));

						break;
					case LuaOpcode.FORLOOP:
						Struct.Depth = (byte) --Level;

						break;
					case LuaOpcode.FORPREP:
						int Fr = WhereWrote(Struct.Result),
							Sc = WhereWrote(Struct.Result + 1),
							Th = WhereWrote(Struct.Result + 2);

						Skips[Fr] = true;
						Skips[Sc] = true;
						Skips[Th] = true;

						Struct.Set(Th);
						Struct.Set(Sc);
						Struct.Set(Fr);

						Level++;

						break;
					//case LuaOpcode.TFORLOOP:
					//	break;
					/*case LuaOpcode.SETLIST:
						break;
					case LuaOpcode.CLOSE:
						break;*/
					case LuaOpcode.CLOSURE:
						LuaProto Closure = Wrappeds[B];

						int PastClo = Index + Closure.NumUpvals + 1;
						int Def = -1;

						if (PastClo < Instrs.Count) {
							LuaInstruct NextClo = Instrs[PastClo];
							LuaOpcode NextOp = NextClo.Opcode;

							if (NextClo.A == A) {
								if ((NextOp == LuaOpcode.SETGLOBAL) || (NextOp == LuaOpcode.SETUPVAL))
									Def = PastClo - Index;
							}
						}

						if (Def == -1)
							Write(Index, A);

						Struct.Set(Def);
						Struct.Set(B);

						break;
					case LuaOpcode.VARARG:
						if (B == 1)
							goto default;
						else if (B > 1) {
							int Num = A + B - 2;

							for (int Idx = Num; Idx >= A; Idx--) {
								Struct.Set(LocalAt(Idx));

								Write(Index, Idx);
							}
						}

						break;
					default:
						Struct.Op = LuaOpcode.NULL;
						Struct.Set($"/{Index}/({Serial.Lines[Index]}) Error at {Instr}");

						break;
				}
			}
		}
		
		private string ParseEnd(int Index) {
			string Result;

			HasStart = false;
			Result = ParseTok(Index).ToString();
			HasStart = true;

			return Result;
		}

		private void DefineEq(StringBuilder Str, LuaStruct Lu) {
			if (HasStart) { // Ugly way of cutting things in half
				Str.Append(LocalAt(Lu.Result)); // Helper function for defining
				Str.Append(" = ");
			}
		}

		private StringBuilder ParseTok(int Index) {
			StringBuilder Appendee = new StringBuilder(32); // Start off well
			LuaStruct Struct = TokenList[Index];
			string Location = LocalAt(Struct.Result);

			if (Struct.Local && HasStart)
				Appendee.Append("local ");

			switch (Struct.Op) {
				case LuaOpcode.MOVE:
				case LuaOpcode.LOADK:
				case LuaOpcode.LOADBOOL:
					DefineEq(Appendee, Struct);
					Appendee.Append(Struct.Get());

					break;
				case LuaOpcode.LOADNIL:
					if (!HasStart) {
						Appendee.Append("nil");

						break;
					}

					int Nils = Struct.Get().Integer;
					
					for (int Idx = 0; Idx < Nils; Idx++) {
						Appendee.Append(Struct.Get().String);

						if ((Idx + 1) != Nils)
							Appendee.Append(", ");
					}

					if (!Struct.Local) {
						Appendee.Append(" = ");

						for (int Idx = 0; Idx < Nils; Idx++) {
							Appendee.Append("nil");

							if ((Idx + 1) != Nils)
								Appendee.Append(", ");
						}
					}

					break;
				case LuaOpcode.GETUPVAL:
				case LuaOpcode.GETGLOBAL:
					DefineEq(Appendee, Struct);
					Appendee.Append(Struct.Get().String);

					break;
				case LuaOpcode.GETTABLE:
					StructKst LGet = Struct.Get();
					StructKst RGet = Struct.Get();

					DefineEq(Appendee, Struct);

					if ((RGet.Type == StructKst.Stype.STRING) && Regex.IsMatch(RGet.String, @"^[a-zA-Z0-9_]+$"))
						Appendee.Append($"{LGet.String}.{RGet.String}");
					else
						Appendee.Append($"{LGet.String}[{RGet}]");

					break;
				case LuaOpcode.SETGLOBAL:
				case LuaOpcode.SETUPVAL:
					Appendee.Append(Struct.Get().String);
					Appendee.Append(" = ");
					Appendee.Append(Location);

					break;
				case LuaOpcode.SETTABLE:
					StructKst LSet = Struct.Get();
					StructKst RSet = Struct.Get();

					if ((LSet.Type == StructKst.Stype.STRING) && Regex.IsMatch(LSet.String, @"^[a-zA-Z0-9_]+$"))
						Appendee.Append($"{Location}.{LSet.String} = {RSet}");
					else
						Appendee.Append($"{Location}[{LSet}] = {RSet}");

					break;
				case LuaOpcode.NEWTABLE:
					DefineEq(Appendee, Struct);
					Appendee.Append("{}");

					break;
				//case LuaOpcode.SELF:
				//	break;
				case LuaOpcode.ADD:
				case LuaOpcode.SUB:
				case LuaOpcode.MUL:
				case LuaOpcode.DIV:
				case LuaOpcode.MOD:
				case LuaOpcode.POW:
					DefineEq(Appendee, Struct);
					Appendee.Append(Struct.Get());
					Appendee.Append(SymbolOf(Struct.Op));
					Appendee.Append(Struct.Get());

					break;
				case LuaOpcode.UNM:
				case LuaOpcode.NOT:
				case LuaOpcode.LEN:
					DefineEq(Appendee, Struct);
					Appendee.Append(SymbolOf(Struct.Op));
					Appendee.Append(Struct.Get());

					break;
				case LuaOpcode.CONCAT:
					DefineEq(Appendee, Struct);
					Appendee.Append(Struct.Get().String);

					while (Struct.Next())
						Appendee.Append($" .. {Struct.Get().String}");

					break;
				/*case LuaOpcode.JMP:
					break;
				case LuaOpcode.EQ:
					break;
				case LuaOpcode.LT:
					break;
				case LuaOpcode.LE:
					break;
				case LuaOpcode.TEST:
					break;
				case LuaOpcode.TESTSET:
					break;*/
				case LuaOpcode.CALL:
				//case LuaOpcode.TAILCALL:
					string Name = ParseEnd(Struct.Get().Integer);
					int Ntrn, Narg;

					Ntrn = Struct.Get().Integer;

					if (Ntrn != 0 && HasStart) {
						for (int Idx = 0; Idx < Ntrn; Idx++) {
							Appendee.Append(Struct.Get().String);

							if ((Idx + 1) != Ntrn)
								Appendee.Append(", ");
						}

						Appendee.Append(" = ");
					}

					Narg = Struct.Get().Integer;

					Appendee.Append(Name);
					Appendee.Append('(');

					for (int Idx = 0; Idx < Narg; Idx++) {
						Appendee.Append(ParseEnd(Struct.Get().Integer));

						if ((Idx + 1) != Narg)
							Appendee.Append(", ");
					}

					Appendee.Append(')');

					break;
				case LuaOpcode.RETURN:
					if ((Index + 1) != TokenList.Count) {
						Appendee.Append("return");

						while (Struct.Next()) {
							Appendee.Append(' ' + Struct.Get().String);

							if (Struct.Next())
								Appendee.Append(",");
						}
					}

					break;
				case LuaOpcode.FORLOOP:
					Appendee.Append("end");

					break;
				case LuaOpcode.FORPREP:
					string Loopn = LocalAt(Struct.Result + 3);
					string FIndex = ParseEnd(Struct.Get().Integer),
						FLimit = ParseEnd(Struct.Get().Integer),
						FStep = ParseEnd(Struct.Get().Integer);
					// TODO: Append finishing lines
					Appendee.Append($"for {Loopn} = {FIndex}, {FLimit}, {FStep} do");

					break;
				/*case LuaOpcode.TFORLOOP:
					break;
				case LuaOpcode.SETLIST:
					break;
				case LuaOpcode.CLOSE:
					break;*/
				case LuaOpcode.CLOSURE:
					LuaProto Wrapped = Wrappeds[Struct.Get().Integer];
					LuaSoftDecompile Soft = new LuaSoftDecompile(Wrapped);
					int UpvalsNum = Wrapped.NumUpvals;
					int Def = Struct.Get().Integer;
					
					if (Def != -1) {
						LuaStruct NextClo = TokenList[Index + Def];

						Location = NextClo.Get().String;
						Appendee.Clear();

						Skips[Index + Def] = true;
					}

					for (int UpvalsAfter = 0; UpvalsAfter < UpvalsNum; UpvalsAfter++)
						Skips[Index + UpvalsAfter + 1] = true; // Skip upval related stuff

					if (HasStart)
						Appendee.Append($"function {Location}(");
					else
						Appendee.Append("function("); // Function anonymous special case

					for (int NumArg = 0; NumArg < Wrapped.NumArgs; NumArg++) {
						if (NumArg != 0)
							Appendee.Append(", ");

						Appendee.Append(Soft.LocalAt(NumArg));
					}

					if (Wrapped.Vararg != 0) {
						if (Wrapped.NumArgs != 0)
							Appendee.Append(", ");

						Appendee.Append("...");
					}

					Appendee.AppendLine($") -- {Wrapped.ToString()}");
					Appendee.AppendLine(Soft.GetSource(Level + 1));
					Appendee.ReruAppend(Level, false, "end");

					break;
				case LuaOpcode.VARARG:
					if (Struct.Next()) {
						while (true) {
							Appendee.Append(Struct.Get().String);

							if (Struct.Next())
								Appendee.Append(", ");
							else
								break;
						}

						Appendee.Append(" = ...");
					}

					break;
				default:
					Appendee.Append($"--<*> {Struct.Get().String}");

					break;
			}

			return Appendee;
		}

		void DoTokens() {
			StringBuilder Appendee;

			for (int Index = 0; Index < TokenList.Count; Index++) {
				if (Skips[Index])
					continue;

				Appendee = ParseTok(Index);

				if (Appendee.Length != 0)
					Result.ReruAppend(TokenList[Index].Depth, true, Appendee.ToString());
			}
		}

		public override string GetSource(int Srclevel) {
			TokenList = new List<LuaStruct>(Instrs.Count);
			Registers = new List<List<byte>>(Serial.Stack);
			HasStart = true;
			Result = new StringBuilder();
			Skips = MakeArray(false, Instrs.Count);
			Level = Srclevel;

			AsTokens();
			DoTokens();

			if (Result.Length != 0)
				Result.Remove(0, 1); // Newline at start

			return Result.ToString();
		}
	}
}
