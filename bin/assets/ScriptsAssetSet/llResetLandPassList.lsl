//Commands are:
///5 ban:full_avatar_name
///5 tempban:full_avatar_name
///5 unban:full_avatar_name
///5 pass:full_avatar_name
///5 unpass:full_avatar_name
///5 clearban
///5 clearpass

string command;

default
{
    state_entry()
    {
        llListen(5, "", llGetOwner(), "");
    }
    
    on_rez(integer param)
    {
        llResetScript();
    }

    listen(integer chan, string name, key id, string message)
    {
        if (command != "")
        {
            llOwnerSay("Sorry, still processing last command, try again in a second.");
        }
        
        list args = llParseString2List(message,[":"],[]);
        command = llToLower(llList2String(args,0));
        
        if (command == "clearbans")
        {
            llResetLandBanList();
        }
        if (command == "clearpass")
        {
            llResetLandPassList();
        }
        else
        {
            llSensor(llList2String(args,1),NULL_KEY,AGENT,96,PI);
        }
    }
    
    no_sensor()
    {
        command = "";
    }
    
    sensor(integer num)
    {
        integer i;
        for (i=0; i< num; ++i)
        {
            if (command == "ban")
            {
                // Ban indefinetely 
                llAddToLandBanList(llDetectedKey(i),0.0);
            }
            if (command == "tempban")
            {
                // Ban for 1 hour.
                llAddToLandBanList(llDetectedKey(i),1.0);
            }
            if (command == "unban")
            {
                llRemoveFromLandBanList(llDetectedKey(i));
            }
            if (command == "pass")
            {
                // Add to land pass list for 1 hour
                llAddToLandPassList(llDetectedKey(i),1.0);
            }
            if (command == "unpass")
            {
                llRemoveFromLandPassList(llDetectedKey(i));
            }
        }
        command = "";
    }
}
