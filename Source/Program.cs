#define IS_TRY_CATCH

using Relua;
using System;
using System.Diagnostics;
using System.IO;

/* > Rerumu
 * Developer note:
 *	Everything here is subject to change,
 *	since it's all one big experiment it'll all be
 *	very unstable and should be expected to bug out randomly,
 *	I did a lot of stupid things in source but they worked oh well,
 *	and I'll keep doing more stupid things anyways to test
 *
 *	implicit owner rights @ Rerumu
 *	source credit @ Rerumu
 *	dependencies
 *		* luac.c in PATH
 *		* ReluaCore
 */

// Program.cs : Only makes usage of the Ui and compiling, Rerude itself is only dependant on the core
namespace Rerulsd {
	static class Program { // This is for testing stuff, just loads in some bytecode
		static DisReader LuReader = new DisReader();

		static byte Mode = 2;
		static bool Strip = false;
		static bool Rebuild = false;
		static string OutputName = null;

		static byte[] Compile(string FileName) {
			byte[] Code = File.ReadAllBytes(FileName);

			if ((Code.Length == 0) || (Code[0] == 27))
				return Code;

			var Proc = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "luac.exe",
					Arguments = $"-o {FileName}.tempout {FileName}",
					UseShellExecute = false,
					RedirectStandardOutput = false,
					CreateNoWindow = true
				}
			};

			Proc.Start();
			Proc.WaitForExit();

			Code = File.ReadAllBytes(FileName + ".tempout");
			File.Delete(FileName + ".tempout");

			return Code;
		}

		static void ToFile(byte[] Content, string Path) {
			if (Content.Length == 0) {
				WriteLine("Failed to save empty file", ConsoleColor.Red);

				return;
			}

			FileStream Dump = File.Create(Path, Content.Length);

			Dump.Write(Content, 0, Content.Length);
			Dump.Close();
		}

		static void WriteLine(object Message, ConsoleColor Color = ConsoleColor.Gray, bool NewLine = true) {
			Console.ForegroundColor = Color;
			Console.Write(Message + (NewLine ? "\n" : ""));
			Console.ForegroundColor = ConsoleColor.Gray;
		}

		static void Assemble(string FileName) {
			string[] Lines = File.ReadAllLines(FileName, Data.RealASCII);
			byte[] Result = LuReader.AsArray(Lines);

			ToFile(Result, OutputName ?? (FileName + ".out"));
		}

		static void Execute(string FileName) {
			byte[] Result;
			byte[] Bytecode = Compile(FileName);
			LuaProto_S Proto = new LuaProto_S(Bytecode);

			if (Strip)
				Proto.StripDebug(true);

			if (Rebuild)
				Proto.Analyze(true);

			WriteLine($"Size - {Bytecode.Length} bytes", ConsoleColor.Yellow);

			switch (Mode) {
				case 0:
					Result = Bytecode;

					break;
				case 2:
					LuaDisassembly Disas = new LuaDisassembly(Proto);

					Result = Data.RealASCII.GetBytes(Disas.GetSource(0));

					break;
				default:
					throw new ArgumentOutOfRangeException("Mode");
			}

			ToFile(Result, OutputName ?? (FileName + ".out"));
		}

		static void HandleError(string Input) {
			Stopwatch Watcher = new Stopwatch();
			bool Executed = false;

#if IS_TRY_CATCH
			try {
#endif
				Watcher.Start();

				if (Mode != 1)
					Execute(Input);
				else
					Assemble(Input);

				Executed = true;
#if IS_TRY_CATCH
			}
			catch (Exception E) {
				WriteLine(E.Message, ConsoleColor.Red);
				WriteLine(E.StackTrace, ConsoleColor.White);
			}
#endif
			Watcher.Stop();

			if (Executed)
				WriteLine($"Elapsed - {Watcher.ElapsedMilliseconds}ms", ConsoleColor.DarkYellow);

#if !IS_TRY_CATCH
			Console.ReadLine();
#endif
		}

		static void Main(string[] Args) {
			int Numargs = Args.Length;
			bool Version = false;

			if (Numargs != 0) {
				for (int Idx = 0; Idx < Numargs; Idx++) {
					string Arg = Args[Idx];

					switch (Arg) {
						case "-v":
							if (!Version) {
								WriteLine("Lida Test Build - Copyright (c) 2017 Rerumu");

								Version = true;
							}

							break;
						case "-o":
							OutputName = Args[++Idx];

							break;
						case "-m":
							Mode = Byte.Parse(Args[++Idx]);

							break;
						case "-s":
							Strip = !Strip;

							break;
						case "-r":
							Rebuild = !Rebuild;

							break;
						default:
							HandleError(Arg);

							break;
					}
				}
			}
			else {
				WriteLine("Lida options;", ConsoleColor.White);
				WriteLine("-v          : List current version");
				WriteLine("-o [output] : Sets the output destination");
				WriteLine("-m [mode]   : Sets the mode, Compile = 0, Disassemble = 1, Assemble = 2");
				WriteLine("-s          : Strips debug data from result");
				WriteLine("-r          : Rebuilds debug data onto result");
			}
		}
	}
}