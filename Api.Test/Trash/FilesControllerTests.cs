using Microsoft.Net.Http.Headers;
using System.Net.Http.Formatting;
using Models;
using System.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography.Xml;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using Microsoft.AspNetCore.TestHost;
using DryIoc;
using Xunit;
using System;
using Shared;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Api.Test
{
  public class FilesControllerTests : IClassFixture<ContainerFixture>
  {
    public const string SmallFilePath = "SmallFile.txt";
    public const string LargeFilePath = "LargeFile.txt";
    public const string HugeFilePath = "HugeFile.txt";

    readonly IContainer _Container;
    public FilesControllerTests(ContainerFixture fixture)
    {
      _Container = fixture.Container;
    }

    public HttpClient Client => _Container.Resolve<HttpClient>();

    [Fact]
    public async Task TestConnection()
    {
      var result = await Client.GetAsync(Constants.FilesRoute);
      Assert.True(result.IsSuccessStatusCode);
    }

    [Fact]
    public async Task IncreaseFile()
    {
      using (var file = File.OpenWrite(HugeFilePath))
      using (var writer = new StreamWriter(file))
      {
        var value = new string('a', int.MaxValue);
        await writer.WriteAsync(value);
      }
    }

    [Theory]
    [InlineData(SmallFilePath)]
    [InlineData(LargeFilePath)]
    [InlineData(HugeFilePath)]
    public async Task UploadFile(string path)
    {
      using (var fileStream = File.OpenRead(path))
      {
        var record = new FileRecord
        {
          FileId = Guid.NewGuid(),
          Size = fileStream.Length
        };

        decimal completed = default;
        var progress = new Progress<decimal>(d => completed = d);
        var wrapper = new ProgressStream(fileStream, progress);

        var multipart = new MultipartContent
        {
          new ObjectContent<FileRecord>(record, new JsonMediaTypeFormatter()),
          new StreamContent(wrapper)
        };

        var response = await Client.PostAsync($"{Constants.FilesRoute}/{Constants.LargeFile}/{wrapper.BufferSize}", multipart);

        Assert.True(completed == 1);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
      }
    }

    //Stats:
    //MB/MS
    //8/200
    //16/200
    //32/230
    [Theory]
    [InlineData(LargeFilePath)]
    [InlineData(HugeFilePath)]
    public async Task UploadByChunks(string path)
    {
      var current = 0;
      var bufferSize = 1024 * 1000 * 16; //16MB chunks
      var notifications = 0;
      var progress = new Progress<decimal>(d => notifications++);

      using (var stream = File.OpenRead(path))
      using (var bufferedStream = new ProgressStream(stream, progress, bufferSize))
      {
        int length;
        var buffer = new byte[bufferSize];
        while ((length = await bufferedStream.ReadAsync(buffer, 0, bufferSize)) > 0)
        {
          var result = await Client.PostAsync($"{Constants.FilesRoute}/{Constants.HugeFile}/{current}", new ByteArrayContent(buffer, 0, length));
          Assert.True(result.IsSuccessStatusCode);
        }
      }
    }

    [Theory]
    [InlineData(HugeFilePath)]
    public async Task UploadPush(string path)
    {  
      
      using (var fileStream = File.OpenRead(path))
      using (var bufferedStream = new ProgressStream(fileStream,))
      {
        var httpContent = new PushStreamContent((stream, content, context) =>
        {

        });

      }
    }

    class BufferedStreamWrapper : Stream
    {
      /// <summary>
      /// 8 Megabytes.
      /// </summary>
      public const int DefaultBufferSize = 1024 * 4;// 1024 * 1000 * 8;

      public BufferedStreamWrapper(Stream baseStream, int bufferSize = DefaultBufferSize)
      {
        BaseStream = new BufferedStream(baseStream, bufferSize);
      }

      protected BufferedStream BaseStream { get; }
      public int BufferSize => BaseStream.BufferSize;
      public override bool CanRead => BaseStream.CanRead;
      public override bool CanSeek => BaseStream.CanSeek;
      public override bool CanWrite => BaseStream.CanWrite;
      public override long Length => BaseStream.Length;
      public override long Position { get => BaseStream.Position; set => BaseStream.Position = value; }
      public override void Flush() => BaseStream.Flush();
      public override int Read(byte[] buffer, int offset, int count)
      {
        return BaseStream.Read(buffer, offset, count);
      }
      public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
      {
        return base.ReadAsync(buffer, offset, count, cancellationToken);
      }
      public override Task FlushAsync(CancellationToken cancellationToken)
      {
        return base.FlushAsync(cancellationToken);
      }
      public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
      {
        return base.BeginRead(buffer, offset, count, callback, state);
      }
      public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
      {
        return base.CopyToAsync(destination, bufferSize, cancellationToken);
      }

      public override long Seek(long offset, SeekOrigin origin) => BaseStream.Seek(offset, origin);
      public override void SetLength(long value) => BaseStream.SetLength(value);
      public override void Write(byte[] buffer, int offset, int count) => BaseStream.Write(buffer, offset, count);

      protected override void Dispose(bool disposing)
      {
        BaseStream.Dispose();
        base.Dispose(disposing);
      }
    }

    class ProgressStream : BufferedStreamWrapper
    {
      protected IProgress<decimal> Progress { get; }
      public ProgressStream(FileStream fileStream, IProgress<decimal> progress, int bufferSize = DefaultBufferSize)
        : base(fileStream, bufferSize) =>
        Progress = progress;

      public override int Read(byte[] buffer, int offset, int count)
      {
        ReportProgress();
        var result = base.Read(buffer, offset, count);
        ReportProgress();
        return result;
      }

      public decimal CurrentProgress { get; private set; }

      protected virtual void ReportProgress() =>
        Progress.Report(CurrentProgress = Length == 0 ? 1 : Position / Length);


    }


  }
}
