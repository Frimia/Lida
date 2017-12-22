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
 *	source credit @ Rerumu
 *
 *	other dependencies
 *		* NLua
 *		* ReluaCore
 */

// Program.cs : Only makes usage of the Ui and NLua, Rerude itself is only dependant on Rerudec.cs and ReluaCore
namespace Rerulsd {
	static class Program { // This is for testing stuff, just loads in some bytecode
		enum OutputType {
			COMPILE, // Only available because of NLua
			ASSEMBLE,
			DISASSEMBLE,
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
			// Talk about cutting corners
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

			for (int Idx = 0; Idx < Source.Length; Idx++) // I know this could have been done a better way
				Bytecode[Idx] = ((byte) Source[Idx]);

			return Bytecode;
		}

		static void WriteLine(object Message, ConsoleColor Color = ConsoleColor.White, bool NewLine = true) {
			Console.ForegroundColor = Color;
			Console.Write(Message + (NewLine ? "\n" : ""));
			Console.ForegroundColor = ConsoleColor.Gray;
		}

		static void Interface() {
			OutputType Type = OutputType.DISASSEMBLE;
			bool Running = true;
			string Command;
			string Input;
			string Output;

			Console.Title = $"Rerude {Version:x4}";
			Console.WindowWidth = Console.LargestWindowWidth / 2;
			Console.WindowHeight = Console.LargestWindowHeight / 2;
			
			WriteLine("Please input a method, an input filename, and output filename", ConsoleColor.Magenta);
			WriteLine("asm    : Reassembles a disassembled file into bytecode");
			WriteLine("cpl    : Source compiling");
			WriteLine("dis    : Source disassembly");
			WriteLine("soft   : Soft decompiler (not yet implemented)");
			WriteLine("credit : Shows credits");
			WriteLine("clear  : Clear the console output");
			WriteLine("//////////////////////////////\n", ConsoleColor.Red);

			Startup:
			WriteLine("/method/> ", ConsoleColor.DarkCyan, false);
			Command = Console.ReadLine().Trim(new char[] { ' ', '\t' });

			if (Command.Equals("clear")) {
				Console.Clear();

				goto Startup;
			}
			else if (Command.Equals("credit")) {
				WriteLine("< Credits >", ConsoleColor.Green);
				WriteLine("Credits to Rerumu for the software and underlying library", ConsoleColor.DarkGray);
				WriteLine("Special thanks to creators of NLua for its usage here", ConsoleColor.DarkGray);
				WriteLine("Other credits to the internet for Lua related info", ConsoleColor.DarkGray);

				goto Startup;
			}

			WriteLine("/input/> ", ConsoleColor.DarkCyan, false);
			Input = Console.ReadLine().Trim(new char[] { ' ', '\t' });

			WriteLine("/output/> ", ConsoleColor.DarkCyan, false);
			Output = Console.ReadLine().Trim(new char[] { ' ', '\t' });

			while (true) {
				switch (Command) {
					case "asm":
						WriteLine("< Reassembly >", ConsoleColor.Cyan);
						WriteLine("Reassembling file...", ConsoleColor.Yellow);

						ToFile(LuReader.AsArray(Input), Output);

						Console.Beep();

						break;
					case "cpl":
						Type = OutputType.COMPILE;

						goto case "last";
					case "dis":
						Type = OutputType.DISASSEMBLE;

						goto case "last";
					case "soft":
						throw new NotImplementedException();
					case "last":
						byte[] Files;
						
						WriteLine($"< Rerude Debugger >", ConsoleColor.Cyan);
#if IS_TRY_CATCH
						try {
#endif
							Stopwatch Watcher = new Stopwatch();
							Watcher.Start();

							WriteLine("Retrieving bytecode", ConsoleColor.Yellow);
							Files = FileBytes(Input, false);

							WriteLine($"Bytecode size of {Files.LongLength} bytes", ConsoleColor.Yellow);
							Files = AsSource(Type, Files);

							WriteLine("Saving file result", ConsoleColor.Yellow);
							ToFile(Files, Output);

							Watcher.Stop();

							WriteLine($"Operation took {Watcher.ElapsedMilliseconds}ms", ConsoleColor.Green);

							Console.Beep();
#if IS_TRY_CATCH
						}
						catch (Exception E) {
							WriteLine("An error occurred", ConsoleColor.Red);
							WriteLine(E.Message, ConsoleColor.Red);

							Console.WriteLine();

							goto Startup;
						}
#endif

						break;
					default:
						WriteLine($"Command \"{Command}\" not recognized", ConsoleColor.Red);

						goto Startup;
				}

				WriteLine("Say \"break\" to stop looping", ConsoleColor.DarkYellow);

				if (!Running)
					break;

				if (Console.ReadLine().Equals("break")) {
					Console.WriteLine();

					goto Startup;
				}
			}

			WriteLine("< Closing > Program ended", ConsoleColor.Red);
		}

		static void Commandlined(string Method, string Input, string Output) {
			OutputType Type;
			byte[] Files;

			switch (Method) {
				case "Cpl":
					Type = OutputType.COMPILE;

					break;
				case "Dis":
					Type = OutputType.DISASSEMBLE;

					break;
				default:
					throw new ArgumentException("Parameter not recognized", "Method");
			}

			try {
				Stopwatch Watcher = new Stopwatch();
				Watcher.Start();

				WriteLine("Retrieving bytecode", ConsoleColor.Yellow);
				Files = FileBytes(Input, false);

				WriteLine($"Bytecode size of {Files.LongLength} bytes", ConsoleColor.Yellow);
				Files = AsSource(Type, Files);

				WriteLine("Saving file result", ConsoleColor.Yellow);
				ToFile(Files, Output);

				Watcher.Stop();

				WriteLine($"Operation took {Watcher.ElapsedMilliseconds}ms", ConsoleColor.Green);
			}
			catch (Exception E) {
				WriteLine("An error occurred", ConsoleColor.Red);
				WriteLine(E.Message, ConsoleColor.Red);
			}
		}

		static void Main(string[] Args) {
			int Numargs = Args.Length;

			if (Numargs <= 0)
				Interface();
			else {
				if (Numargs == 1)
					throw new Exception("Missing args `In` and `Out`");
				else if (Numargs == 2)
					throw new Exception("Missing arg `Out`");
				else if (Numargs > 3)
					throw new Exception("Too many args to Rerude");

				Commandlined(Args[0], Args[1], Args[2]);
			}
		}
	}
}