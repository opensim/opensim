vector startPosition;
float groundLevel;

default 
{
   state_entry() 
   { 
      // get permission to take over the avatar's control inputs.
      llRequestPermissions( llGetOwner(), PERMISSION_TAKE_CONTROLS );
      
      startPosition = llGetPos();
      groundLevel = llGround( startPosition );
   }

   run_time_permissions( integer perm )  // event for processing 
                                          // permission dialog.
   {
       if ( perm & PERMISSION_TAKE_CONTROLS )  // permission has been given.
       { 
           // go ahead and take over the forward and backward controls.
           llTakeControls( CONTROL_FWD | CONTROL_BACK, TRUE, FALSE ); 
       }
   }
    
   control( key id, integer held, integer change )  // event for processing 
                                                     // key press.
   { 
       vector position = llGetPos();
       
       if ( change & held & CONTROL_FWD ) 
       {   // the "move forward" control has been activated.
           if( position.z < (startPosition.z + 10.0) )
           {
              llSetPos( llGetPos() + < 0, 0, 1.0 >); // move up
           }
       } 
       else if ( change & held & CONTROL_BACK ) 
       {   // the "move backward" key has been activated.
           if( position.z > groundLevel + 1.0 ) 
           {
              llSetPos( llGetPos() + < 0, 0, -1.0 >); // move down
           }
       }
   }
}

