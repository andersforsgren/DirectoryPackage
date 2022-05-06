using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace PackagingExtras.Directory
{
   /// <summary>
   ///   Implementation of <see cref="System.IO.Packaging.Package"/> with files stored in a folder rather than in 
   ///   a zip file as is the case with the default implementation <see cref="System.IO.Packaging.ZipPackage"/>.
   /// </summary>
   public class DirectoryPackage : Package
   {
      private static readonly string ContentTypeFileName = "[Content_Types].xml";
      private static readonly string ContentTypesNameSpace = "http://schemas.openxmlformats.org/package/2006/content-types";

      private readonly DirectoryInfo directory;
      private readonly Uri rootUri;
      private readonly ContentTypesTable contentTypes;
      private FileStream contentTypeFile;    // Content type xml is kept open as a lock on the package.
      private string contentTypeFileContent; 
      private DirectoryPackagePropertiesPart propertiesPart;                 

      /// <summary>
      ///   Opens or creates a Package at the given diretory path. 
      /// </summary>
      /// <param name="path">Path to the directory to open or create.</param>
      /// <param name="mode">A <see cref="FileMode"/> constant specifying the mode in which to open the package.</param>
      /// <param name="openFileAccess">File open access for the whole directory.</param>
      /// <param name="share">A <see cref="FileShare"/> constant specifying what filemodes are allowed for other (preceding and subsequent) package opens on the same directory.</param>
      /// <exception cref="ArgumentNullException">Path is null.</exception>
      /// <exception cref="DirectoryPackageException">Thrown if the package is already open in a way that cannot be shared, or if some other IO exception occurs such as a security exception.</exception>
      /// <exception cref="System.IO.DirectoryNotFoundException">Thrown if the package is opened for reading but the directory does not exist.</exception>
      public DirectoryPackage(string path, FileMode mode = FileMode.OpenOrCreate, FileAccess openFileAccess = FileAccess.ReadWrite, FileShare share = FileShare.None)
         : base(openFileAccess)
      {
         if (path == null)
            throw new ArgumentNullException("path");

         if (mode != FileMode.CreateNew && mode != FileMode.Create && mode != FileMode.Open && mode != FileMode.OpenOrCreate)
            throw new ArgumentOutOfRangeException(nameof(mode));

         if (mode == FileMode.CreateNew && System.IO.Directory.Exists(path))
            throw new IOException("Directory already exists at " + path);

         if (mode == FileMode.Create && System.IO.Directory.Exists(path))
            System.IO.Directory.Delete(path, recursive: true); // in lack of a fast .NET API for cleaning, we just drop it and recreate it below

         if (mode == FileMode.Open && !System.IO.Directory.Exists(path))
            throw new DirectoryNotFoundException("Can't find package at " + path);                  

         if (mode == FileMode.CreateNew || mode == FileMode.Create || mode == FileMode.OpenOrCreate)
         {
            if (!System.IO.Directory.Exists(path))
               System.IO.Directory.CreateDirectory(path);
         }

         try
         {
            contentTypeFile = new FileInfo(Path.Combine(path, ContentTypeFileName)).Open(FileMode.OpenOrCreate, openFileAccess, share);
            using (StreamReader reader = new StreamReader(contentTypeFile, Encoding.UTF8, true, 2048, leaveOpen: true))
            {
                contentTypeFileContent = reader.ReadToEnd();
            }
         }
         catch (Exception ex)
         {
            throw new DirectoryPackageException("Error opening package at " + path, ex);
         }
         directory = new DirectoryInfo(path);
         rootUri = new Uri(directory.FullName + Path.DirectorySeparatorChar, UriKind.Absolute);
         contentTypes = new ContentTypesTable(this);

         // Note: The static method `Package.Open` does this for ZipPackage, thus we need to do it for DirectoryPackage, too.
         // Also note that the Package implementation in NetFx/WindowsBase.dll ensures most stuff works even without this call
         // at construction time because it invokes GetPartCore in may more places then the implementation in System.IO.Packagin.dll
         // This divergence in behavior between NetFx and NetCore is really ugly, but we have to live with it :-(
         if (FileOpenAccess.IsRead())
            GetParts();
      }

      protected override PackagePart CreatePartCore(Uri partUri, string contentType, CompressionOption compressionOption)
      {
         if (contentType == null)
            throw new ArgumentNullException("contentType");

         ThrowIfInvalidPartUri(partUri);

         string path = GetPath(partUri);
         string dirName = Path.GetDirectoryName(path);
         if (!System.IO.Directory.Exists(dirName))
         {
            System.IO.Directory.CreateDirectory(dirName);
         }
         File.Create(path).Dispose();

         var ctype = new ContentType(contentType);
         contentTypes.AddContentType(partUri, ctype);

         return CreatePackagePart(partUri, contentType);
      }

      private void ThrowIfInvalidPartUri(Uri partUri)
      {
         if (partUri == null)
            throw new ArgumentNullException("partUri");
         if (partUri.IsAbsoluteUri)
            throw new ArgumentException("Part Uri must be relative", "partUri");
         if (partUri.ToString().EndsWith(ContentTypeFileName, StringComparison.InvariantCultureIgnoreCase))
            throw new ArgumentException("Part uri must not be reserved file name " + ContentTypeFileName);
      }

      protected override PackagePart GetPartCore(Uri partUri)
      {
         ThrowIfInvalidPartUri(partUri);
         var path = GetPath(partUri);
         if (!File.Exists(path))
            return null;
         var ctype = contentTypes.GetContentType(partUri);

         // Ignore parts that have no content types (those are invalid).
         if (ctype == null)
            return null;

         return CreatePackagePart(partUri, ctype.ToString());
      }

      private DirectoryPackagePart CreatePackagePart(Uri partUri, string contentType)
      {
         if (contentType == "application/vnd.openxmlformats-package.core-properties+xml" && partUri.ToString().StartsWith("/package"))
         {
            if (propertiesPart == null)
               propertiesPart = new DirectoryPackagePropertiesPart(this, partUri, contentType);
            return propertiesPart;
         }
         return new DirectoryPackagePart(this, partUri, contentType);
      }

      protected override void DeletePartCore(Uri partUri)
      {
         ThrowIfInvalidPartUri(partUri);
         string path = GetPath(partUri);
         if (File.Exists(path))
            File.Delete(path);
         contentTypes.DeleteOverrideContentType(partUri);
      }

      protected override PackagePart[] GetPartsCore()
      {
         var files = directory.GetFiles("*", SearchOption.AllDirectories);
         return files.Where(file => !IsReservedFile(file))
            .Select(file => GetUri(file.FullName))
            .Select(GetPartCore)
            .Where(part => part != null)
            .ToArray();
      }

      private static bool IsReservedFile(FileInfo file)
      {
         if (file.Name.StartsWith(ContentTypeFileName, StringComparison.CurrentCultureIgnoreCase))
            return true;

         return false;
      }

      protected override void FlushCore()
      {         
      }           
      
      protected override void Dispose(bool disposing)
      {
         try
         {
            if (this.FileOpenAccess.IsWrite())
            {
               contentTypes.SaveToFile(contentTypeFile);

               // Flush properties manually because of bug in Package.cs
               if (propertiesPart != null)
               {
                  propertiesPart.SaveToFile();
               }
            }
         }
         finally
         {
            try
            {
               if (contentTypeFile != null)
                  contentTypeFile.Close();
                    contentTypeFile = null;
            }
            finally
            {
               base.Dispose(disposing);
            }
         }
      }

      internal string GetContentFileContents()
      {
          return this.contentTypeFileContent;
      }

      internal string GetPath(Uri relativeUri)
      {
         var relPath = relativeUri.ToString().Substring(1).Replace('/', Path.DirectorySeparatorChar);
         return Path.Combine(directory.FullName, relPath);
      }

      internal Uri GetUri(string fullPath)
      {
         Uri absUri = new Uri(fullPath, UriKind.Absolute);
         Uri relUri = rootUri.MakeRelativeUri(absUri);
         return PackUriHelper.CreatePartUri(relUri);
      }

      public override string ToString()
      {
         return string.Format("DirectoryPackage [{0}]", directory);
      }

      private class ContentTypesTable
      {
         private readonly Dictionary<string, ContentType> defaultDictionary;
         private readonly Dictionary<string, ContentType> overrideDictionary;
         private readonly DirectoryPackage package;
         private bool isDirty;

         internal ContentTypesTable(DirectoryPackage package)
         {
            this.package = package;
            defaultDictionary = new Dictionary<string, ContentType>(StringComparer.InvariantCultureIgnoreCase);
            overrideDictionary = new Dictionary<string, ContentType>(StringComparer.InvariantCultureIgnoreCase);

            if (package.FileOpenAccess.IsRead())
            {
               ParseContentTypeFile();
            }
         }

         private void ParseContentTypeFile()
         {
            string xml = package.GetContentFileContents();
            if (xml == "")
                return;
            var doc = XDocument.Load(new StringReader(xml));
            foreach (var def in doc.Root.Elements().Where(e => e.Name.LocalName == "Default"))
            {
                defaultDictionary[def.Attribute("Extension").Value] = new ContentType(def.Attribute("ContentType").Value);
            }
            foreach (var def in doc.Root.Elements().Where(e => e.Name.LocalName == "Override"))
            {
                overrideDictionary[def.Attribute("PartName").Value] = new ContentType(def.Attribute("ContentType").Value);
            }            
         }

         internal void AddContentType(Uri partUri, ContentType contentType)
         {
            string extension = partUri.Extension();
            bool makeOverride = false;
            if (extension == "")
            {
               makeOverride = true;
            }
            else if (defaultDictionary.ContainsKey(extension))
            {
               makeOverride = !defaultDictionary[extension].ToString().Equals(contentType.ToString(), StringComparison.InvariantCultureIgnoreCase);
            }

            if (makeOverride)
            {
               overrideDictionary[partUri.ToString()] = contentType;
            }
            else
            {
               defaultDictionary[extension] = contentType;
            }
            isDirty = true;
         }

         internal ContentType GetContentType(Uri partUri)
         {
            ContentType ctype;
            if (overrideDictionary.TryGetValue(partUri.ToString(), out ctype))
               return ctype;

            string ext = partUri.Extension();

            if (overrideDictionary.TryGetValue(partUri.ToString(), out ctype))
               return ctype;

            defaultDictionary.TryGetValue(ext, out ctype);
            return ctype;
         }

         internal void DeleteOverrideContentType(Uri partUri)
         {
            isDirty |= overrideDictionary.Remove(partUri.ToString());
         }

         internal void SaveToFile(Stream stream)
         {
            if (!isDirty)
               return;
            stream.Seek(0, SeekOrigin.Begin);
            using (var writer = new XmlTextWriter(stream, Encoding.UTF8))
            {
                var root = new XElement("Types",
                    new XAttribute("xmlns", ContentTypesNameSpace),
                    defaultDictionary.Select(kvp => new XElement("Default", new XAttribute("Extension", kvp.Key), new XAttribute("ContentType", kvp.Value))).Concat(
                    overrideDictionary.Select(kvp => new XElement("Override", new XAttribute("PartName", kvp.Key), new XAttribute("ContentType", kvp.Value)))).ToArray());
                XDocument doc = new XDocument();
                doc.Add(root);
                doc.WriteTo(writer);
            }            
         }
      }
   }
}
