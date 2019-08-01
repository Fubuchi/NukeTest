using NUnit.Framework;

namespace librarytest
{
  public class MathTest
  {
    [Test]
    public void TestAdd()
    {
      Assert.AreEqual(5, library.Math.add(2, 3));
    }
  }
}
