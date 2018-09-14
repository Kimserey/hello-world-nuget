namespace Groomgy.HelloWorld
{
    public class HelloWorldService
    {
        public string Say()
        {
            return "Hello World";
        }
        public string Say1()
        {
            return "Hello World";
        }
        public string Say2()
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

        public string GoodNight()
        {
            return "Good night";
        }

        public string CallMyDependency()
        {
            return "My dependency: " + new HelloWorldDependencyLibrary.MyDependency().Get();
        }
    }
}
