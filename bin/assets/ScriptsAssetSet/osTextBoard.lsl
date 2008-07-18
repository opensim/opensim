string title = "";
string subtitle = "";
string text = "";
string add = "";
integer channel = 0; // if this is >= 0, llSay on that channel on updates

push_text()
{
    compile_text();
    draw_text();    
}

compile_text()
{
    title = "Some Title";
    subtitle = "Some subtitle";

    text = "Plenty of text for the main body.\n";
    text += "You need to manual do line breaks\n";
    text += "here.  No word wrap yet.";

    add = "Additional text at the bottom";
}

draw_text()
{
    string drawList = "MoveTo 40,80; PenColour RED; FontSize 48; Text " + title + ";";
    drawList += "MoveTo 160,160; FontSize 32; Text " + subtitle + ";";
    drawList += "PenColour BLACK; MoveTo 40,220; FontSize 24; Text " + text + ";";
    drawList += "PenColour RED; FontName Times New Roman; MoveTo 40,900; Text " + add + ";"; 
    osSetDynamicTextureData("", "vector", drawList, "1024", 0);
}

default {
    state_entry()
    {
        push_text();    
    }
   
    touch_start(integer count)
    {
        push_text();
        if (channel >= 0) {
            llSay(channel, text);    
        }
    }
    
}
