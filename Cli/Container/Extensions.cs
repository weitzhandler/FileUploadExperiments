using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Cli
{
  public static class Assert
  {
    public static void True(bool condition)
    {
      Debug.Assert(condition);
    }

    public static void Equal(object expected, object actual)
    {
      Debug.Assert(Equals(expected, actual));
    }

  }

  abstract class TestAttribute : Attribute
  {
    public string Skip { get; set; }
  }

  [AttributeUsage(AttributeTargets.Method)]
  class FactAttribute : TestAttribute { }

  [AttributeUsage(AttributeTargets.Method)]
  class TheoryAttribute : TestAttribute { }

  [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
  class InlineDataAttribute : TestAttribute
  {
    public string[] Arguments { get; }
    public InlineDataAttribute(params string[] args)
    {
      Arguments = args;
    }
  }
}
