using System;
using Groomgy.HelloWorld;

namespace Groomgy.TestConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(new HelloWorldService().Say());
            Console.ReadLine();
        }
    }
}
