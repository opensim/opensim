//Rez an object, and drop this script in it.
//This will launch it at the owner.
default
{
    state_entry()
    {
        list p = llGetObjectDetails(llGetOwner(), [OBJECT_POS]);
        if(p != [])
        {
            llSetStatus(STATUS_PHYSICS, TRUE);
            vector pos = llList2Vector(p, 0);
            vector direction = llVecNorm(pos - llGetPos());
            llApplyImpulse(direction * 100, 0);
        }
    }
}
