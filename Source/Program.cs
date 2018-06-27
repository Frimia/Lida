#define IS_TRY_CATCH

using System;
using System.Diagnostics;
using System.IO;
using SeeLua.Abstracted;

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
namespace Lida {
	static class Program { // This is for testing stuff, just loads in some bytecode
		static DisReader LuReader = new DisReader();

		static byte Mode = 2;
		static bool Strip = false;
		static bool Rebuild = false;
		static string OutputName = null;

		static byte[] Compile(string FileName) {
			byte[] Code = File.ReadAllBytes(FileName);

			if ((Code.Length != 0) && (Code[0] == 27))
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

			if (File.Exists(FileName + ".tempout")) {
				Code = File.ReadAllBytes(FileName + ".tempout");

				File.Delete(FileName + ".tempout");
			}
			else {
				throw new Exception("File could not be compiled");
			}

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
		}
		
		static void Execute(string FileName) {
			byte[] Result;
			byte[] Bytecode;
			LuaProto Proto;

			if (Mode == 1)
				Bytecode = LuReader.AsBytecode(File.ReadAllLines(FileName, StaticsData.EightBit));
			else
				Bytecode = Compile(FileName);

			Proto = Deserializer.Resolve(Bytecode).GetProto();

			if (Strip) {
				Proto.StripDebug(true);
			}
			/*
			if (Rebuild) {
				Proto.Cascade(true);
				Proto.Repair(true);
			}
			*/

			switch (Mode) {
				case 0:
				case 1:
					Result = Proto.Serialize();

					break;
				case 2:
					LuaDisassembly Disas = new LuaDisassembly(Proto);

					Result = StaticsData.EightBit.GetBytes(Disas.GetSource());

					break;
				default:
					throw new ArgumentOutOfRangeException("Mode");
			}

			WriteLine($"Size - {Result.Length} bytes ({FileName})", ConsoleColor.Yellow);

			ToFile(Result, OutputName ?? (FileName + ".out"));
		}

		static void HandleError(string Input) {
			Stopwatch Watcher = new Stopwatch();
			bool Executed = false;

#if IS_TRY_CATCH
			try {
#endif
				Watcher.Start();

				Execute(Input);
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
								WriteLine("Lida Test Build - Copyright (c) 2018 Rerumu");

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
				WriteLine("-m [mode]   : Sets the mode, Compile = 0, Assemble = 1, Disassemble = 2");
				WriteLine("-s          : Strips debug data from result");
				WriteLine("-r          : Rebuilds debug data onto result (nyi)");
			}

			Console.ResetColor();
#if !IS_TRY_CATCH
			Console.ReadLine();
#endif
		}
	}
}