vector startPosition;
float groundLevel;
 
default
{
   state_entry() 
   {
       llListen( 0, "", llGetOwner(), "");

       startPosition = llGetPos();
       groundLevel = llGround( startPosition );
   
       llSay( 0, "Control this object with chat commands like:" );
       llSay( 0, "'up' or 'down' followed by a distance." );
   }  
     
   listen( integer channel, string name, key id, string message ) 
   {
       // separate the input into blank-delmited tokens.
       list parsed = llParseString2List( message, [ " " ], [] );

       // get the first part--the "command".
       string command = llList2String( parsed, 0 );
       
       // get the second part--the "distance".
       string distance_string = llList2String( parsed, 1 );
       float distance = ( float )distance_string;
 
       vector position = llGetPos();
 
       if( command == "up" )
       {
           if( ( position.z + distance ) < (startPosition.z + 10.0 ) )
           {
              llSetPos( llGetPos() + < 0, 0, distance > ); // move up
              llSetText( "Went up " + (string)distance, < 1, 0, 0 >, 1 );
           }
           else
           {
              llSetText( "Can't go so high.", < 1, 0, 0 >, 1 );
           }
       }
       else if( command == "down" )
       {
           if( ( position.z - distance ) > groundLevel ) 
           {
              llSetPos( llGetPos() + < 0, 0, -distance > ); // move down
              llSetText( "Went down " + (string)distance, < 1, 0, 0 >, 1 );
           }
           else
           {
              llSetText( "Can't go so low.", < 1, 0, 0 >, 1 );
           }
       }    
   }
}

