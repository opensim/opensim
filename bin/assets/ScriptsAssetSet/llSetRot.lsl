default
{
    state_entry()
    {
        llOwnerSay("Touch me");
    }
    touch_start(integer total_number)
    {
        rotation Y_10 = llEuler2Rot( < 0, 0, 30 * DEG_TO_RAD > );
        rotation newRotation = llGetRot() * Y_10;
        llSetRot( newRotation );             
    }
}
