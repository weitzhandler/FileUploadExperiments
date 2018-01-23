using Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http.Features;
using Api.Filters;
using Shared;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using IOFile = System.IO.File;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Globalization;

namespace Api.Controllers
{
  [Route(Constants.FilesRoute)]
  [DisableRequestSizeLimit]
  public class FilesController : Controller
  {
    readonly IOptions<MvcJsonOptions> _JsonOptions;
    readonly ILogger<FilesController> _Logger;

    public FilesController(ILogger<FilesController> logger, IOptions<MvcJsonOptions> jsonOptions)
    {
      _Logger = logger;
      _JsonOptions = jsonOptions;
    }

    /// <summary>
    /// A convenience action to ping API and test connection.
    /// </summary>
    [HttpGet]
    public IActionResult Get() => Ok("OK");


    [HttpPost]
    [Route(Constants.UploadDirect)]
    [DisableFormValueModelBinding]
    public async Task<IActionResult> UploadDirect()
    {
      await SaveFileAsync(Request.Body);

      return Ok();
    }

    [HttpPost]
    [Route(Constants.UploadMultipartDirect)]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadMultipartDirect()
    {
      var boundary = Request.GetMultipartBoundary();
      var reader = new MultipartReader(boundary, Request.Body);

      FileRecord record = default;
      var section = await reader.ReadNextSectionAsync();
      if (!section.IsJson())
        return StatusCode(StatusCodes.Status415UnsupportedMediaType);
      else
        record = section.ReadAs<FileRecord>(_JsonOptions.Value.SerializerSettings);

      section = await reader.ReadNextSectionAsync();
      if (section != null)
        await SaveFileAsync(section.Body, fileName: record.FileId.ToString());

      if (section == null || await reader.ReadNextSectionAsync() != null)
        return StatusCode(StatusCodes.Status207MultiStatus);

      return Ok();
    }

    [HttpPost]
    [Route(Constants.UploadChunk + "/{transactionId}/{chunkId}/{last?}")]
    public async Task<IActionResult> UploadChunk(Guid transactionId, int chunkId, bool last = false)
    {
      //if (!(Request.Body is MemoryStream memoryStream))
      //  return BadRequest();
      var stream = Request.Body;

      var folderName = Path.Combine(TempFolder, transactionId.ToString());
      await SaveFileAsync(stream, folderName, chunkId.ToString());

      if (last)
      {
        var length = await JoinChunksAsync(folderName);
        return Ok(length);
      }

      return Ok();
    }

    [HttpPost]
    [Route(Constants.UploadChunkSingleFile + "/{transactionId}")]
    public async Task<IActionResult> UploadChunkSingleFile(Guid transactionId)
    {
      var stream = Request.Body;
      //if (!(Request.Body is MemoryStream memoryStream))
      //  return BadRequest();

      if (transactionId == Guid.Empty)
        transactionId = Guid.NewGuid();

      //var folderName = Path.Combine(TempFolder, transactionId.ToString());
      await SaveFileAsync(stream, transactionId.ToString(), transactionId.ToString());

      return Ok();
    }

    [HttpPost]
    [Route(Constants.UploadPush + "/{transactionId}")]
    [DisableRequestSizeLimit]
    public async Task<IActionResult> UploadPush(Guid transactionId)
    {
      try
      {
        await SaveFileAsync(Request.Body, fileName: transactionId.ToString());
      }
      catch (Exception e)
      {         
        return StatusCode(StatusCodes.Status500InternalServerError, e);
      }

      return Ok();
    }


    [HttpPost]
    [Route(Constants.UploadAttachment + "/{transactionId}")]
    public async Task<IActionResult> UploadAttachment(Guid transactionId)
    {
      if (!Request.IsMultipartMimeType())
        return StatusCode(StatusCodes.Status415UnsupportedMediaType);

      var requestHeader = MediaTypeHeaderValue.Parse(Request.ContentType);
      var reader = new MultipartReader(requestHeader.Boundary.Value, Request.Body);
      var section = await reader.ReadNextSectionAsync();
      var remoteFile = section.AsFileSection();

      await SaveFileAsync(remoteFile.FileStream, fileName: transactionId.ToString());

      return Ok();
    }


    async Task SaveFileAsync(Stream stream, string directory = null, string fileName = null)
    {
      if (string.IsNullOrWhiteSpace(fileName))
        fileName = $"{Guid.NewGuid()}";

      if (Path.GetExtension(fileName).Length == 0)
        fileName += ".tmp";

      var folder = Path.Combine(TempFolder, directory ?? string.Empty);

      if (!Directory.Exists(folder))
        Directory.CreateDirectory(folder);

      fileName = Path.Combine(folder, fileName);

      using (var fileStream = new FileStream(fileName, FileMode.Append))
        await stream.CopyToAsync(fileStream);
    }

