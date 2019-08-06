using System;
using System.Collections;
using System.Collections.Generic;
using library;

namespace app
{
  class Program
  {
    static void Main(string[] args)
    {
      var pairNums = new List<int> { 8, 7, 1 };
      pairNums.Sort(Comparer<int>.Default);
      pairNums.ForEach(Console.WriteLine);
    }
  }
}
