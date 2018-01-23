using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Api.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Shared.Models;
using System.Net.Http;
using System.Net.Http.Headers;

using IOFile = System.IO.File;

namespace Api.Controllers
{
  [Route("Upload")]
  public class UploadController : Controller
  {
    readonly IHostingEnvironment _HostingEnvironment;
    public UploadController(IHostingEnvironment hostingEnvironment)
    {
      _HostingEnvironment = hostingEnvironment;
    }
        
    [HttpPost]
    public async Task<IActionResult> Post()
    {
      // Check whether the POST operation is MultiPart?
      //if (!Request.body.IsMimeMultipartContent())
      //{
      //  return StatusCode(StatusCodes.Status415UnsupportedMediaType);
      //}

      // Prepare CustomMultipartFormDataStreamProvider in which our multipart form
      // data will be loaded.
      string fileSaveLocation = _HostingEnvironment.ContentRootPath;
      CustomMultipartFormDataStreamProvider provider = new CustomMultipartFormDataStreamProvider(fileSaveLocation);
      List<string> files = new List<string>();

      try
      {
        // Read all contents of multipart message into CustomMultipartFormDataStreamProvider.
        var reader = new MultipartReader("", Request.Body);
        var section = await reader.ReadNextSectionAsync();

        var fileSection = section.AsFileSection();
          files.Add(Path.GetFileName(fileSection.FileName));     

        // Send OK Response along with saved file names to the client.
        return Ok(files);
      }
      catch (System.Exception e)
      {
        return StatusCode(StatusCodes.Status500InternalServerError, e);
      }
    }
  }

  // We implement MultipartFormDataStreamProvider to override the filename of File which
  // will be stored on server, or else the default name will be of the format like Body-
  // Part_{GUID}. In the following implementation we simply get the FileName from 
  // ContentDisposition Header of the Request Body.
  public class CustomMultipartFormDataStreamProvider : MultipartFormDataStreamProvider
  {
    public CustomMultipartFormDataStreamProvider(string path) : base(path) { }

    public override string GetLocalFileName(HttpContentHeaders headers)
    {
      return headers.ContentDisposition.FileName.Replace("\"", string.Empty);
    }
  }
}