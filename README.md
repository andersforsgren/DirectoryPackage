# DirectoryPackage
System.IO.Packaging.Package implementation with an extracted directory as physical storage

Intended as a drop-in replacement for the ZipPackage type created by `Package.Open` in System.IO.Packaging. Having an extracted directory rather than a zip archive can be useeful when

 - Debugging, to see the package contents 
 - High performance package modification is required. Applications can keep a consistent document model and zip only when required (for transfer). An open document can be mapped to an extracted directory instead.

## Usage

    using(var pkg = new DirectoryPackage(pathToDirectory, ...))
    {
        // Same code as when using Package.Open
    }
    
## Limitations

The entire spec of OPC (http://www.ecma-international.org/news/TC45_current_work/tc45-2006-335.pdf) is *not* yet implemented. This means some external packages may not be readable. For example, package parts with multiple pieces are not yet supported. Packages created with this class are always readable.

   
