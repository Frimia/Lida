#define IS_TRY_CATCH

using Relua;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

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
		enum OutputType {
			COMPILE,
			ASSEMBLE,
			DISASSEMBLE
		}

		static DisReader LuReader = new DisReader();

		static byte[] Compile(string FileName) {
			var Proc = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "luac.exe",
					Arguments = $"-o {FileName}.out {FileName}",
					UseShellExecute = false,
					RedirectStandardOutput = false,
					CreateNoWindow = true
				}
			};

			Proc.Start();
			Proc.WaitForExit();

			return File.ReadAllBytes(FileName + ".out");
		}

		static byte[] AsSource(OutputType Type, byte[] Bytecode) { // This is a mess but readable ok
			LuaProto_S Prototype = new LuaProto_S(Bytecode);
			string Source;

			switch (Type) {
				case OutputType.COMPILE:
					//Prototype.StripDebug(true);
					Prototype.Analyze(true);

					return new LuaProto_D(Prototype).Dump();
				case OutputType.DISASSEMBLE:
					LuaDisassembly Disas = new LuaDisassembly(Prototype);

					Source = Disas.GetSource(0);

					break;
				default:
					throw new NotImplementedException();
			}

			return Data.RealASCII.GetBytes(Source);
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

		static byte[] FileBytes(string FileName) {
			byte[] Bytecode = File.ReadAllBytes(FileName);

			if (Bytecode.Length == 0)
				throw new Exception("File was empty");

			if (Bytecode[0] != 27)
				Bytecode = Compile(FileName);

			return Bytecode;
		}

		static void WriteLine(object Message, ConsoleColor Color = ConsoleColor.White, bool NewLine = true) {
			Console.ForegroundColor = Color;
			Console.Write(Message + (NewLine ? "\n" : ""));
			Console.ForegroundColor = ConsoleColor.Gray;
		}

		static void Commandlined(string Method, string Input, string Output) {
			OutputType Type;
			byte[] Files;

			switch (Method) {
				case "cpl":
					Type = OutputType.COMPILE;

					break;
				case "asm":
					Type = OutputType.ASSEMBLE;

					break;
				case "dis":
					Type = OutputType.DISASSEMBLE;

					break;
				default:
					throw new ArgumentException("Parameter not recognized", "Method");
			}

			if (Type == OutputType.ASSEMBLE) {
				string[] Lines = File.ReadAllLines(Input, Data.RealASCII);

				Files = LuReader.AsArray(Lines);
				WriteLine($"Bytecode size of {Files.LongLength} bytes", ConsoleColor.Yellow);
			}
			else {
				Files = FileBytes(Input);

				WriteLine($"Bytecode size of {Files.LongLength} bytes", ConsoleColor.Yellow);
				Files = AsSource(Type, Files);
			}

			ToFile(Files, Output);
		}

		static void Main(string[] Args) {
			int Numargs = Args.Length;
			int Iterator = 0;
			Stopwatch Watcher;

			if (Numargs == 0) {
				WriteLine("Rerudec created and developed by Rerumu");
				WriteLine("Options->");
				WriteLine("Cpl  - Compile a script", ConsoleColor.Gray);
				WriteLine("Asm  - Assemble a disassembled file to bytecode", ConsoleColor.Gray);
				WriteLine("Dis  - Disassemble a file to Lua assembly", ConsoleColor.Gray);

				return;
			}

			while (Numargs != 0) {
				if (Numargs == 1)
					throw new Exception("Missing args `In` and `Out`");
				else if (Numargs == 2)
					throw new Exception("Missing arg `Out`");

				Watcher = new Stopwatch(); // Remember to include System assembly if this errors
				Watcher.Start();

#if IS_TRY_CATCH
				try {
#endif
					Commandlined(Args[Iterator].ToLower(), Args[Iterator + 1], Args[Iterator + 2]);

					Watcher.Stop();

					WriteLine(String.Join(" ", Args[Iterator], Args[Iterator + 1], Args[Iterator + 2]), ConsoleColor.DarkCyan);
					WriteLine($"Operation took {Watcher.ElapsedMilliseconds}ms");
#if IS_TRY_CATCH
				}
				catch (Exception E) {
					Watcher.Stop();
					WriteLine(E.Message, ConsoleColor.Red);
					WriteLine(E.StackTrace, ConsoleColor.White);
				}
#endif
				Numargs -= 3;
				Iterator += 3;
			}
#if !IS_TRY_CATCH
			Console.ReadLine(); // Yield for testing
#endif
		}
	}
}