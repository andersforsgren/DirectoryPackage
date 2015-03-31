using System;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;

namespace PackagingExtras.Directory
{
   public class DirectoryPackagePart : PackagePart
   {
      public DirectoryPackagePart(DirectoryPackage package, Uri partUri, string contentType)
         : base(package, partUri, contentType)
      {
      }

      protected DirectoryPackage DirectoryPackage
      {
         get { return (DirectoryPackage)Package; }
      }

      protected override Stream GetStreamCore(FileMode mode, FileAccess access)
      {         
         return new FileStream(DirectoryPackage.GetPath(Uri), mode, access);
      }
   }

   /// <summary>
   ///   This contraption only exists to work around a bug in the BCL where the Package
   ///   might get the properties stream several times without closing.   
   /// </summary>
   internal class DirectoryPackagePropertiesPart : DirectoryPackagePart
   {
      private PropertiesStream stream;
      private readonly string filePath;
      
      internal DirectoryPackagePropertiesPart(DirectoryPackage package, Uri partUri, string contentType)
         : base(package, partUri, contentType)
      {
         filePath = package.GetPath(partUri);
      }

      internal byte[] PropertiesData { get; private set; }
       
      protected override Stream GetStreamCore(FileMode mode, FileAccess access)
      {
         // If reading, store the file contents in a memory stream and return it.
         // If writing, let the writer write to the MemoryStream only. We will flush it to disk later.
         stream = new PropertiesStream(this);   
         if (access == FileAccess.Read && mode == FileMode.Open)
         {                 
            using (Stream fs = base.GetStreamCore(mode, access))
            {
               fs.CopyTo(stream);
               stream.Seek(0, 0);
            }            
         }         
         return stream;
      }

      public void SaveToFile()
      {
         if (stream == null)
            return;
         
         using (var fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
         {
            fs.Write(PropertiesData, 0, PropertiesData.Length);
         }
      }

      // A memory stream that reports the last written data to the part when it is closed.
      private class PropertiesStream : MemoryStream
      {
         private readonly DirectoryPackagePropertiesPart part;
         internal PropertiesStream(DirectoryPackagePropertiesPart part)
         {
            this.part = part;
         }         
         protected override void Dispose(bool disposing)
         {
            part.PropertiesData = ToArray();
            base.Dispose(disposing);
         }
      }
   }  
}