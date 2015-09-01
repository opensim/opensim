default
{
    state_entry()
    {
        float num1 = llFrand(100.0);
        float num2 = llFrand(100.0);
        llOwnerSay("y = " + (string)num1);
        llOwnerSay("x = " + (string)num2);
        llOwnerSay("The tangent of y divided by x is " + (string)llAtan2(num1, num2));
  }
}