    async Task<long> JoinChunksAsync(string directory)
    {
      //folderName = Path.Combine(TempFolder, folderName);
      var fileName = Path.GetFileName(directory);
      fileName = Path.Combine(directory, $"{fileName}.tmp");
      using (var outputStream = IOFile.OpenWrite(fileName))
      {
        foreach (var tempFile in
         Directory.GetFiles(directory)
         .OrderBy(dir => dir)
         .Except(new[] { fileName }))
        {
          try
          {
            using (var inputStream = IOFile.OpenRead(tempFile))
              await inputStream.CopyToAsync(outputStream);
            IOFile.Delete(tempFile);
          }
          catch
          {
            throw;
          }
        }
        return outputStream.Length;
      }
    }

    static readonly string TempFolder = Path.Combine(Path.GetTempPath(), Constants.FileUploadExperimentVault);



    // 1. Disable the form value model binding here to take control of handling 
    //    potentially large files.
    // 2. Typically antiforgery tokens are sent in request body, but since we 
    //    do not want to read the request body early, the tokens are made to be 
    //    sent via headers. The antiforgery token filter first looks for tokens
    //    in the request header and then falls back to reading the body.
    [HttpPost]
    [DisableFormValueModelBinding]
    //[ValidateAntiForgeryToken]
    [Route(Constants.UploadMultipartOriginal)]
    public async Task<IActionResult> UploadMultipartOriginal()
    {
      if (!MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
      {
        return BadRequest($"Expected a multipart request, but got {Request.ContentType}");
      }

      // Used to accumulate all the form url encoded key value pairs in the 
      // request.
      var formAccumulator = new KeyValueAccumulator();
      string targetFilePath = null;

      var boundary = MultipartRequestHelper.GetBoundary(
          MediaTypeHeaderValue.Parse(Request.ContentType),
          FormOptions.DefaultMultipartBoundaryLengthLimit);
      var reader = new MultipartReader(boundary, HttpContext.Request.Body);

      var section = await reader.ReadNextSectionAsync();
      while (section != null)
      {
        ContentDispositionHeaderValue contentDisposition;
        var hasContentDispositionHeader = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out contentDisposition);

        if (hasContentDispositionHeader)
        {
          if (MultipartRequestHelper.HasFileContentDisposition(contentDisposition))
          {
            targetFilePath = Path.GetTempFileName();
            using (var targetStream = IOFile.Create(targetFilePath))
            {
              await section.Body.CopyToAsync(targetStream);

              _Logger.LogInformation($"Copied the uploaded file '{targetFilePath}'");
            }
          }
          else if (MultipartRequestHelper.HasFormDataContentDisposition(contentDisposition))
          {
            // Content-Disposition: form-data; name="key"
            //
            // value

            // Do not limit the key name length here because the 
            // multipart headers length limit is already in effect.
            var key = HeaderUtilities.RemoveQuotes(contentDisposition.Name);
            var encoding = GetEncoding(section);
            using (var streamReader = new StreamReader(
                section.Body,
                encoding,
                detectEncodingFromByteOrderMarks: true,
                bufferSize: 1024,
                leaveOpen: true))
            {
              // The value length limit is enforced by MultipartBodyLengthLimit
              var value = await streamReader.ReadToEndAsync();
              if (String.Equals(value, "undefined", StringComparison.OrdinalIgnoreCase))
              {
                value = String.Empty;
              }
              formAccumulator.Append(key.Value, value);

              //if (formAccumulator.ValueCount >  _defaultFormOptions.ValueCountLimit)
              //{
              //  throw new InvalidDataException($"Form key count limit {_defaultFormOptions.ValueCountLimit} exceeded.");
              //}
            }
          }
        }

        // Drains any remaining section body that has not been consumed and
        // reads the headers for the next section.
        section = await reader.ReadNextSectionAsync();
      }

      // Bind form data to a model      
      var formValueProvider = new FormValueProvider(
          BindingSource.Form,
          new FormCollection(formAccumulator.GetResults()),
          CultureInfo.CurrentCulture);

      var record = new FileRecord();
      var bindingSuccessful = await TryUpdateModelAsync(record, prefix: "",
          valueProvider: formValueProvider);
      if (!bindingSuccessful)
      {
        if (!ModelState.IsValid)
        {
          return BadRequest(ModelState);
        }
      }                 

      return Json(record);


    }

    static Encoding GetEncoding(MultipartSection section)
    {
      MediaTypeHeaderValue mediaType;
      var hasMediaTypeHeader = MediaTypeHeaderValue.TryParse(section.ContentType, out mediaType);
      // UTF-7 is insecure and should not be honored. UTF-8 will succeed in 
      // most cases.
      if (!hasMediaTypeHeader || Encoding.UTF7.Equals(mediaType.Encoding))
      {
        return Encoding.UTF8;
      }
      return mediaType.Encoding;
    }


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
