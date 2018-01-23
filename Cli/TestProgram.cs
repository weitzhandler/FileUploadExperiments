using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Api.Test;
using System.Runtime.InteropServices.ComTypes;
using System.Net.Http.Headers;
using System;
using TestResult = System.ValueTuple<System.Reflection.MethodInfo, object[], long>;



namespace Cli
{
  class TestProgram : FilesControllerTests
  {
    readonly ContainerFixture _Fixture;
    public TestProgram(ContainerFixture fixture) : base(fixture)
    {
      _Fixture = fixture;
      Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

    

    }

    public MethodInfo CurrentMethod
    {
      get => _Fixture.CurrentMethod;
      set => _Fixture.CurrentMethod = value;
    }
    public List<string> Parameters => _Fixture.Parameters;


    /// <summary>
    /// Method, params, ms it took to run.
    /// </summary>
    public List<(MethodInfo Method, object[] Parameters, long Milliseconds)> Results = new List<(MethodInfo, object[], long)>();

    static async Task Main(string[] args)
    {
      await Task.Delay(2000);      

      var fixture = new ContainerFixture();
      var program = new TestProgram(fixture);
      await program.InitializeFiles();

      await program.RunTests();// (mi=> mi.Name == nameof(UploadMultipartOriginal));

      Console.WriteLine("Press any key to terminate.");
      Console.Read();
    }

    async Task RunTests(Predicate<MethodInfo> filter = null)
    {
      if (filter == null)
        filter = (mi) => true;

      var methods = GetType().GetRuntimeMethods();
      var facts = methods.Where(mi => mi.GetCustomAttributes<FactAttribute>().Any(notskipped));

      foreach (var fact in facts)
        await InvokeTest(fact, filter);

      foreach (var theory in methods.Where(mi => mi.GetCustomAttributes<TheoryAttribute>().Any(notskipped)))
        foreach (var inlineData in theory.GetCustomAttributes<InlineDataAttribute>().Where(notskipped))
          await InvokeTest(theory, filter, inlineData.Arguments);
      bool notskipped(TestAttribute att) => att.Skip == null;
    }

    async Task InvokeTest(MethodInfo method, Predicate<MethodInfo> filter, params object[] args)
    {
      if (filter?.Invoke(method) == false)
        return;

      var parameters = new object[method.GetParameters().Count()];
      args.CopyTo(parameters, 0);

      for (int i = args.Length; i < parameters.Length; i++)
        parameters[i] = Type.Missing;

      var sw = new Stopwatch();
      OnInvokeMethod(method, args);
      sw.Start();
      var result = method.Invoke(this, BindingFlags.OptionalParamBinding, null, parameters, CultureInfo.InvariantCulture);
      sw.Stop();
      if (result is Task task)
      {
        sw.Start();
        await task;
        sw.Stop();
      }

      var stats = (method, parameters, sw.ElapsedMilliseconds);
      Results.Add(stats);
      PrintResult(stats);
    }

    void OnInvokeMethod(MethodInfo methodInfo, params object[] args)
    { 
      Parameters.Clear();
      if (args?.Any() == true)
        Parameters.AddRange(args.Select(a => a.ToString()));
      CurrentMethod = methodInfo;
    }

    void PrintResult(TestResult result)
    {
      var args = string.Join(',', result.Item2);
      var ms = result.Item3;
      var time = ms < 1000 ? $"{ms} ms" : $"{ms / 1000} sec";
      Console.WriteLine();
      Console.WriteLine($"{time}\t{args}");
    }

  }
}
