namespace Groomgy.HelloWorld
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

        public string GoodEvening1()
        {
            return "Good evening";
        }

        public string GoodNight()
        {
            return "Good night";
        }

        public string GoodNight1()
        {
            return "Good night";
        }

        public string GoodNight2()
        {
            return "Good night";
        }

        public string GoodNight3()
        {
            return "Good night";
        }

        public string GoodNight4()
        {
            return "Good night";
        }

        public string GoodNight5()
        {
            return "Good night";
        }

        public string GoodNight6()
        {
            return "Good night";
        }


        public string CallMyDependency()
        {
            return "My dependency: " + new HelloWorldDependencyLibrary.MyDependency().Get();
        }
    }
}
