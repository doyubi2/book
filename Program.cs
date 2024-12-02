using System;

namespace AccountBook
{
    class Program
    {
        static void Main(string[] args)
        {
            var accountBook = new AccountBook();
            accountBook.Initialize();
            accountBook.Run();
        }
    }
}