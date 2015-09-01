default
{
    state_entry()
    {
        // set sit target, otherwise this will not work 
        llSitTarget(<0.0, 0.0, 0.1>, ZERO_ROTATION);
    }
    changed(integer change)
    {
        if (change & CHANGED_LINK)
        { 
            key av = llAvatarOnSitTarget();
            //evaluated as true if not NULL_KEY or invalid
            if (av)
            {
                llSay(0, "Hello " + llKey2Name(av) + ", thank you for sitting down");
            }
        }
    }
}
