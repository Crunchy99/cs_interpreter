
C# interpreter with support for a good range of scripting capability. It functionizes the outputs of a parser. (Not a virtual machine).

I tried to write something intuitive. The classes are easy to read and understand, and C# provides some incredibly useful syntax sugar.

The main idea was to be able to generate and run code dynamically. There are a few issues like how the function calling isn't properly done as in functional programming languages - so "a.b.c()" syntax doesn't work. But plenty is working.


Dependencies: You need to get csparser (included here).

Sample code that is executed dynamically:

    class Class2
    {
      public int addf(){return 1;}
      public int addf(int x, int y){return x+y;}
    
      public static int x;
    }    

    Class2 g = new Class2();
    
    bool l = return_true(5,1);
    Console.WriteLine (g.addf());
    Console.WriteLine (l);
    Console.WriteLine ("HEYYEYAAEYAAAEYAEYAA.mp4" + " -> " + l);
    
    Class2 x = new Class2();
    int intv = x.addf(6,0) * 2 / 2;
    bool h = true;
    if((h)) { Console.WriteLine("yooo"); }
    else Console.WriteLine("a");
