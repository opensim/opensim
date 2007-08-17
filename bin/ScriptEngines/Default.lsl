integer touch_count = 0;
default {
    state_entry()
    {
        llSay(0, "Hello, Avatar!");
    }

    touch_start(integer total_number)
    {
	touch_count++;
        llSay(0, "Object was touched. Touch count: " + touch_count);
    }
}
