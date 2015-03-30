using System;
using System.IO;
using System.Runtime.Serialization;

namespace DirectoryPackage
{
   [Serializable]
   public class DirectoryPackageException : IOException
   {
      public DirectoryPackageException()
      {
      }

      public DirectoryPackageException(string msg, Exception inner)
         : base(msg, inner)
         
      {         
      }

      public DirectoryPackageException(SerializationInfo info, StreamingContext context)
         : base(info, context)
      {
      }
   }
}