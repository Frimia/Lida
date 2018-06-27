# Lida
A disassembler and reassembler of Lua 5.1 bytecode (eventually will support further types).
Lida is a "standalone" C# library made for tweaking and visualizing the Lua bytecode format for 5.1.
The library currently contains tools to disassemble bytecode out into an assembly-like style, and a way to get that assembly back into code.
The following should be noted:

1. The library is "standalone" as in all the code required is open source, with it being only 1 real dependency, the also open source [SeeLua](https://github.com/Rerumu/SeeLua), by me, which also includes credits and sources
2. Lua must be installed for this to work, or at least have `luac.exe` in your PATH variable
3. All of the **necessary** code for the library to work is made by me
4. While the majority of the code is either self explanatory or documented, I suck at both of those
5. Decompiler has been split into a separate project and will not be present here

The ***core*** files (dependencies) you would *need* to use the standalone library only are inside of Components.

You do not *need* Program.cs, and should only be there to show off how the program works. Running the file without any arguments shows the accepted arguments.
The **Test** folder contains test files I normally use while debugging.

Documentation coming soon, hopefully, although Program.cs (my half hour mess) should just about explain how things work for now.
Out of the good of open source, I suggest you don't use this for anything malicious, unless you're willing to let me in on the meme.

Program.cs test images:
![Program Image](https://image.prntscr.com/image/LuzESa2aTty-fiMcyMOsDg.png)