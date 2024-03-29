﻿using System;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PackagingExtras.Directory;

namespace Tests
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

      [DataTestMethod]
      [DataRow(FileMode.OpenOrCreate)]
      [DataRow(FileMode.Create)]
      [DataRow(FileMode.CreateNew)]
      public void TestCreatePacakge_CreatesDirectory(FileMode mode)
      {
         using (new DirectoryPackage(path, mode))
         {
            Assert.IsTrue(System.IO.Directory.Exists(path), "Expected package constructor to create directory");
         }
         Assert.IsTrue(System.IO.Directory.Exists(path), "Expected package dir not to be deleted at package close");
      }

      [TestMethod]
      public void TestCreatePacakge_EnsuresEmptyDirectory()
      {
         CreateDummyPackageDir(path);
         using (var package = new DirectoryPackage(path, FileMode.Create))
         {
            Assert.AreEqual(0, package.GetParts().Count(), "Expected package to be empty.");
         }
      }

      [DataTestMethod]
      [DataRow(FileMode.OpenOrCreate)]
      [DataRow(FileMode.Open)]
      public void TestCreatePacakge_KeepsExistingContent(FileMode mode)
      {
          CreateDummyPackageDir(path);
         using (var package = new DirectoryPackage(path, mode))
         {
            Assert.AreEqual(1, package.GetParts().Count(), "Expected package to have parts.");
         }
      }

      [TestMethod]
      public void TestCreatePart()
      {
         string partFile = "/test.ext";
         Uri partUri = new Uri(partFile, UriKind.Relative);
         using (Package package = new DirectoryPackage(path))
         {
            var part = package.CreatePart(partUri, "text/plain");
            Assert.IsNotNull(part, "Failed to create part " + partUri);
            using (var stream = part.GetStream(FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(stream, Encoding.UTF8))
            {
               sw.Write("SomeTest");
            }

            Assert.IsTrue(File.Exists(GetPath(part.Uri)), "Expected create part to create a file.");
            Assert.AreEqual("text/plain", part.ContentType, "Part had unexpected content type");
            package.CreateRelationship(new Uri("/test.ext", UriKind.Relative), TargetMode.Internal, "testRelationship");
         }
         using (Package package = new DirectoryPackage(path, FileMode.Open))
         {
            Assert.IsTrue(package.PartExists(partUri), "Expected part to persist.");
            var part = package.GetPart(partUri);
            Assert.IsTrue(File.Exists(GetPath(part.Uri)), "Expected part file to exist at " + partUri + "->" + GetPath(partUri));
            Assert.AreEqual("SomeTest", File.ReadAllText(GetPath(part.Uri)), "Part file contents differs!");
            Assert.AreEqual("text/plain", part.ContentType, "Part had unexpected content type after reopen");
         }
      }

      [TestMethod]
      public void TestDeletePart()
      {
         string partFile = "/test.ext";
         Uri partUri = new Uri(partFile, UriKind.Relative);
         using (Package package = new DirectoryPackage(path))
         {
            var part = package.CreatePart(partUri, "text/plain");
            Assert.IsNotNull(part, "Failed to create part " + partUri);
            using (var stream = part.GetStream(FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(stream, Encoding.UTF8))
            {
               sw.Write("SomeTest");
            }
         }
         using (Package package = new DirectoryPackage(path, FileMode.Open))
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
         using (Package package = new DirectoryPackage(path))
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
         using (Package package = new DirectoryPackage(path, FileMode.Open))
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

         using (Package package = new DirectoryPackage(path))
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
         using (Package package = new DirectoryPackage(path, FileMode.Open))
         {
            Assert.AreEqual("text/plain", package.GetPart(part1Uri).ContentType, "Default content type was not persisted");
            Assert.AreEqual("image/jpeg", package.GetPart(part2Uri).ContentType, "Override content type was not persisted");
         }
      }

      [TestMethod]
      public void TestPackageProperties()
      {
         using (var pkg = new DirectoryPackage(path))
         {
            pkg.PackageProperties.Creator = "Value1";
            pkg.Flush();
            pkg.PackageProperties.Creator = "Value2";
         }
         using (var pk2 = new DirectoryPackage(path, FileMode.Open))
         {
            Assert.AreEqual("Value2", pk2.PackageProperties.Creator);            
         }
      }

      [TestMethod]
      public void TestOpenReadCanShare()
      {
         using (Package p1 = new DirectoryPackage(path))
         {
         }
         using (Package p1 = new DirectoryPackage(path, FileMode.Open, FileAccess.Read, FileShare.Read))
         {
            using (Package p2 = new DirectoryPackage(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
            }
         }
      }

      [TestMethod]      
      public void TestOpenReadAfterWriteWithShareDoesNotThrow()
      {
         using (Package p1 = new DirectoryPackage(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
         {
            using (Package p2 = new DirectoryPackage(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
            }
         }
      }

      [TestMethod]
      [ExpectedException(typeof(DirectoryPackageException))]
      public void TestOpenReadAfterWriteWithoutShareThrows()
      {
         using (Package p1 = new DirectoryPackage(path))
         {
         }

         using (Package p1 = new DirectoryPackage(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
         {
            using (Package p2 = new DirectoryPackage(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
            }
         }
      }


      [TestMethod]
      [ExpectedException(typeof(DirectoryPackageException))]
      public void TestOpenWriteAfterReadThrows()
      {
         using (Package p1 = new DirectoryPackage(path))
         {
         }

         using (Package p1 = new DirectoryPackage(path, FileMode.Open, FileAccess.Read, FileShare.Read))
         {
            using (Package p2 = new DirectoryPackage(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
            }
         }
      }

      /// <summary>
      ///   Tests loading existing OPC files to compare that a DirectoryPackage can be loaded
      ///   and is equal to the original file package.
      /// 
      ///   Test files are from http://blogs.msdn.com/b/dmahugh/archive/2007/09/11/open-xml-implementation-test-documents.aspx
      /// </summary>
      [DeploymentItem(@"testdocuments\", @"testdocuments\")]
      [TestMethod]
      public void TestDocuments()
      {
         foreach (var file in System.IO.Directory.GetFiles("testdocuments"))
         {
            // Extract the contents to a directory
            System.IO.Compression.ZipFile.ExtractToDirectory(file, Path.Combine(path, file));

            using (Package zipPackage = Package.Open(file, FileMode.Open))
            {
               // Check that we can open the package as a directory.
               using (DirectoryPackage directoryPackage = new DirectoryPackage(Path.Combine(path, file), FileMode.Open))
               {
                  AssertPackagesEqual(zipPackage, directoryPackage);
               }
            }
         }
      }

      private static void CreateDummyPackageDir(string path)
      {
          Directory.CreateDirectory(path);
          File.WriteAllText(Path.Combine(path, "dummyPart.txt"), 
              "hello world");
          File.WriteAllText(Path.Combine(path, "[Content_Types].xml"), 
              "<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Default Extension='txt' ContentType='text/plain' /></Types>");
      }

      private static void AssertPackagesEqual(Package x, Package y)
      {
         // Compare package level relationships
         Assert.AreEqual(x.GetRelationships().Count(), y.GetRelationships().Count(), "Relationship count differs");
         foreach (var xRel in x.GetRelationships())
         {
            var yRel = y.GetRelationship(xRel.Id);
            AssertRelationsEqual(xRel, yRel);
         }

         Assert.AreEqual(x.GetParts().Count(), y.GetParts().Count(), "Part count differs");
         foreach (var xPart in x.GetParts())
         {
            var yPart = y.GetPart(xPart.Uri);
            Assert.IsNotNull(yPart, "Part " + xPart.Uri + " missing from second package");
            Assert.AreEqual(xPart.ContentType, yPart.ContentType, "Parts content type differ");

            // Compare part relationships, unless it's a relationship part itself
            if (!PackUriHelper.IsRelationshipPartUri(xPart.Uri))
            {
               Assert.AreEqual(xPart.GetRelationships().Count(), yPart.GetRelationships().Count(),
                  "Wrong number of rels for part " + xPart.Uri);

               foreach (var xRel in xPart.GetRelationships())
               {
                  var yRel = yPart.GetRelationship(xRel.Id);
                  AssertRelationsEqual(xRel, yRel);
               }
            }
         }
         // Compare package properties.
         Expression<Func<Package, object>>[] props =
            {
               p => p.PackageProperties.Category,
               p => p.PackageProperties.ContentStatus,
               p => p.PackageProperties.ContentType,
               p => p.PackageProperties.Created,
               p => p.PackageProperties.Creator,
               p => p.PackageProperties.Description,
               p => p.PackageProperties.Identifier,
               p => p.PackageProperties.Keywords,
               p => p.PackageProperties.Language,
               p => p.PackageProperties.LastModifiedBy,
               p => p.PackageProperties.LastPrinted,
               p => p.PackageProperties.Modified,
               p => p.PackageProperties.Revision,
               p => p.PackageProperties.Subject,
               p => p.PackageProperties.Title,
               p => p.PackageProperties.Version
            };

         foreach (var expr in props)
         {
            var get = expr.Compile();
            Assert.AreEqual(get(x), get(y), "Difference in package property: " + expr);
         }
      }

      private static void AssertRelationsEqual(PackageRelationship xRel, PackageRelationship yRel)
      {
         Assert.IsNotNull(yRel, "Relationship " + yRel + " missing from second package/part");
         Assert.AreEqual(xRel.RelationshipType, yRel.RelationshipType, "Relationship type differs for " + xRel);
         Assert.AreEqual(xRel.SourceUri, yRel.SourceUri, "Uri difference in relationship");
         Assert.AreEqual(xRel.TargetUri, yRel.TargetUri, "Uri difference in relationship");
         Assert.AreEqual(xRel.TargetMode, yRel.TargetMode, "Target mode differs");
      }
   }
}
