﻿using System.IO;

namespace PackagingExtras.Directory
{
   internal static class FileAccessExtensions
   {
      public static bool IsRead(this FileAccess access)
      {
         return (access & FileAccess.Read) == FileAccess.Read;
      }

      public static bool IsWrite(this FileAccess access)
      {
         return (access & FileAccess.Write) == FileAccess.Write;
      }
   }
}