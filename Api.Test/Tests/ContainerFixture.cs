using Shared;
using System.Reflection.Metadata;
using Remotion.Linq.Clauses.ResultOperators;
using DryIoc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Api.Test
{
  public class ContainerFixture : IDisposable 
  {
    public ContainerFixture()
    {
      Container = new Container();
      RegisterTypes(Container);
    }

    private void RegisterTypes(IContainer container)
    {
      container.RegisterDelegate(r =>
        new TestServer(new WebHostBuilder()
        .UseStartup<Startup>()),
        //.UseUrls(Constants.Url)),
        Reuse.Singleton);
      container.RegisterDelegate(r =>
      {
        var client = r
        .Resolve<TestServer>()
        .CreateClient();

        //client.BaseAddress = new Uri(Constants.Url);

        return client;
      },
      Reuse.Singleton);
    }

    public IContainer Container { get; }

    public void Dispose()
    {
      Container.Dispose();
    }


  }

}
