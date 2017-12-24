# Rerudec
For lack of a better name, a (wip) decompiler, disassembler and reassembler of Lua 5.1 bytecode.
Rerudec is a "standalone" C# library made for tweaking and visualizing the Lua bytecode format for 5.1.
The library currently contains tools to disassemble bytecode out into an assembly-like style, and a way to get that assembly back into code.
The following should be noted:

1. The library is "standalone" as in all the code required is open source, with it being only 1 real dependency, the also open source [CSLuaCore](https://github.com/Rerumu/CSLuaCore), by me.
2. Despite NLua being listed as a dependency, it's only if you wish to use this test implementation, due to needing a way to compile, as the underlying libraries do not use it
3. All of the **necessary** code for the library to work is made by me
4. While the majority of the code is either self explanatory or documented, I suck at both of those
5. The decompiler has been stripped from this version most likely until it is finished and error-less

The ***core*** files (dependencies) you would *need* to use the standalone library only are inside of Components.

You do not *need* Program.cs, and should only be there to show off how the program works. Program.cs is ran via **command line** and takes 3 arguments, a `Mode`, `Input`, and `Output` parameter. For more info, simply run in the containing folder `./Rerude.exe Help`.

Documentation coming soon, hopefully, although Program.cs (my half hour mess) should just about explain how things work for now.
Out of the good of open source, I suggest you don't use this for anything malicious, unless you're willing to let me in on the meme.

Program.cs (old) test image:
![Program Image](https://image.prntscr.com/image/DK0EG924So66vZAc6RR35g.png)
