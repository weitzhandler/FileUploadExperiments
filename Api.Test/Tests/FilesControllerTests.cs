using System.Runtime.InteropServices.ComTypes;
using System.Net.Http.Formatting;
using Models;
using DryIoc;
using System.Net.Http.Headers;
using Shared;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Threading.Tasks;
#if TEST
using Xunit;
#else
using Cli;
#endif

namespace Api.Test
{

  public class FilesControllerTests
#if TEST
    : IClassFixture<ContainerFixture>
#endif
  {
    readonly IContainer _Container;
    public FilesControllerTests(ContainerFixture fixture)
    {
      _Container = fixture.Container;      
    }

    protected async Task InitializeFiles()
    {
      if (!File.Exists(SmallFilePath))
        await File.WriteAllTextAsync(SmallFilePath, generate(KiloByte));

      if (!File.Exists(LargeFilePath))
        await File.WriteAllTextAsync(LargeFilePath, generate(5 * MegaByte));

      if (!File.Exists(HugeFilePath))
        await File.WriteAllTextAsync(HugeFilePath, generate(200 * MegaByte));

      string generate(int length) => new string('a', length);
    }

    public HttpClient Client => _Container.Resolve<HttpClient>();
    public IProgress<decimal> Progress => _Container.Resolve<IProgress<decimal>>();

    public const string SmallFilePath = "SmallFile.txt";
    public const string LargeFilePath = "LargeFile.txt";
    public const string HugeFilePath = "HugeFile.txt";
    static readonly string[] AllPaths = { SmallFilePath, LargeFilePath, HugeFilePath };

    public const int KiloByte = 1024;
    public const int MegaByte = 1000 * KiloByte;
    public const int DefaultBufferSize = 4 * MegaByte;


    [Fact(Skip = "")]
    public async Task TestConnection()
    {
      var result = await Client.GetAsync(Constants.FilesRoute);
      Assert.True(result.IsSuccessStatusCode);
    }

    [Theory]
    [InlineData(HugeFilePath, Constants.UploadChunk)]
    [InlineData(HugeFilePath, Constants.UploadChunkSingleFile)]
    public async Task UploadChunkTest(string path, string controller = Constants.UploadChunk, int bufferSize = DefaultBufferSize)
    {
      var transactionId = Guid.NewGuid();
      var chunkId = 0;      

      using (var stream = File.OpenRead(path))
      using (var bufferedStream = new ProgressStream(stream, Progress, bufferSize))
      {
        int length;
        var buffer = new byte[bufferSize];
        while ((length = await bufferedStream.ReadAsync(buffer, 0, bufferSize)) > 0)
        {
          var url = $"{Constants.FilesRoute}/{controller}/{transactionId}";
          var last = stream.Position >= stream.Length;
          if (controller == Constants.UploadChunk)
            url += $"/{chunkId++}/{last}";

          var result =
            await Client.PostAsync(
              url,
              new ByteArrayContent(buffer, 0, length));

          Assert.True(result.IsSuccessStatusCode);
          if (last && controller == Constants.UploadChunk)
          {
            var resultLength = await result.Content.ReadAsAsync<long>();
            Assert.Equal(stream.Length, resultLength);
          }
        }
      }
    }

    [Theory]
    [InlineData(HugeFilePath)]
    public async Task UploadDirectStreamTest(string path, string controller = Constants.UploadDirect, int bufferSize = DefaultBufferSize)
    {
      using (var file = File.OpenRead(path))
      using (var stream = new ProgressStream(file, Progress, bufferSize))
      using (var streamContent = new StreamContent(stream, bufferSize))
      {
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
        streamContent.Headers.ContentDisposition = new FormDataContentDispositionHeaderValue();

        var response =
          await Client.PostAsync(
            $"{Constants.FilesRoute}/{controller}",
            streamContent);

        Assert.True(response.IsSuccessStatusCode);
      }
    }


    [Theory()]
    [InlineData(HugeFilePath)]
    public async Task UploadDirectMultipartTest(string path, int bufferSize = DefaultBufferSize)
    {
      var record = new FileRecord();
      using (var recordContent = new ObjectContent<FileRecord>(record, new JsonMediaTypeFormatter()))
      using (var fileStream = File.OpenRead(path))
      using (var progressStream = new ProgressStream(fileStream, Progress, bufferSize))
      using (var streamContent = new StreamContent(progressStream, bufferSize))
      {
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Octet);
        streamContent.Headers.ContentDisposition = new FormDataContentDispositionHeaderValue();

        var content = new MultipartContent
        {
          recordContent,
          streamContent
        };

        var response =
          await Client.PostAsync(
            $"{Constants.FilesRoute}/{Constants.UploadMultipartDirect}",
            content);

        Assert.True(response.IsSuccessStatusCode);
      }
    }


    [Theory]
    [InlineData(HugeFilePath)]
    public async Task UploadPushTest(string path, int bufferSize = DefaultBufferSize)
    {
      using (var fileStream = File.OpenRead(path))
      using (var bufferedStream = new ProgressStream(fileStream, Progress, bufferSize))
      {
        var psContent = new PushStreamContent(async (stream, content, context) =>
        {
          try
          {
            await bufferedStream.CopyToAsync(stream, bufferSize).ConfigureAwait(false);
          }
          finally
          {
            stream.Dispose();
          }
        });
        var response =
          await Client.PostAsync(
            $"{Constants.FilesRoute}/{Constants.UploadPush}/{Guid.NewGuid()}",
            psContent).ConfigureAwait(false);
        Assert.True(response.IsSuccessStatusCode);
      }
    }


    [Theory]
    [InlineData(HugeFilePath)]
    public async Task UploadMultipartOriginal(string path, int bufferSize = DefaultBufferSize)
    {
      var record = new FileRecord();

      using (var fileStream = File.OpenRead(path))
      using (var bufferedStream = new ProgressStream(fileStream, Progress, bufferSize))
      {
        using (var multipart = new MultipartContent())
        using (var fileContent = new StreamContent(bufferedStream, bufferSize))
        {
          multipart.Add(fileContent);
          var contentDispositionHeader = new ContentDispositionHeaderValue("attachment")
          {
            CreationDate = DateTime.Now,
            FileName = Path.GetFileName(path),
            Size = fileStream.Length
          }; 

          fileContent.Headers.ContentDisposition = contentDispositionHeader;  

          var response = await Client.PostAsync($"{Constants.FilesRoute}/{Constants.UploadMultipartOriginal}", multipart);
        }
      }

    }



  }
}
