using Shared;
using System.Net.Http.Headers;
using System.IO;
using DryIoc;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using Xunit;
using System.Threading.Tasks;


namespace Api.Test
{
  public class StreamingControllerTests : IClassFixture<ContainerFixture>
  {
    public const string SmallFilePath = "SmallFile.txt";
    public const string LargeFilePath = "LargeFile.txt";
    public const string HugeFilePath = "HugeFile.txt";

    readonly IContainer _Container;
    public StreamingControllerTests(ContainerFixture fixture)
    {
      _Container = fixture.Container;
    }

    public HttpClient Client => _Container.Resolve<HttpClient>();


    [Theory]
    [InlineData(LargeFilePath)]
    [InlineData(HugeFilePath)]
    public async Task UploadFileByteArrayContent(string path)
    {
      using (var content = new MultipartFormDataContent())
      {
        // Add first file content 
        var fileContent1 = new ByteArrayContent(File.ReadAllBytes(path));
        fileContent1.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
        {
          FileName = path
        };

        content.Add(fileContent1);

        // Make a call to Web API
        var result = await Client.PostAsync($"Streaming", content);

        Assert.True(result.IsSuccessStatusCode);
      }


    }

  }
}
