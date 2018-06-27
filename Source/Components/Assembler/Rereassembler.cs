using System;
using System.Collections.Generic;
using System.Text;
using SeeLua.Abstracted;
using static SeeLua.Abstracted.StaticsData;

namespace Lida {
	class DisReader {
		private struct Token {
			public enum Toktype : byte {
				LOCAL, UPVALUE, CONST, RAW,
				STACK, NUMPARAMS, NUPS, VARARG,
				FUNCTION, END, INSTRUCT,
				LITERAL, LINE, VERSION
			}

			public Toktype Type;
			public LuaOpcode Opcode;
			public LuaConstant Const;

			public override string ToString() {
				switch (Type) {
					case Toktype.INSTRUCT:
						return Opcode.ToString();
					case Toktype.LITERAL:
						return Const.ToString();
					case Toktype.LINE:
						return $"ln({(int) Const.Number})";
					default:
						return Type.ToString();
				}
			}
		}

		List<LuaProto> Protos;
		List<Token> Tokens;
		String[] Lines;

		string ParseStringTok(string Str) { // Nasty way of doing this, but I can't think of better
			StringBuilder String = new StringBuilder(Str.Length);
			bool Esc = false;

			for (int Idx = 0; Idx < Str.Length; Idx++) {
				char Strc = Str[Idx];

				if (Strc == '\\') {
					Esc = !Esc;

					if (Esc) {
						byte Skips = 0;
						byte Sz = 0;

						for (int Idz = 0; (Idz < 3) && ((Idx + Idz + 1) < Str.Length); Idz++) {
							char Leading = Str[Idx + Idz + 1];

							if (Char.IsDigit(Leading)) {
								Byte.TryParse(Leading.ToString(), out byte Content);

								Sz += (byte) (10 ^ (2 - Idz) * Content);

								Skips++;
							}
							else
								break;
						}

						if (Skips != 0) {
							String.Append(Convert.ToChar(Sz));

							Idx += Skips;
						}
						else {
							char Next = Str[++Idx];

							switch (Next) {
								case 'b':
									String.Append('\b');

									break;
								case 'n':
									String.Append('\n');

									break;
								case 'r':
									String.Append('\r');

									break;
								case 't':
									String.Append('\t');

									break;
								default:
									String.Append(Next);

									break;
							}
						}

						Esc = false;
					}
				}
				else {
					String.Append(Strc);

					Esc = false;
				}
			}

			return String.ToString();
		}

		void ParseTok(string Tok) {
			Token New = new Token();

			if (Tok.Length == 0)
				return;

			else if (Tok[0] == '.') {
				bool Success = Enum.TryParse(Tok.Substring(1), true, out New.Type);

				if (!Success)
					throw new Exception("Invalid type to \"" + Tok + "\"");
			}
			else if (Char.IsUpper(Tok[0])) {
				New.Type = Token.Toktype.INSTRUCT;

				Enum.TryParse(Tok, out New.Opcode);
			}
			else {
				LuaConstant Const;

				New.Type = Token.Toktype.LITERAL;

				if (Tok.StartsWith("(")) {
					New.Type = Token.Toktype.LINE;

					Const = Convert.ToDouble(Tok.Substring(1, Tok.Length - 2));
				}
				else if (Tok.StartsWith("\"")) {
					if (Tok.Length != 1)
						Const = ParseStringTok(Tok.Substring(1));
					else
						Const = "";
				}
				else if (Tok.StartsWith("-") || Tok.StartsWith(".") || Char.IsDigit(Tok[0])) {
					if (Tok.Equals("-"))
						Const = 0;
					else
						Const = Convert.ToDouble(Tok);
				}
				else if (Tok.Equals("true") || Tok.Equals("false"))
					Const = Convert.ToBoolean(Tok);
				else if (Tok.Equals("nil"))
					Const = LuaNil;
				else
					return;

				New.Const = Const;
			};

			Tokens.Add(New);
		}

		void SanitizeLns() {
			for (int Index = 0; Index < Lines.Length; Index++) {
				StringBuilder NewLine = new StringBuilder();
				string Line = Lines[Index] + '\n';
				bool Quo = false,
					Esc = false;

				for (int Idx = 0; Idx < Line.Length; Idx++) {
					char Tok = Line[Idx];

					if (!Quo && Char.IsWhiteSpace(Tok)) {
						ParseTok(NewLine.ToString());

						NewLine.Clear();

						continue;
					}
					else if (!Esc && Tok.Equals('"')) {
						if (Quo) {
							ParseTok(NewLine.ToString());

							NewLine.Clear();
						}
						else
							NewLine.Append(Tok);

						Quo = !Quo;

						continue;
					}
					else if (!Quo && Tok.Equals(';')) {
						ParseTok(NewLine.ToString());

						break;
					}

					if (!Esc)
						Esc = Tok.Equals('\\');
					else
						Esc = false;

					NewLine.Append(Tok);
				}
			}
		}

