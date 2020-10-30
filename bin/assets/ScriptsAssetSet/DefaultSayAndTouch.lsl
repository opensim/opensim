default
{
    state_entry()
    {
        llSay( 0, "I am Alive!");
    }

    touch_start(integer number_of_touchs)
    {
        llSay( 0, "Touched.");
    }
}

