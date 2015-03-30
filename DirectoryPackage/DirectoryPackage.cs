using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace DirectoryPackage
{
   /// <summary>
   ///   Implementation of <see cref="System.IO.Packaging.Package"/> with files stored in a folder rather than in 
   ///   a zip file as is the case with the default implementation <see cref="System.IO.Packaging.ZipPackage"/>.
   /// </summary>
   public class DirectoryPackage : Package
   {
      private static readonly string LockFileName = "_DirectoryPackage_lock";
      private static readonly string ContentTypeFileName = "[Content_Types].xml";
      private static readonly string ContentTypesNameSpace = "http://schemas.openxmlformats.org/package/2006/content-types";

      private readonly DirectoryInfo directory;
      private readonly Uri rootUri;
      private readonly ContentTypesTable contentTypes;
      private FileStream lockFile;

      /// <summary>
      ///   Opens or creates a Package at the given diretory path. 
      /// </summary>
      /// <param name="path">Path to the directory to open or create.</param>
      /// <param name="openFileAccess">File open access for the whole directory.</param>
      /// <exception cref="ArgumentNullException">Path is null.</exception>
      /// <exception cref="DirectoryPackageException">Thrown if the package is already open in a way that cannot be shared, or if some other IO exception occurs such as a security exception.</exception>
      /// <exception cref="System.IO.DirectoryNotFoundException">Thrown if the package is opened for reading but the directory does not exist.</exception>
      public DirectoryPackage(string path, FileAccess openFileAccess)
         : base(openFileAccess)
      {
         if (path == null)
            throw new ArgumentNullException(path);

         if (openFileAccess == FileAccess.Read && !Directory.Exists(path))
            throw new DirectoryNotFoundException("Can't find package at " + path);

         var lockFileInfo = new FileInfo(Path.Combine(path, LockFileName));

         if (openFileAccess.IsWrite())
         {
            if (!Directory.Exists(path))
               Directory.CreateDirectory(path);
         }

         try
         {
            lockFile = lockFileInfo.Open(FileMode.OpenOrCreate, openFileAccess, openFileAccess.IsWrite() ? FileShare.None : FileShare.Read);
         }
         catch (Exception ex)
         {
            throw new DirectoryPackageException("Error opening package at " + path, ex);
         }
         directory = new DirectoryInfo(path);
         rootUri = new Uri(directory.FullName, UriKind.Absolute);
         contentTypes = new ContentTypesTable(this);
      }

      protected override PackagePart CreatePartCore(Uri partUri, string contentType, CompressionOption compressionOption)
      {
         if (contentType == null)
            throw new ArgumentNullException("contentType");

         ThrowIfInvalidPartUri(partUri);

         string path = GetPath(partUri);
         string dirName = Path.GetDirectoryName(path);
         if (!Directory.Exists(dirName))
         {
            Directory.CreateDirectory(dirName);
         }
         File.Create(path).Dispose();

         var ctype = new ContentType(contentType);
         contentTypes.AddContentType(partUri, ctype);

         return new DirectoryPackagePart(this, partUri, contentType);
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
         return new DirectoryPackagePart(this, partUri, contentTypes.GetContentType(partUri).ToString());
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
         var files = directory.GetFiles();
         var result = new List<PackagePart>();
         foreach (var file in files)
         {
            if (IsReservedFile(file))
               continue;
            Uri uri = GetUri(file.FullName);
            result.Add(GetPartCore(uri));
         }
         return result.ToArray();
      }

      private bool IsReservedFile(FileInfo file)
      {
         if (file.Name.StartsWith(ContentTypeFileName, StringComparison.CurrentCultureIgnoreCase))
            return true;

         if (file.Name.StartsWith(LockFileName, StringComparison.InvariantCultureIgnoreCase))
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
            contentTypes.SaveToFile();
         }
         finally
         {
            try
            {
               if (lockFile != null)
                  lockFile.Close();
               lockFile = null;
            }
            finally
            {
               base.Dispose(disposing);
            }
         }
      }

      internal FileInfo ContentTypeFile
      {
         get { return new FileInfo(Path.Combine(directory.FullName, ContentTypeFileName)); }
      }

      internal string GetPath(Uri relativeUri)
      {
         var relPath = relativeUri.ToString().Substring(1).Replace('/', Path.DirectorySeparatorChar);
         return Path.Combine(directory.FullName, relPath);
      }

      internal Uri GetUri(string fullPath)
      {
         Uri absUri = new Uri(fullPath, UriKind.Absolute);
         Uri relUri = absUri.MakeRelativeUri(rootUri);
         return relUri;
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
               ParseContentTypeFile(package);
            }
         }

         private void ParseContentTypeFile(DirectoryPackage directoryPackage)
         {
            if (!File.Exists(directoryPackage.ContentTypeFile.FullName))
               return;
            using (var fs = File.OpenRead(directoryPackage.ContentTypeFile.FullName))
            {
               var doc = XDocument.Load(fs);
               foreach (var def in doc.Root.Elements().Where(e => e.Name.LocalName == "Default"))
               {
                  defaultDictionary[def.Attribute("Extension").Value] = new ContentType(def.Attribute("ContentType").Value);
               }
               foreach (var def in doc.Root.Elements().Where(e => e.Name.LocalName == "Override"))
               {
                  overrideDictionary[def.Attribute("PartName").Value] = new ContentType(def.Attribute("ContentType").Value);
               }
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

         internal void SaveToFile()
         {
            if (!isDirty)
               return;

            using (Stream stream = package.ContentTypeFile.Open(FileMode.OpenOrCreate))
            {
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
}