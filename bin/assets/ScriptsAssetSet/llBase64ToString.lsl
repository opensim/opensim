default
{
    state_entry()
    {
        string test = llBase64ToString("U2VjcmV0Ok9wZW4=");
        llOwnerSay(test);
    }
}
