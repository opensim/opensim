default
{
    state_entry()
    {
        rotation aRot = ZERO_ROTATION;
        rotation bRot = llGetRot();
        float aBetween = llAngleBetween( aRot, bRot );
        llOwnerSay((string)aBetween);
        //llGetRot() being < 0, 0, 90 > this should report 1.570796
    }
}
