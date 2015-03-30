using System;
using System.IO;
using System.IO.Packaging;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DirectoryPackage.Tests
{
   [TestClass]
   public class TestDirectoryPackage
   {
      private string dirName;
      private string root;
      private string path;


      [TestInitialize]
      public void Setup()
      {
         dirName = "TestDirectoryPkg_" + Path.GetRandomFileName();
         root = Path.GetTempPath();
         path = Path.Combine(root, dirName);
      }

      [TestCleanup]
      public void TearDown()
      {
         DirectoryInfo di = new DirectoryInfo(path);
         foreach (FileInfo file in di.GetFiles())
         {
            file.Delete();
         }
         foreach (DirectoryInfo dir in di.GetDirectories())
         {
            dir.Delete(true);
         }
         di.Delete(false);
      }

      internal string GetPath(Uri relativeUri)
      {
         var relPath = relativeUri.ToString().Substring(1).Replace('/', Path.DirectorySeparatorChar);
         return Path.Combine(path, relPath);
      }

      [TestMethod]
      public void TestCreatePacakge()
      {
         using (new DirectoryPackage(path, FileAccess.ReadWrite))
         {
            Assert.IsTrue(Directory.Exists(path), "Expected package constructor to create directory");
         }
         Assert.IsTrue(Directory.Exists(path), "Expected package dir not to be deleted at package close");
      }

      [TestMethod]
      public void TestCreatePart()
      {
         string partFile = "/test.ext";
         Uri partUri = new Uri(partFile, UriKind.Relative);
         using (Package package = new DirectoryPackage(path, FileAccess.ReadWrite))
         {
            var part = package.CreatePart(partUri, "text/plain");
            Assert.IsNotNull(part, "Failed to create part "+partUri);
            using (var stream = part.GetStream(FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(stream, Encoding.UTF8))
            {
               sw.Write("SomeTest");
            }

            Assert.IsTrue(File.Exists(GetPath(part.Uri)), "Expected create part to create a file.");
            Assert.AreEqual("text/plain", part.ContentType, "Part had unexpected content type");
            package.CreateRelationship(new Uri("/test.ext", UriKind.Relative), TargetMode.Internal, "testRelationship");
         }
         using (Package package = new DirectoryPackage(path, FileAccess.Read))
         {
            Assert.IsTrue(package.PartExists(partUri), "Expected part to persist.");
            var part = package.GetPart(partUri);
            Assert.IsTrue(File.Exists(GetPath(part.Uri)), "Expected part file to exist at "+partUri+"->"+GetPath(partUri));
            Assert.AreEqual("SomeTest", File.ReadAllText(GetPath(part.Uri)), "Part file contents differs!");
            Assert.AreEqual("text/plain", part.ContentType, "Part had unexpected content type after reopen");
         }
      }

      [TestMethod]
      public void TestDeletePart()
      {
         string partFile = "/test.ext";
         Uri partUri = new Uri(partFile, UriKind.Relative);
         using (Package package = new DirectoryPackage(path, FileAccess.ReadWrite))
         {
            var part = package.CreatePart(partUri, "text/plain");
            Assert.IsNotNull(part, "Failed to create part " + partUri);
            using (var stream = part.GetStream(FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(stream, Encoding.UTF8))
            {
               sw.Write("SomeTest");
            }
         }
         using (Package package = new DirectoryPackage(path, FileAccess.ReadWrite))
         {
            Assert.IsTrue(package.PartExists(partUri), "Expected part to persist.");
            var part = package.GetPart(partUri);
            Assert.IsTrue(File.Exists(GetPath(part.Uri)), "Expected part file to exist at " + partUri + "->" + GetPath(partUri));
            Assert.AreEqual("SomeTest", File.ReadAllText(GetPath(part.Uri)), "Part file contents differs!");
            Assert.AreEqual("text/plain", part.ContentType, "Part had unexpected content type after reopen");
            package.DeletePart(partUri);
            Assert.IsFalse(package.PartExists(partUri), "Expected part not to exist after delete");
            Assert.IsFalse(File.Exists(GetPath(partUri)), "Expected file to be deleted when part is deleted");
         }
      }

      [TestMethod]
      public void TestCreateRelationship()
      {
         PackageRelationship rel;
         using (Package package = new DirectoryPackage(path, FileAccess.ReadWrite))
         {
            string partFile = "/test.ext";
            Uri partUri = new Uri(partFile, UriKind.Relative);    
            var part = package.CreatePart(partUri, "text/plain");
            Assert.IsNotNull(part, "Failed to create part " + partUri);
            using (var stream = part.GetStream(FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(stream, Encoding.UTF8))
            {
               sw.WriteLine("SomeTest");
            }

            rel = package.CreateRelationship(new Uri("/test.ext", UriKind.Relative), TargetMode.Internal, "testRelationship");
            Assert.IsTrue(package.RelationshipExists(rel.Id), "Expected relationship to exist in package after being created");
         }

         // Re-open package
         using (Package package = new DirectoryPackage(path, FileAccess.Read))
         {
            var rel2 = package.GetRelationship(rel.Id);
            Assert.IsNotNull(rel2, "Expected relationship to be persisted");
            Assert.AreEqual(rel.RelationshipType, rel2.RelationshipType, "Relationship type was not persisted");
            Assert.AreEqual(rel.TargetUri, rel2.TargetUri, "Relationship type was not persisted");
         }
      }

      [TestMethod]
      public void TestContentTypeOverride()
      {
         string part1File = "/test1.ext";
         string part2File = "/test2.ext";
         Uri part1Uri = new Uri(part1File, UriKind.Relative);
         Uri part2Uri = new Uri(part2File, UriKind.Relative);

         using (Package package = new DirectoryPackage(path, FileAccess.ReadWrite))
         {
            var part = package.CreatePart(part1Uri, "text/plain");
            using (var stream = part.GetStream(FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(stream, Encoding.UTF8))
            {
               sw.Write("SomeTest");
            }

            var part2 = package.CreatePart(part2Uri, "image/jpeg");

            Assert.AreEqual("text/plain", part.ContentType);
            Assert.AreEqual("image/jpeg", part2.ContentType);
         }

         // Reopen & reassert.
         using (Package package = new DirectoryPackage(path, FileAccess.ReadWrite))
         {
            Assert.AreEqual("text/plain", package.GetPart(part1Uri).ContentType, "Default content type was not persisted");
            Assert.AreEqual("image/jpeg", package.GetPart(part2Uri).ContentType, "Override content type was not persisted");
         }
      }

      [TestMethod]
      public void TestOpenReadCanShare()
      {
         using (Package p1 = new DirectoryPackage(path, FileAccess.Write))
         {
         }
         using (Package p1= new DirectoryPackage(path, FileAccess.Read))
         {
            using (Package p2 = new DirectoryPackage(path, FileAccess.Read))
            {
            }
         }
      }

      [TestMethod]
      [ExpectedException(typeof(DirectoryPackageException))]
      public void TestOpenReadAfterWriteThrows()
      {
         using (Package p1 = new DirectoryPackage(path, FileAccess.ReadWrite))
         {
            using (Package p2 = new DirectoryPackage(path, FileAccess.Read))
            {
            }
         }
      }

      [TestMethod]
      [ExpectedException(typeof(DirectoryPackageException))]
      public void TestOpenWriteAfterReadThrows()
      {
         using (Package p1 = new DirectoryPackage(path, FileAccess.Write))
         {
         }

         using (Package p1 = new DirectoryPackage(path, FileAccess.Read))
         {
            using (Package p2 = new DirectoryPackage(path, FileAccess.ReadWrite))
            {
            }
         }
      }
   }
}
