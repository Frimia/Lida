local GETGLOBAL = next; -- Use all opcodes, by Rerumu
local MOVE = GETGLOBAL;
local NEWTABLE = {'SETLIST_LOADK'};
local VARARG = ...;
local LOADNIL, LOADBOOL = nil, false;

for PREP_AND_LOOP = 1, #VARARG do
	VARARG[PREP_AND_LOOP] = PREP_AND_LOOP;
end;

for GENERIC, LOOP in MOVE, VARARG do
	if (not GENERIC) or (GENERIC > 5) or (GENERIC <= 1) or (GENERIC == 3) then
		local GETTABLE = VARARG[
			(-GENERIC
			+ GENERIC
			- GENERIC
			* GENERIC
			/ GENERIC
			% GENERIC
			^ GENERIC)
			.. GENERIC
		];
	end;
end;

MOVE = LOADNIL;

if LOADBOOL then
	local LOADK = 0;

	LOADNIL = VARARG:SELF(function()
		local CLOSE_GETUPVALUE = LOADK;

		LOADK = 'SETUPVALUE';
	end);

	return VARARG();
end;

if (not NOT_TEST) then
	TESTSET_SETGLOBAL = SETGLOBAL_CLOSURE(NEWTABLE) and MOVE or GETGLOBAL;
end;