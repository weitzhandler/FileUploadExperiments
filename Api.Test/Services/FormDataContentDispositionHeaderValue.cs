using System;
using System.Collections.Generic;
using System.Text;

namespace System.Net.Http.Headers
{
  public class FormDataContentDispositionHeaderValue : ContentDispositionHeaderValue
  {
    public FormDataContentDispositionHeaderValue() : base("form-data")
    {
    }
  }
}
