﻿using Relua;
using System;
using System.Collections.Generic;
using System.Text;
using static Relua.Data;

namespace Rerulsd {
	class DisReader {
		private struct Token {
			public enum Toktype {
				LOCAL, UPVALUE, CONST, RAW,
				STACK, NUMPARAMS, NUPS, VARARG,
				FUNCTION, END, INSTRUCT,
				LITERAL, LINE
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
						return $"ln({(int) Const.Double})";
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

						for (int Idz = 0; Idz < 3; Idz++) {
							char Leading = Str[Idx + Idz + 1];

							if (Char.IsDigit(Leading)) {
								Sz += (byte) (10 ^ (2 - Idz) * Convert.ToByte(Leading));

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
							char Next = Str[Idx + 1];

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
					Const = LuaNilConstant;
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
			for (int Idx = 0; Idx < Tokens.Count; Idx++) {
				Token Tok = Tokens[Idx];
				LuaProto Proto = null;

				if (Protos.Count != 0)
					Proto = Protos[Protos.Count - 1];

				switch (Tok.Type) {
					case Token.Toktype.LOCAL:
						LuaLocal NewLocal = new LuaLocal {
							Startpc = (int) Tokens[++Idx].Const.Double,
							Endpc = (int) Tokens[++Idx].Const.Double,
							Name = Tokens[++Idx].Const.String,
							Arg = false
						};

						NewLocal.Startpc = Math.Min(NewLocal.Startpc, Proto.Instructs.Count);
						NewLocal.Endpc = Math.Min(NewLocal.Endpc, Proto.Instructs.Count);

						Proto.Locals.Add(NewLocal);

						break;
					case Token.Toktype.UPVALUE:
						Proto.Upvalues.Add(Tokens[++Idx].Const.String);

						break;
					case Token.Toktype.CONST:
						Proto.Constants.Add(Tokens[++Idx].Const);

						break;
					case Token.Toktype.RAW:
						Proto.Instructs.Add(new LuaInstruct((int) Tokens[++Idx].Const.Double));

						break;
					case Token.Toktype.STACK:
						Proto.Stack = (byte) Tokens[++Idx].Const.Double;

						break;
					case Token.Toktype.NUMPARAMS:
						Proto.NumArgs = (byte) Tokens[++Idx].Const.Double;

						break;
					case Token.Toktype.NUPS:
						Proto.NumUpvals = (byte) Tokens[++Idx].Const.Double;

						break;
					case Token.Toktype.VARARG:
						Proto.Vararg = (byte) Tokens[++Idx].Const.Double;

						break;
					case Token.Toktype.FUNCTION:
						LuaProto NewProto = new LuaProto().Set(null);

						NewProto.Defined = new int[2] {
							(int) Tokens[++Idx].Const.Double,
							(int) Tokens[++Idx].Const.Double
						};

						NewProto.Name = Tokens[++Idx].Const.String;
						NewProto.NumUpvals = 0;
						NewProto.NumArgs = 0;
						NewProto.Vararg = 0;
						NewProto.Stack = 0;

						if (Proto != null) {
							NewProto.Parent = Proto;

							Proto.Protos.Add(NewProto);
						}

						Protos.Add(NewProto);

						break;
					case Token.Toktype.END:
						if (Protos.Count > 1)
							Protos.RemoveAt(Protos.Count - 1);

						break;
					case Token.Toktype.INSTRUCT:
						int A = (int) Tokens[++Idx].Const.Double,
							B = (int) Tokens[++Idx].Const.Double,
							C = (int) Tokens[++Idx].Const.Double;

						LuaInstruct Instr = new LuaInstruct {
							Opcode = Tok.Opcode,
							A = A,
							B = B,
							C = C
						};
						
						Proto.Instructs.Add(Instr);

						break;
					case Token.Toktype.LINE:
						Proto.Lines.Add((int) Tok.Const.Double);

						break;
				}
			}
		}

		public byte[] AsArray(string[] Lns) {
			Protos = new List<LuaProto>();
			Tokens = new List<Token>();
			Lines = Lns;

			SanitizeLns();
			ParseLns();

			if (Protos.Count != 0)
				return new LuaProto_D(Protos[0]).Dump();
			else
				throw new Exception("File could not be parsed");
		}
	}
}
