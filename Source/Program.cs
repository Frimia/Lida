#define IS_TRY_CATCH

using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using Relua;
using NLua;

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
 *		* NLua
 *		* ReluaCore
 */

// Program.cs : Only makes usage of commandline and NLua, Rerude itself is only dependant on Components
namespace Rerulsd {
	static class Program { // This is for testing stuff, just loads in some bytecode
		enum OutputType {
			COMPILE,
			ASSEMBLE,
			DISASSEMBLE
		}

		static DisReader LuReader = new DisReader();
		static Lua Parser = new Lua();
		const long Version = 0x003A;

		static byte[] AsSource(OutputType Type, byte[] Bytecode) { // This is a mess but readable ok
			LuaProto_S Prototype = new LuaProto_S(Bytecode);
			string Source;

			switch (Type) {
				case OutputType.COMPILE:
					return Bytecode;
				case OutputType.DISASSEMBLE:
					LuaDisassembly Disas = new LuaDisassembly(Prototype);

					Source = Disas.GetSource(0);

					break;
				default:
					throw new NotImplementedException();
			}

			return Encoding.UTF8.GetBytes(Source);
		}

		static void ToFile(byte[] Content, string Path) {
			FileStream Dump = File.Create(Path, Content.Length);

			Dump.Write(Content, 0, Content.Length);
			Dump.Close();

			WriteLine("File saved", ConsoleColor.Green);
		}

		static byte[] FileBytes(string FileName, bool IsSource) {
			string Source;
			string NameOf;
			byte[] Bytecode;

			if (IsSource) { // Did we pass the actual code?
				NameOf = "Rerude";
				Source = FileName;
			}
			else {
				NameOf = FileName;
				Bytecode = File.ReadAllBytes(FileName);

				if (Bytecode[0] == 27) {
					WriteLine("File detected as bytecode", ConsoleColor.Yellow);

					return Bytecode;
				}
				else {
					StringBuilder Sobuild = new StringBuilder(Bytecode.Length);

					for (long Idx = 0; Idx < Bytecode.Length; Idx++)
						Sobuild.Append((char) Bytecode[Idx]);

					Source = Sobuild.ToString();
				}
			}

			Parser.DoString($@"
				local Ran, Error = loadstring([===[{Source}]===], [[{NameOf}]]);

				if Ran then
					Result = string.dump(Ran);
				else
					error(Error);
				end;
			");

			Source = Parser["Result"] as string;

			if (Source == null)
				throw new Exception("Failed to load bytecode");
			else
				Bytecode = new byte[Source.Length];

			for (int Idx = 0; Idx < Source.Length; Idx++)
				Bytecode[Idx] = ((byte) Source[Idx]);

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
				Files = FileBytes(Input, false);

				WriteLine($"Bytecode size of {Files.LongLength} bytes", ConsoleColor.Yellow);
				Files = AsSource(Type, Files);
			}

			WriteLine("Saving file result", ConsoleColor.Yellow);
			ToFile(Files, Output);
		}

		static void Main(string[] Args) {
			int Numargs = Args.Length;
			Stopwatch Watcher;

			if (Numargs <= 0)
				throw new Exception("Missing args `Mode`, `In` and `Out`");
			else if (Numargs == 1) {
				if (Args[0].Equals("help", StringComparison.CurrentCultureIgnoreCase)) {
					WriteLine("Rerudec created and developed by Rerumu");
					WriteLine("Credits to creators of NLua for my usage of their compiler here");
					WriteLine("Options->");
					WriteLine("Cpl  - Compile a script", ConsoleColor.Gray);
					WriteLine("Asm  - Assemble a disassembled file to bytecode", ConsoleColor.Gray);
					WriteLine("Dis  - Disassemble a file to Lua assembly", ConsoleColor.Gray);
					WriteLine("Help - This message", ConsoleColor.Gray);
					WriteLine("NOTE: NLua is used to compile code, and without it, Rerudec works only on 5.1 bytecode", ConsoleColor.Gray);

					return;
				}
				else
					throw new Exception("Missing args `In` and `Out`");
			}
			else if (Numargs == 2)
				throw new Exception("Missing arg `Out`");
			else if (Numargs > 3)
				throw new Exception("Too many args to Rerude");

			Watcher = new Stopwatch();
			Watcher.Start();

			try {
				Commandlined(Args[0].ToLower(), Args[1], Args[2]);

				Watcher.Stop();
				WriteLine($"Operation took {Watcher.ElapsedMilliseconds}ms");
			}
			catch (Exception E) {
				Watcher.Stop();
				WriteLine(E.Message, ConsoleColor.Red);
				WriteLine(E.StackTrace, ConsoleColor.White);
			}
		}
	}
}