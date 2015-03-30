using System;

namespace DirectoryPackage
{
   internal static class UriExtensions
   {
      internal static string Extension(this Uri uri)
      {
         string uriString = uri.ToString();
         int lastDot = uriString.LastIndexOf(".", StringComparison.InvariantCulture);
         if (lastDot < 0 || lastDot == uriString.Length - 1)
            return "";
         return uri.ToString().Substring(lastDot + 1);
      }
   }
}