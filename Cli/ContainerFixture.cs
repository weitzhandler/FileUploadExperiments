using System.Linq;
using System.Reflection;
using System.Net.Http;
using DryIoc;
using System;
using System.Collections.Generic;
using System.Text;
using Shared;

namespace Cli
{
  public class ContainerFixture : IDisposable, IProgress<decimal>
  {
    public ContainerFixture()
    {
      Container = new Container();
      RegisterTypes(Container);
    }

    private void RegisterTypes(IContainer container)
    {
      container.RegisterDelegate(r =>
      {
        var client = new HttpClient
        {
          BaseAddress = new Uri(Constants.Url)
        };

        return client;
      },
      Reuse.Singleton);


      container.RegisterInstance<IProgress<decimal>>(this, Reuse.Singleton);
      var progress = container.Resolve<IProgress<decimal>>();
    }

    public IContainer Container { get; }

    public void Dispose()
    {
      Container.Dispose();
    }

    public MethodInfo CurrentMethod { get; set; }
    public List<string> Parameters { get; } = new List<string>();

    public void Report(decimal value)
    {
      var method = CurrentMethod.Name;
      method += $"({string.Join(", ", Parameters)})";

      Console.Write($"\r{method}: {value:p2}");
    }
  }
}
