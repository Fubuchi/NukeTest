using System;
using library;

namespace app
{
  class Program
  {
    static void Main(string[] args)
    {
      Console.WriteLine("Math: ");
      Console.WriteLine("5 + 6 = {0}", library.Math.add(5, 6));
      Console.WriteLine("5 - 6 = {0}", library.Math.subtract(5, 6));
    }
  }
}
