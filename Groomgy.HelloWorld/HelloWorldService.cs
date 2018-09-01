namespace Groomgy.HelloWorld
{
    public class HelloWorldService
    {
        public string Say()
        {
            return "Hello World";
        }

        public string Bye()
        {
            return "Bye Bye";
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
            return new HelloWorldDependencyLibrary.MyDependency().Get();
        }
    }
}
