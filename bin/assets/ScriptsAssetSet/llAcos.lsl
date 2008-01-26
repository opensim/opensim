default
{
    state_entry()
    {
        float r = llFrand(2) - 1.0;
        llOwnerSay("The arccosine of " + (string)r + " is " + llAcos(r));
    }
}
