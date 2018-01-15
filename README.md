# Rerudec
For lack of a better name, a (wip) decompiler, disassembler and reassembler of Lua 5.1 bytecode (eventually will support further types).
Rerudec is a "standalone" C# library made for tweaking and visualizing the Lua bytecode format for 5.1.
The library currently contains tools to disassemble bytecode out into an assembly-like style, and a way to get that assembly back into code.
The following should be noted:

1. The library is "standalone" as in all the code required is open source, with it being only 1 real dependency, the also open source [CSLuaCore](https://github.com/Rerumu/CSLuaCore), by me, which also includes credits and sources.
2. Despite NLua being listed as a dependency, it's only if you wish to use this test implementation, due to needing a way to compile, as the underlying libraries do not use it
3. All of the **necessary** code for the library to work is made by me
4. While the majority of the code is either self explanatory or documented, I suck at both of those
5. The decompiler is very unstable but it has been added into this version for those who want to have fun

The ***core*** files (dependencies) you would *need* to use the standalone library only are inside of Components.

You do not *need* Program.cs, and should only be there to show off how the program works. Program.cs is ran via **command line** and takes 3 arguments, a `Mode`, `Input`, and `Output` parameter. For more info, simply run in the containing folder `./Rerude.exe Help`.
The **Test** folder contains test files I normally use while debugging.

Documentation coming soon, hopefully, although Program.cs (my half hour mess) should just about explain how things work for now.
Out of the good of open source, I suggest you don't use this for anything malicious, unless you're willing to let me in on the meme.

Program.cs test images:
![Program Image](https://image.prntscr.com/image/5Y2KpnQwR06gRN0jvYlsQw.png)
![Decompiler](https://image.prntscr.com/image/OIMfCplQTSa-dmtSWpHAKA.png)