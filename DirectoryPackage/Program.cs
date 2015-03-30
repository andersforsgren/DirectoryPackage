using System;
using System.IO;
using System.IO.Packaging;
using System.Text;

namespace DirectoryPackage
{
   class Program
   {
      static void Main(string[] args)
      {
         using (Package package = new DirectoryPackage(@"C:\Temp\Testpkg2", FileAccess.ReadWrite))
         {
            var part = package.CreatePart(new Uri("/test2.ext", UriKind.Relative), "text/plain");
            using (var stream = part.GetStream(FileMode.OpenOrCreate))
            using (var sw = new StreamWriter(stream, Encoding.UTF8))
            {
               sw.WriteLine("Test2");
            }

            package.CreateRelationship(new Uri("/test2.ext", UriKind.Relative), TargetMode.Internal, "testRelationship");
         }

         //using (Package package = new DirectoryPackage(@"C:\Temp\Testpkg", FileAccess.Read))
         //{
         //   var part = package.GetPart(new Uri("/test.ext", UriKind.Relative));
         //   using (var stream = part.GetStream(FileMode.Open))
         //   using (var sw = new StreamReader(stream, Encoding.UTF8))
         //   {
         //      Console.WriteLine(sw.ReadToEnd());
         //   }
         //}
         //Console.ReadKey();
      }
   }
}
