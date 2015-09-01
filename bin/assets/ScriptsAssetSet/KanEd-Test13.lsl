default
{
   state_entry()
   {
       llSay( 0, "Hello, Avatar!");
   }

   touch_start(integer total_number)
   {
       llSay( 0, "Touched.");
       
       llRezObject("Object1", llGetPos() + < 0, 0, 2 >, ZERO_VECTOR, 
           ZERO_ROTATION, 42);
   }
}

