﻿namespace Groomgy.HelloWorld
{
    public class HelloWorldService
    {
        public string Say()
        {
            return "Hello World";
        }

        public string GoodEvening()
        {
            return "Good evening";
        }

        public string GoodNight()
        {
            return "Good night test";
        }

        public string GoodNight2()
        {
            return "Good night";
        }

        public string CallMyDependency()
        {
            return "My dependency: " + new HelloWorldDependencyLibrary.MyDependency().Get();
        }
    }
}
