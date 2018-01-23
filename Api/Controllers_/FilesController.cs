using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Threading;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Models;
using Newtonsoft.Json;
using Shared;
using System;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

using IOFile = System.IO.File;

namespace Api.Controllers
{
  [Route(Constants.FilesRoute)]
  public class FilesController : Controller
  {
    IOptions<MvcJsonOptions> _Options;
    public FilesController(IOptions<MvcJsonOptions> jsonOptions)
    {
      _Options = jsonOptions;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
      return await Task.FromResult(Ok());
    }

    [HttpPost]
    [Route(Constants.SmallFile)]
    public async Task<IActionResult> PostSmallFile()
    {
      var buffer = new byte[Request.Body.Length];
      await Request.Body.ReadAsync(buffer, 0, buffer.Length);

      var result = Encoding.UTF8.GetString(buffer);

      return await Task.FromResult(Ok(result));
    }


    [HttpPost]
    [Route(Constants.LargeFile + "/{bufferSize}")]
    public async Task<IActionResult> PostLargeFile(int bufferSize)
    {
      var boundary = Request.GetMultipartBoundary();
      var reader = new MultipartReader(boundary, Request.Body);

      FileRecord fileRecord = default;
      var section = await reader.ReadNextSectionAsync();
      if (!section.IsJson())
        return BadRequest();
      else
        fileRecord = section.ReadAs<FileRecord>();

      section = await reader.ReadNextSectionAsync();

      var inputStream = section.Body;
      var folder = Path.Combine(Path.GetTempPath(), "FileUploadExperimentVault");
      if (!Directory.Exists(folder))
        Directory.CreateDirectory(folder);
      var file = Path.Combine(folder, $"{fileRecord.FileId}.tmp");

      using (var outputStream = IOFile.OpenWrite(file))
      //using (var bufferedStream = new BufferedStream(inputStream, BufferSize))
      {
        try
        { 
          

          //await inputStream.CopyToAsync(outputStream, bufferSize);


          while (inputStream.Position < fileRecord.Size)
          {
            var buffer = new byte[bufferSize];
            var length = await inputStream.ReadAsync(buffer, 0, buffer.Length);
            await outputStream.WriteAsync(buffer, 0, length);
            await outputStream.FlushAsync();
          }
        }
        catch (AggregateException e)
        {
          return BadRequest(e.InnerException);
        }
      }


      var info = new FileInfo(file);
      if (info.Length != fileRecord.Size)
        return BadRequest("Size doesn't match.");

      if (await reader.ReadNextSectionAsync() != null)
        return BadRequest();

      return await Task.FromResult(Ok());
    }

    [HttpPost]
    [Route(Constants.HugeFile + "/{transactionId?}")]
    public async Task<IActionResult> UploadInChunks([FromRoute]Guid transactionId)
    {
      if (!(Request.Body is MemoryStream memoryStream))
        return BadRequest();

      var folder = Path.Combine(Path.GetTempPath(), "FileUploadExperimentVault", transactionId.ToString());
      var temp = Path.Combine(folder, $"{Guid.NewGuid()}.tmp");

      if (!Directory.Exists(folder))
        Directory.CreateDirectory(folder);
      var file = Path.GetFileName(temp);
      temp = Path.Combine(folder, file);

      using (var fileStream = IOFile.OpenWrite(temp))
        await memoryStream.CopyToAsync(fileStream);

      return await Task.FromResult(Ok());
    }


    const int BufferSize = 1024 * 4;// 81920; 
  }






}


namespace Microsoft.AspNetCore.WebUtilities
{
  public static class MultipartSectionJsonExtensions
  {
    public static bool IsJson(this MultipartSection section)
    {
      if (section.ContentType == null) return false;

      var contentType = new ContentType(section.ContentType);
      return contentType.MediaType == "application/json";
    }

    public static T ReadAs<T>(this MultipartSection section, JsonSerializerSettings jsonSerializerSettings = null)
    {
      using (var streamReader = new StreamReader(section.Body))
      using (var jsonReader = new JsonTextReader(streamReader))
      {
        var serializer =
          jsonSerializerSettings == null
          ? JsonSerializer.CreateDefault()
          : JsonSerializer.Create(jsonSerializerSettings);
        var model = serializer.Deserialize<T>(jsonReader);
        return model;
      }
    }

  }
}
