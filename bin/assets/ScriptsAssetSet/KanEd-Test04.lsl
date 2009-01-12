integer counter;
integer second;
vector startPosition;

default
{
   state_entry()
   {
       llSay( 0, "Hello, Avatar! Touch to change position.");
       counter = 0;
       startPosition = llGetPos();
   }

   touch_start(integer total_number)
   {
       counter = counter + 1;        
        
       llSay( 0, "Touched by angel number " + (string)counter);

       llSetTimerEvent( 1 );  // arrange for a "timer event" every second.
   }

   timer()  // do these instructions every time the timer event occurs.
   {
       second++;

       // choose three random distances between 0. and 10.0.
       float X_distance = llFrand( 10.0 );  
       float Y_distance = llFrand( 10.0 );
       float Z_distance = llFrand( 10.0 );
       
       // combine these distance components into a vector and use it
       // to increment the starting position and reposition the object.
       vector increment = < X_distance, Y_distance, Z_distance >;  
       vector newPosition = startPosition + increment;
       llSetPos( newPosition );   // reposition object.
       
       if ( second > 19 )  // then time to wrap this up.
       {    
           // move object back to starting position...
           while ( llVecDist( llGetPos(), startPosition ) > 0.001) 
           {
               llSetPos( startPosition );
           }
    
           llSay( 0, "Object now resting and resetting script." );
           llResetScript();  // return object to ready state.
       }
   }
}

