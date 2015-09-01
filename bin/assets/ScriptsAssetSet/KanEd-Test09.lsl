vector startPos;
vector curPos;
vector curForce;
integer second;

default
{
   state_entry()
   {
       llSay( 0, "Hello, Avatar! Touch to launch me straight up.");
       llSetStatus( 1, TRUE );
       startPos = < 0, 0, 0 >;
   }
 
   touch_start(integer total_number)
   {
       startPos = llGetPos();
       curPos = startPos;
       curForce = < 0, 0, 0 >;
       second = 0;
        
       llSetColor( < 1.0, 0.0, 0.0 > , ALL_SIDES ); // set color to red.
        
       float objMass = llGetMass();
       float Z_force = 10.2 * objMass;
        
       llSetForce( < 0.0, 0.0, Z_force >, FALSE );
        
       llSay( 0, "Force of " + (string)Z_force + " being applied." );
       llSetTimerEvent(1);
   }
    
   timer()
   {
       second++;
       curPos = llGetPos();
       float curDisplacement = llVecMag( curPos - startPos );
       
       if( ( curDisplacement > 30. ) &&  // then object is too far away, and
         ( llGetForce() != < 0.0, 0.0, 0.0 > ) ) // force not already zero, 
       {   // then let gravity take over, and change color to green.
           llSetForce( < 0.0, 0.0, 0.0 >, FALSE ); 
           llSetColor( < 0, 1.0, 0 >, ALL_SIDES ); 
           llSay( 0, "Force removed; object in free flight." );
       } 
        
       if ( second > 19 )  // then time to wrap this up.
       {
           // turn object blue and zero force to be safe....
           llSetColor( < 0, 0, 1.0 >, ALL_SIDES ); // change color to blue.
           llSetForce( < 0, 0, 0 >, FALSE );

           // ...move object back to starting position...
           // ...after saving current status of Physics attribute.
           integer savedStatus = llGetStatus( 1 );
           llSetStatus( 1, FALSE );  // turn physics off.
           while ( llVecDist( llGetPos(), startPos ) > 0.001) 
           {
               llSetPos( startPos );
           }
           llSetStatus( 1, savedStatus );  // restore Physics status.
           
           //...and then turn color to black and Reset the script.          
           llSetColor( < 1, 1, 1 >, ALL_SIDES );
           llSetTimerEvent( 0 );  // turn off timer events.
           llSay( 0, "Done and resetting script." );
           llResetScript();  // return object to ready state.
       }
   }
}

