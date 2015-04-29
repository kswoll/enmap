using System;

namespace Enmap
{
    public class EnmapLogger
    {
        public static Action<string> Logger = x => Console.WriteLine(x);

        public static void Log(string message)
        {
            Logger(message);
        } 
    }
}