		void ParseLns() { // Should handle protos properly
			byte VersionId = 0;
			for (int Idx = 0; Idx < Tokens.Count; Idx++) {
				Token Tok = Tokens[Idx];
				LuaProto Proto = null;

				if (Protos.Count != 0) { // Checks stack of protos
					Proto = Protos[Protos.Count - 1];
				}

				switch (Tok.Type) {
					case Token.Toktype.LOCAL:
						LuaLocal NewLocal = new LuaLocal(
							Tokens[++Idx].Const.String,
							(int) Tokens[++Idx].Const.Number, // lazy conversion ik but it works
							(int) Tokens[++Idx].Const.Number
						);
						
						Proto.Locals.Add(NewLocal);

						break;
					case Token.Toktype.UPVALUE:
						Proto.Upvalues.Add(Tokens[++Idx].Const.String);

						break;
					case Token.Toktype.CONST:
						Proto.Consts.Add(Tokens[++Idx].Const);

						break;
					case Token.Toktype.RAW:
						Proto.Instructs.Add(new LuaInstruct((int) Tokens[++Idx].Const.Number));

						break;
					case Token.Toktype.STACK:
						Proto.Stack = (byte) Tokens[++Idx].Const.Number;

						break;
					case Token.Toktype.NUMPARAMS:
						Proto.Numparams = (byte) Tokens[++Idx].Const.Number;

						break;
					case Token.Toktype.NUPS:
						Proto.Nups = (byte) Tokens[++Idx].Const.Number;

						break;
					case Token.Toktype.VARARG:
						Proto.Vararg = (byte) Tokens[++Idx].Const.Number;

						break;
					case Token.Toktype.FUNCTION:
						LuaProto NewProto = Deserializer.ResolveProto(VersionId);

						NewProto.LineBegin = (int) Tokens[++Idx].Const.Number;
						NewProto.LineEnd = (int) Tokens[++Idx].Const.Number;

						NewProto.Name = Tokens[++Idx].Const.String;
						NewProto.Nups = 0;
						NewProto.Numparams = 0;
						NewProto.Vararg = 0;
						NewProto.Stack = 0;

						NewProto.Instructs = new List<LuaInstruct>();
						NewProto.Consts = new List<LuaConstant>();
						NewProto.Protos = new List<LuaProto>();
						NewProto.Locals = new List<LuaLocal>();
						NewProto.Upvalues = new List<string>();
						NewProto.Lines = new List<int>();

						if (Proto != null) {
							NewProto.Parent = Proto;

							Proto.Protos.Add(NewProto);
						}

						Protos.Add(NewProto);

						break;
					case Token.Toktype.END:
						if (Protos.Count > 1) {
							Protos.RemoveAt(Protos.Count - 1);
						}

						for (int Loc = 0; Loc < Proto.Locals.Count; Loc++) {
							LuaLocal Lvar = Proto.Locals[Loc];
							int Max = Math.Min(Lvar.Endpc, Proto.Instructs.Count - 1);
							int Min = Math.Min(Lvar.Startpc, Max);

							Lvar.Endpc = Max;
							Lvar.Startpc = Min;
						}

						break;
					case Token.Toktype.INSTRUCT:
						int A = (int) Tokens[++Idx].Const.Number,
							B = (int) Tokens[++Idx].Const.Number,
							C = (int) Tokens[++Idx].Const.Number;

						LuaInstruct Instr = new LuaInstruct {
							Opcode = Tok.Opcode,
							A = A,
							B = B,
							C = C
						};
						
						Proto.Instructs.Add(Instr);

						break;
					case Token.Toktype.LINE:
						Proto.Lines.Add((int) Tok.Const.Number);

						break;
					case Token.Toktype.VERSION:
						VersionId = (byte)Tokens[++Idx].Const.Number;

						break;
				}
			}
		}

		public byte[] AsBytecode(string[] Lns) {
			Protos = new List<LuaProto>();
			Tokens = new List<Token>();
			Lines = Lns;

			SanitizeLns();
			ParseLns();

			if (Protos.Count != 0) {
				LuaProto Top = Protos[0];

				return Top.Serialize();
			}
			else {
				throw new Exception("File could not be parsed");
			}
		}
	}
}
