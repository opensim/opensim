integer createdObjectCounter;
integer linkedObjectCounter;

default
{
   state_entry()
   {
       llSay( 0, "Hello, Avatar!");
       linkedObjectCounter = 0;  // zero the linked object counter.
   }

   touch_start(integer total_number)
   {
       if( createdObjectCounter <= 0 )  // nothing has yet been linked,
       {                               // begin object creation sequence...
          // ask for permissions now, since it will be too late later.
          llRequestPermissions( llGetOwner(), PERMISSION_CHANGE_LINKS );
       }
       else   // just do whatever should be done upon touch without
       {      // creating new objects to link. 
           // insert commands here to respond to a touch.
       }
   }
 
   run_time_permissions( integer permissions_granted )
   {
      if( permissions_granted == PERMISSION_CHANGE_LINKS )
      {   // create 2 objects.
          llRezObject("Object1", llGetPos() + < 1, 0, 2 >, 
                      ZERO_VECTOR, ZERO_ROTATION, 42);
          createdObjectCounter = createdObjectCounter + 1;

          llRezObject("Object1", llGetPos() + < -1, 0, 2 >, 
                      ZERO_VECTOR, ZERO_ROTATION, 42);
          createdObjectCounter = createdObjectCounter + 1;

      }
      else
      {
          llOwnerSay( "Didn't get permission to change links." );
          return;
      }
   }
 
   object_rez( key child_id )
   {
       llOwnerSay( "rez happened and produced object with key " + 
                    (string)child_id );
 
       // link as parent to the just created child.
       llCreateLink( child_id, TRUE ); 
 
       // if all child objects have been created then the script can
       // continue to work as a linked set of objects.
       linkedObjectCounter++;
       if( linkedObjectCounter >= 2 ) 
       {
           // Change all child objects in the set to red (including parent).
           llSetLinkColor( LINK_ALL_CHILDREN, < 1, 0, 0 >, ALL_SIDES );  
 
           // Make child object "2" half-tranparent.
           llSetLinkAlpha( 2, .5, ALL_SIDES );

           // Insert commands here to manage subsequent activity of the 
           // linkset, like this command to rotate the result:
           // llTargetOmega( < 0, 1, 1 >, .2 * PI, 1.0 );
       }
   }
}

