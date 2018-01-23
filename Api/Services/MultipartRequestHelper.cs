using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Api
{
  using System;
  using System.IO;
  using Microsoft.AspNetCore.Http;
  using Microsoft.Net.Http.Headers;

  public static class MultipartRequestHelper
  {

    // Content-Type: multipart/form-data; boundary="----WebKitFormBoundarymx2fSWqWSd0OxQqq"
    // The spec says 70 characters is a reasonable limit.
    public static string GetBoundary(this MediaTypeHeaderValue contentType, int lengthLimit = 0)
    {
      var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary).Value;
      if (string.IsNullOrWhiteSpace(boundary))
        throw new InvalidDataException("Missing content-type boundary.");

      if (lengthLimit > 0 && boundary.Length > lengthLimit)
        throw new InvalidDataException(
            $"Multipart boundary length limit {lengthLimit} exceeded.");

      return boundary;
    }

    public static bool IsMultipartContentType(string contentType)
    {
      return !string.IsNullOrEmpty(contentType)
          && contentType.IndexOf("multipart/", StringComparison.OrdinalIgnoreCase) >= 0;
    }


    public static bool IsMultipartMimeType(this HttpRequest httpRequest) =>
  httpRequest.ContentType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase);

    public static bool HasFormDataContentDisposition(this ContentDispositionHeaderValue contentDisposition)
    {
      // Content-Disposition: form-data; name="key";
      return contentDisposition != null
             && contentDisposition.DispositionType.Equals("form-data")
             && string.IsNullOrEmpty(contentDisposition.FileName.Value)
             && string.IsNullOrEmpty(contentDisposition.FileNameStar.Value);
    }

    public static bool HasFileContentDisposition(this ContentDispositionHeaderValue contentDisposition)
    {
      // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
      return contentDisposition != null
             && contentDisposition.DispositionType.Equals("attachment")
             && (!string.IsNullOrEmpty(contentDisposition.FileName.Value)
                 || !string.IsNullOrEmpty(contentDisposition.FileNameStar.Value));
    }
  }
}
