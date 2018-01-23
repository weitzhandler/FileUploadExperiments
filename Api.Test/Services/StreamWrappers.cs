using System.Threading;
using System.Threading.Tasks;

namespace System.IO
{
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
    public override bool CanRead => BaseStream.CanRead;
    public override bool CanSeek => BaseStream.CanSeek;
    public override bool CanWrite => BaseStream.CanWrite;
    public override long Length => BaseStream.Length;
    public override long Position { get => BaseStream.Position; set => BaseStream.Position = value; }
    public override void Flush() => BaseStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => BaseStream.Seek(offset, origin);
    public override void SetLength(long value) => BaseStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => BaseStream.Write(buffer, offset, count);
    public override int Read(byte[] buffer, int offset, int count) => BaseStream.Read(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
      BaseStream.Dispose();
      base.Dispose(disposing);
    }          
  }

  class ProgressStream : BufferedStreamWrapper
  {
    protected IProgress<decimal> Progress { get; }
    public ProgressStream(Stream stream, IProgress<decimal> progress, int bufferSize = DefaultBufferSize)
      : base(stream, bufferSize) =>
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
      Progress.Report(CurrentProgress = Length == 0 ? 1 : (decimal) Position / Length);


  }

}
