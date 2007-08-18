integer touch_count = 0;

to_integer(float num)
{
    llSay(0, num + " floor: " + llFloor(num));
    llSay(0, num + " ceiling: " + llCeil(num));
    llSay(0, num + " round: " + llRound(num));
}

default {
    state_entry()
    {
        llSay(0, "Hello, Avatar!");
    }

    touch_start(integer total_number)
    {
        float angle45 = PI/4.0; // 45 degrees
	float angle30 = PI/6.0; // 30 degrees
	float sqrt2 = llSqrt(2.0);
	float deltaCos = llCos(angle45) - sqrt2/2.0;
	float deltaSin = llSin(angle30) - 0.5;
	float deltaAtan = llAtan2(1, 1)*4 - PI;
	float deltaTan = llTan(PI);
	llSay(0, "deltaSin: " + deltaSin);
	llShout(0, "deltaCos: " + deltaCos);
	llWhisper(0, "deltaTan: " + deltaTan);
	llWhisper(0, "deltaAtan: " + deltaAtan);
	llSay(0, "Fabs(power(2^16)): " + llFabs(0-llPow(2, 16)));
	llSay(0, "Abs(-1): " + llAbs(-1));
	llSay(0, "One random(100): " + llFrand(100));
	llSay(0, "Two random(100): " + llFrand(100));
	llSay(0, "Three random(100): " + llFrand(100));
	llSay(0, "Four random(100.0): " + llFrand(100.0));
	llWhisper(0, "The unix time is: " + llGetUnixTime());
	to_integer(2.4);
	to_integer(2.5);
	to_integer(2.6);
	to_integer(3.51);
	llSay(0, "Should be 112abd47ceaae1c05a826828650434a6: " + llMD5String("Hello, Avatar!", 0));
	llSay(0, "Should be 9: " +llModPow(2, 16, 37));
	llSay(0, "Region corner: " + (string)llGetRegionCorner());
	llSetText("This is a text", <1,0,0>, 1);
	
	touch_count++;
        llSay(0, "Object was touched. Touch count: " + touch_count);
    }
}
