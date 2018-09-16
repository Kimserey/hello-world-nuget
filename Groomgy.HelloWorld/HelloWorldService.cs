﻿namespace Groomgy.HelloWorld
{
    public class HelloWorldService
    {
        public string Say()
        {
            return "Hello World";
        }

        public string GoodMorning()
        {
            return "Good morning";
        }

        public string GoodAfternoon()
        {
            return "Good afternoon";
        }

        public string GoodEvening()
        {
            return "Good evening";
        }

        // test comment
        // some other next comment
        // some next comment

        public string CallMyDependency()
        {
            return "My dependency: " + new HelloWorldDependencyLibrary.MyDependency().Get();
        }
    }
}
