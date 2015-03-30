using System;
using System.IO;
using System.IO.Packaging;

namespace DirectoryPackage
{
   public sealed class DirectoryPackagePart : PackagePart
   {
      public DirectoryPackagePart(DirectoryPackage package, Uri partUri, string contentType)
         : base(package, partUri, contentType)
      {
      }

      private DirectoryPackage DirectoryPackage
      {
         get { return (DirectoryPackage)Package; }
      }

      protected override Stream GetStreamCore(FileMode mode, FileAccess access)
      {
         return new FileStream(DirectoryPackage.GetPath(Uri), mode, access);
      }
   }
}