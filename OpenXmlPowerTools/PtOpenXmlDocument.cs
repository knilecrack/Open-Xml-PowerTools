// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

/*
Here is modification of a WmlDocument:
    public static WmlDocument SimplifyMarkup(WmlDocument doc, SimplifyMarkupSettings settings)
    {
        using (OpenXmlMemoryStreamDocument streamDoc = new OpenXmlMemoryStreamDocument(doc))
        {
            using (WordprocessingDocument document = streamDoc.GetWordprocessingDocument())
            {
                SimplifyMarkup(document, settings);
            }
            return streamDoc.GetModifiedWmlDocument();
        }
    }

Here is read-only of a WmlDocument:

    public static string GetBackgroundColor(WmlDocument doc)
    {
        using (OpenXmlMemoryStreamDocument streamDoc = new OpenXmlMemoryStreamDocument(doc))
        using (WordprocessingDocument document = streamDoc.GetWordprocessingDocument())
        {
            XDocument mainDocument = document.MainDocumentPart.GetXDocument();
            XElement backgroundElement = mainDocument.Descendants(W.background).FirstOrDefault();
            return (backgroundElement == null) ? string.Empty : backgroundElement.Attribute(W.color).Value;
        }
    }

Here is creating a new WmlDocument:

    private OpenXmlPowerToolsDocument CreateSplitDocument(WordprocessingDocument source, List<XElement> contents, string newFileName)
    {
        using (OpenXmlMemoryStreamDocument streamDoc = OpenXmlMemoryStreamDocument.CreateWordprocessingDocument())
        {
            using (WordprocessingDocument document = streamDoc.GetWordprocessingDocument())
            {
                DocumentBuilder.FixRanges(source.MainDocumentPart.GetXDocument(), contents);
                PowerToolsExtensions.SetContent(document, contents);
            }
            OpenXmlPowerToolsDocument newDoc = streamDoc.GetModifiedDocument();
            newDoc.FileName = newFileName;
            return newDoc;
        }
    }
*/

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.IO.Packaging;
using DocumentFormat.OpenXml.Packaging;
using System.Threading.Tasks;
using System.Collections.Frozen;

namespace OpenXmlPowerTools;

public class PowerToolsDocumentException : Exception
{
    public PowerToolsDocumentException(string message) : base(message) { }

    public PowerToolsDocumentException() : base()
    {
    }

    public PowerToolsDocumentException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
public class PowerToolsInvalidDataException : Exception
{
    public PowerToolsInvalidDataException(string message) : base(message) { }

    public PowerToolsInvalidDataException() : base()
    {
    }

    public PowerToolsInvalidDataException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}

public class OpenXmlPowerToolsDocument
{
    private string? _fileName;

    public string FileName
    {
        get => _fileName ?? string.Empty;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                //random filename with docx extension
                _fileName = Path.ChangeExtension(Path.GetRandomFileName(), ".docx");
            }
            else
            {
                _fileName = value;
            }
        }
    }

    public byte[] DocumentByteArray { get; set; } = [];
    public Memory<byte>? Document { get; set; }
    public static OpenXmlPowerToolsDocument FromFileName(string fileName)
    {
        byte[] bytes = File.ReadAllBytes(fileName);
        Type type;
        try
        {
            type = GetDocumentType(bytes);
        }
        catch (FileFormatException)
        {
            throw new PowerToolsDocumentException("Not an Open XML document.");
        }
        if (type == typeof(WordprocessingDocument))
            return new WmlDocument(fileName, bytes);
        if (type == typeof(SpreadsheetDocument))
            return new SmlDocument(fileName, bytes);
        if (type == typeof(PresentationDocument))
            return new PmlDocument(fileName, bytes);
        if (type == typeof(Package))
        {
            OpenXmlPowerToolsDocument pkg = new OpenXmlPowerToolsDocument(bytes);
            pkg.FileName = fileName;
            return pkg;
        }
        throw new PowerToolsDocumentException("Not an Open XML document.");
    }

    public static async Task<OpenXmlPowerToolsDocument> FromFileNameAsync(string fileName)
    {
        byte[] bytes = await File.ReadAllBytesAsync(fileName);
        Type type;
        try
        {
            type = GetDocumentType(bytes);
        }
        catch (FileFormatException)
        {
            throw new PowerToolsDocumentException("Not an Open XML document.");
        }
        if (type == typeof(WordprocessingDocument))
            return new WmlDocument(fileName, bytes);
        if (type == typeof(SpreadsheetDocument))
            return new SmlDocument(fileName, bytes);
        if (type == typeof(PresentationDocument))
            return new PmlDocument(fileName, bytes);
        if (type == typeof(Package))
        {
            OpenXmlPowerToolsDocument pkg = new OpenXmlPowerToolsDocument(bytes);
            pkg.FileName = fileName;
            return pkg;
        }
        throw new PowerToolsDocumentException("Not an Open XML document.");

    }

    public static OpenXmlPowerToolsDocument FromDocument(OpenXmlPowerToolsDocument doc)
    {
        Type type = doc.GetDocumentType();
        if (type == typeof(WordprocessingDocument))
            return new WmlDocument(doc);
        if (type == typeof(SpreadsheetDocument))
            return new SmlDocument(doc);
        if (type == typeof(PresentationDocument))
            return new PmlDocument(doc);

        // This should not be possible from a valid OpenXmlPowerToolsDocument object
        throw new NotSupportedException("This should not be possible from a valid OpenXmlPowerToolsDocument object");
    }

    public OpenXmlPowerToolsDocument(OpenXmlPowerToolsDocument? original)
    {
        ArgumentNullException.ThrowIfNull(original);
        DocumentByteArray = original.DocumentByteArray.ToArray();
        FileName = original.FileName;
    }

    public OpenXmlPowerToolsDocument(OpenXmlPowerToolsDocument original, bool convertToTransitional)
    {
        ArgumentNullException.ThrowIfNull(original.FileName);
        if (convertToTransitional)
        {
            ConvertToTransitional(original.FileName, original.DocumentByteArray);
        }
        else
        {
            DocumentByteArray = new byte[original.DocumentByteArray.Length];
            Array.Copy(original.DocumentByteArray, DocumentByteArray, original.DocumentByteArray.Length);
            FileName = original.FileName;
        }
    }

    public OpenXmlPowerToolsDocument(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        this.FileName = fileName;
        DocumentByteArray = File.ReadAllBytes(fileName);
    }

    public OpenXmlPowerToolsDocument(string fileName, bool convertToTransitional)
    {
        this.FileName = fileName;

        if (convertToTransitional)
        {
            var tempByteArray = File.ReadAllBytes(fileName);
            ConvertToTransitional(fileName, tempByteArray);
        }
        else
        {
            this.FileName = fileName;
            DocumentByteArray = File.ReadAllBytes(fileName);
        }
    }

    private void ConvertToTransitional(string fileName, byte[] tempByteArray)
    {
        Type type;
        try
        {
            type = GetDocumentType(tempByteArray);
        }
        catch (FileFormatException)
        {
            throw new PowerToolsDocumentException("Not an Open XML document.");
        }

        using (MemoryStream ms = new MemoryStream())
        {
            ms.Write(tempByteArray, 0, tempByteArray.Length);
            if (type == typeof(WordprocessingDocument))
            {
                using (WordprocessingDocument sDoc = WordprocessingDocument.Open(ms, true))
                {
                    // following code forces the SDK to serialize
                    foreach (var part in sDoc.Parts)
                    {
                        try
                        {
                            var z = part.OpenXmlPart.RootElement;
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    sDoc.Save();
                }
            }
            else if (type == typeof(SpreadsheetDocument))
            {
                using (SpreadsheetDocument sDoc = SpreadsheetDocument.Open(ms, true))
                {
                    // following code forces the SDK to serialize
                    foreach (var part in sDoc.Parts)
                    {
                        try
                        {
                            var z = part.OpenXmlPart.RootElement;
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    sDoc.Save();
                }
            }
            else if (type == typeof(PresentationDocument))
            {
                using (PresentationDocument sDoc = PresentationDocument.Open(ms, true))
                {
                    // following code forces the SDK to serialize
                    foreach (var part in sDoc.Parts)
                    {
                        try
                        {
                            var z = part.OpenXmlPart.RootElement;
                        }
                        catch (Exception)
                        {
                            continue;
                        }
                    }
                    sDoc.Save();
                }
            }
            this.FileName = fileName;
            DocumentByteArray = ms.ToArray();
        }
    }

    public OpenXmlPowerToolsDocument(byte[] byteArray)
    {
        //DocumentByteArray = new byte[byteArray.Length];
        //Array.Copy(byteArray, DocumentByteArray, byteArray.Length);
        DocumentByteArray = byteArray.ToArray();
        this.FileName = null;
    }

    public OpenXmlPowerToolsDocument(byte[] byteArray, bool convertToTransitional)
    {
        if (convertToTransitional)
        {
            ConvertToTransitional(null, byteArray);
        }
        else
        {
            DocumentByteArray = new byte[byteArray.Length];
            Array.Copy(byteArray, DocumentByteArray, byteArray.Length);
            this.FileName = null;
        }
    }

    public OpenXmlPowerToolsDocument(string fileName, MemoryStream memStream)
    {
        FileName = fileName;
        DocumentByteArray = new byte[memStream.Length];
        Array.Copy(memStream.GetBuffer(), DocumentByteArray, memStream.Length);
    }

    public OpenXmlPowerToolsDocument(string fileName, MemoryStream memStream, bool convertToTransitional)
    {
        if (convertToTransitional)
        {
            ConvertToTransitional(fileName, memStream.ToArray());
        }
        else
        {
            FileName = fileName;
            DocumentByteArray = new byte[memStream.Length];
            Array.Copy(memStream.GetBuffer(), DocumentByteArray, memStream.Length);
        }
    }

    public string GetName()
    {
        if (FileName == null)
            return "Unnamed Document";
        FileInfo file = new FileInfo(FileName);
        return file.Name;
    }

    public void SaveAs(string fileName)
    {
        File.WriteAllBytes(fileName, DocumentByteArray);
    }

    public async Task SaveAsAsync(string fileName)
    {
        await File.WriteAllBytesAsync(fileName, DocumentByteArray);
    }

    public void Save()
    {
        if (this.FileName == null)
            throw new InvalidOperationException("Attempting to Save a document that has no file name.  Use SaveAs instead.");
        File.WriteAllBytes(this.FileName, DocumentByteArray);
    }

    public void WriteByteArray(Stream stream)
    {
        stream.Write(DocumentByteArray, 0, DocumentByteArray.Length);
    }

    public Type GetDocumentType()
    {
        return GetDocumentType(DocumentByteArray);
    }

    private static Type GetDocumentType(byte[] bytes)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            stream.Write(bytes, 0, bytes.Length);
            using (Package package = Package.Open(stream, FileMode.Open, FileAccess.Read))
            {
                PackageRelationship relationship = package.GetRelationshipsByType("http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument").FirstOrDefault();
                if (relationship == null)
                    relationship = package.GetRelationshipsByType("http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument").FirstOrDefault();
                if (relationship != null)
                {
                    PackagePart part = package.GetPart(PackUriHelper.ResolvePartUri(relationship.SourceUri, relationship.TargetUri));
                    switch (part.ContentType)
                    {
                        case "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml":
                        case "application/vnd.ms-word.document.macroEnabled.main+xml":
                        case "application/vnd.ms-word.template.macroEnabledTemplate.main+xml":
                        case "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml":
                            return typeof(WordprocessingDocument);
                        case "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml":
                        case "application/vnd.ms-excel.sheet.macroEnabled.main+xml":
                        case "application/vnd.ms-excel.template.macroEnabled.main+xml":
                        case "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml":
                            return typeof(SpreadsheetDocument);
                        case "application/vnd.openxmlformats-officedocument.presentationml.template.main+xml":
                        case "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml":
                        case "application/vnd.ms-powerpoint.template.macroEnabled.main+xml":
                        case "application/vnd.ms-powerpoint.addin.macroEnabled.main+xml":
                        case "application/vnd.openxmlformats-officedocument.presentationml.slideshow.main+xml":
                        case "application/vnd.ms-powerpoint.presentation.macroEnabled.main+xml":
                            return typeof(PresentationDocument);
                    }
                    return typeof(Package);
                }
                return null;
            }
        }
    }

    public static void SavePartAs(OpenXmlPart part, string filePath)
    {
        Stream partStream = part.GetStream(FileMode.Open, FileAccess.Read);
        byte[] partContent = new byte[partStream.Length];
        partStream.Read(partContent, 0, (int)partStream.Length);

        File.WriteAllBytes(filePath, partContent);
    }
}

public partial class WmlDocument : OpenXmlPowerToolsDocument
{
    public WmlDocument(OpenXmlPowerToolsDocument original)
        : base(original)
    {
        if (GetDocumentType() != typeof(WordprocessingDocument))
            throw new PowerToolsDocumentException("Not a Wordprocessing document.");
    }

    public WmlDocument(OpenXmlPowerToolsDocument original, bool convertToTransitional)
        : base(original, convertToTransitional)
    {
        if (GetDocumentType() != typeof(WordprocessingDocument))
            throw new PowerToolsDocumentException("Not a Wordprocessing document.");
    }

    public WmlDocument(string fileName)
        : base(fileName)
    {
        if (GetDocumentType() != typeof(WordprocessingDocument))
            throw new PowerToolsDocumentException("Not a Wordprocessing document.");
    }

    public WmlDocument(string fileName, bool convertToTransitional)
        : base(fileName, convertToTransitional)
    {
        if (GetDocumentType() != typeof(WordprocessingDocument))
            throw new PowerToolsDocumentException("Not a Wordprocessing document.");
    }

    public WmlDocument(string fileName, byte[] byteArray)
        : base(byteArray)
    {
        FileName = fileName;
        if (GetDocumentType() != typeof(WordprocessingDocument))
            throw new PowerToolsDocumentException("Not a Wordprocessing document.");
    }

    public WmlDocument(string fileName, byte[] byteArray, bool convertToTransitional)
        : base(byteArray, convertToTransitional)
    {
        FileName = fileName;
        if (GetDocumentType() != typeof(WordprocessingDocument))
            throw new PowerToolsDocumentException("Not a Wordprocessing document.");
    }

    public WmlDocument(string fileName, MemoryStream memStream)
        : base(fileName, memStream)
    {
    }

    public WmlDocument(string fileName, MemoryStream memStream, bool convertToTransitional)
        : base(fileName, memStream, convertToTransitional)
    {
    }
}

public partial class SmlDocument : OpenXmlPowerToolsDocument
{
    public SmlDocument(OpenXmlPowerToolsDocument original)
        : base(original)
    {
        if (GetDocumentType() != typeof(SpreadsheetDocument))
            throw new PowerToolsDocumentException("Not a Spreadsheet document.");
    }

    public SmlDocument(OpenXmlPowerToolsDocument original, bool convertToTransitional)
        : base(original, convertToTransitional)
    {
        if (GetDocumentType() != typeof(SpreadsheetDocument))
            throw new PowerToolsDocumentException("Not a Spreadsheet document.");
    }

    public SmlDocument(string fileName)
        : base(fileName)
    {
        if (GetDocumentType() != typeof(SpreadsheetDocument))
            throw new PowerToolsDocumentException("Not a Spreadsheet document.");
    }

    public SmlDocument(string fileName, bool convertToTransitional)
        : base(fileName, convertToTransitional)
    {
        if (GetDocumentType() != typeof(SpreadsheetDocument))
            throw new PowerToolsDocumentException("Not a Spreadsheet document.");
    }

    public SmlDocument(string fileName, byte[] byteArray)
        : base(byteArray)
    {
        FileName = fileName;
        if (GetDocumentType() != typeof(SpreadsheetDocument))
            throw new PowerToolsDocumentException("Not a Spreadsheet document.");
    }

    public SmlDocument(string fileName, byte[] byteArray, bool convertToTransitional)
        : base(byteArray, convertToTransitional)
    {
        FileName = fileName;
        if (GetDocumentType() != typeof(SpreadsheetDocument))
            throw new PowerToolsDocumentException("Not a Spreadsheet document.");
    }

    public SmlDocument(string fileName, MemoryStream memStream)
        : base(fileName, memStream)
    {
    }

    public SmlDocument(string fileName, MemoryStream memStream, bool convertToTransitional)
        : base(fileName, memStream, convertToTransitional)
    {
    }
}

public partial class PmlDocument : OpenXmlPowerToolsDocument
{
    public PmlDocument(OpenXmlPowerToolsDocument original)
        : base(original)
    {
        if (GetDocumentType() != typeof(PresentationDocument))
            throw new PowerToolsDocumentException("Not a Presentation document.");
    }

    public PmlDocument(OpenXmlPowerToolsDocument original, bool convertToTransitional)
        : base(original, convertToTransitional)
    {
        if (GetDocumentType() != typeof(PresentationDocument))
            throw new PowerToolsDocumentException("Not a Presentation document.");
    }

    public PmlDocument(string fileName)
        : base(fileName)
    {
        if (GetDocumentType() != typeof(PresentationDocument))
            throw new PowerToolsDocumentException("Not a Presentation document.");
    }

    public PmlDocument(string fileName, bool convertToTransitional)
        : base(fileName, convertToTransitional)
    {
        if (GetDocumentType() != typeof(PresentationDocument))
            throw new PowerToolsDocumentException("Not a Presentation document.");
    }

    public PmlDocument(string fileName, byte[] byteArray)
        : base(byteArray)
    {
        FileName = fileName;
        if (GetDocumentType() != typeof(PresentationDocument))
            throw new PowerToolsDocumentException("Not a Presentation document.");
    }

    public PmlDocument(string fileName, byte[] byteArray, bool convertToTransitional)
        : base(byteArray, convertToTransitional)
    {
        FileName = fileName;
        if (GetDocumentType() != typeof(PresentationDocument))
            throw new PowerToolsDocumentException("Not a Presentation document.");
    }

    public PmlDocument(string fileName, MemoryStream memStream)
        : base(fileName, memStream)
    {
    }

    public PmlDocument(string fileName, MemoryStream memStream, bool convertToTransitional)
        : base(fileName, memStream, convertToTransitional)
    {
    }
}

public class OpenXmlMemoryStreamDocument : IDisposable
{
    private static readonly FrozenSet<string> _wordprocessingContentTypes = FrozenSet.ToFrozenSet<string>(
    [
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml",
        "application/vnd.ms-word.document.macroEnabled.main+xml",
        "application/vnd.ms-word.template.macroEnabledTemplate.main+xml",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml"
    ]);

    private static readonly FrozenSet<string> _spreadsheetContentTypes = FrozenSet.ToFrozenSet<string>(
    [
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
        "application/vnd.ms-excel.sheet.macroEnabled.main+xml",
        "application/vnd.ms-excel.template.macroEnabled.main+xml",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.template.main+xml"
    ]);

    private static readonly FrozenSet<string> _presentationContentTypes = FrozenSet.ToFrozenSet<string>(
    [
        "application/vnd.openxmlformats-officedocument.presentationml.template.main+xml",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml",
        "application/vnd.ms-powerpoint.template.macroEnabled.main+xml",
        "application/vnd.ms-powerpoint.addin.macroEnabled.main+xml",
        "application/vnd.openxmlformats-officedocument.presentationml.slideshow.main+xml",
        "application/vnd.ms-powerpoint.presentation.macroEnabled.main+xml"
    ]);

    private static readonly string[] _officeDocumentRelationshipTypes =
    [
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument",
        "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument"
    ];

    private readonly OpenXmlPowerToolsDocument? _document;
    private MemoryStream? _docMemoryStream;
    private Package? _docPackage;
    private bool _disposed;

    public OpenXmlMemoryStreamDocument(OpenXmlPowerToolsDocument doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        _document = doc;
        _docMemoryStream = new MemoryStream(doc.DocumentByteArray);
        _docPackage = OpenPackageSafe(_docMemoryStream);
    }

    internal OpenXmlMemoryStreamDocument(MemoryStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _docMemoryStream = stream;
        _docPackage = OpenPackageSafe(_docMemoryStream);
    }

    public static OpenXmlMemoryStreamDocument CreateWordprocessingDocument()
    {
        var stream = new MemoryStream();
        using var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

        doc.AddMainDocumentPart();
        doc.MainDocumentPart!.PutXDocument(new XDocument(
            new XElement(W.document,
                new XAttribute(XNamespace.Xmlns + "w", W.w),
                new XAttribute(XNamespace.Xmlns + "r", R.r),
                new XElement(W.body))));

        return new OpenXmlMemoryStreamDocument(stream);
    }

    public static OpenXmlMemoryStreamDocument CreateSpreadsheetDocument()
    {
        var stream = new MemoryStream();
        using var doc = SpreadsheetDocument.Create(stream, DocumentFormat.OpenXml.SpreadsheetDocumentType.Workbook);

        doc.AddWorkbookPart();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        doc.WorkbookPart!.PutXDocument(new XDocument(
            new XElement(ns + "workbook",
                new XAttribute("xmlns", ns),
                new XAttribute(XNamespace.Xmlns + "r", relationshipsNs),
                new XElement(ns + "sheets"))));

        return new OpenXmlMemoryStreamDocument(stream);
    }

    public static OpenXmlMemoryStreamDocument CreatePresentationDocument()
    {
        var stream = new MemoryStream();
        using var doc = PresentationDocument.Create(stream, DocumentFormat.OpenXml.PresentationDocumentType.Presentation);

        doc.AddPresentationPart();
        XNamespace ns = "http://schemas.openxmlformats.org/presentationml/2006/main";
        XNamespace relationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace drawingNs = "http://schemas.openxmlformats.org/drawingml/2006/main";

        doc.PresentationPart!.PutXDocument(new XDocument(
            new XElement(ns + "presentation",
                new XAttribute(XNamespace.Xmlns + "a", drawingNs),
                new XAttribute(XNamespace.Xmlns + "r", relationshipsNs),
                new XAttribute(XNamespace.Xmlns + "p", ns),
                new XElement(ns + "sldMasterIdLst"),
                new XElement(ns + "sldIdLst"),
                new XElement(ns + "notesSz",
                    new XAttribute("cx", "6858000"),
                    new XAttribute("cy", "9144000")))));

        return new OpenXmlMemoryStreamDocument(stream);
    }

    public static OpenXmlMemoryStreamDocument CreatePackage()
    {
        var stream = new MemoryStream();
        using var package = Package.Open(stream, FileMode.Create);
        return new OpenXmlMemoryStreamDocument(stream);
    }

    public Package GetPackage()
    {
        ThrowIfDisposed();
        return _docPackage!;
    }

    public WordprocessingDocument GetWordprocessingDocument() =>
        GetTypedDocument<WordprocessingDocument>(
            () => WordprocessingDocument.Open(_docMemoryStream!, true),
            "Wordprocessing");

    public SpreadsheetDocument GetSpreadsheetDocument() =>
        GetTypedDocument<SpreadsheetDocument>(
            () => SpreadsheetDocument.Open(_docMemoryStream!, true),
            "Spreadsheet");

    public PresentationDocument GetPresentationDocument() =>
        GetTypedDocument<PresentationDocument>(
            () => PresentationDocument.Open(_docMemoryStream!, true),
            "Presentation");

    private TDocument GetTypedDocument<TDocument>(Func<TDocument> opener, string documentTypeName)
        where TDocument : OpenXmlPackage
    {
        ThrowIfDisposed();

        if (GetDocumentType() != typeof(TDocument))
            throw new PowerToolsDocumentException($"Not a {documentTypeName} document.");

        try
        {
            return opener();
        }
        catch (Exception e)
        {
            throw new PowerToolsDocumentException($"Failed to open {documentTypeName} document.", e);
        }
    }

    public Type GetDocumentType()
    {
        ThrowIfDisposed();

        var relationship = _officeDocumentRelationshipTypes
            .Select(type => _docPackage!.GetRelationshipsByType(type).FirstOrDefault())
            .FirstOrDefault(r => r is not null)
            ?? throw new PowerToolsDocumentException("Not an Open XML Document.");

        var partUri = PackUriHelper.ResolvePartUri(relationship.SourceUri, relationship.TargetUri);
        var part = _docPackage!.GetPart(partUri);

        return part.ContentType switch
        {
            var ct when _wordprocessingContentTypes.Contains(ct) => typeof(WordprocessingDocument),
            var ct when _spreadsheetContentTypes.Contains(ct) => typeof(SpreadsheetDocument),
            var ct when _presentationContentTypes.Contains(ct) => typeof(PresentationDocument),
            var ct => throw new PowerToolsDocumentException($"Unknown content type: {ct}")
        };
    }

    public OpenXmlPowerToolsDocument GetModifiedDocument()
    {
        ThrowIfDisposed();
        FlushPackage();
        return new OpenXmlPowerToolsDocument(_document?.FileName, _docMemoryStream!);
    }

    public WmlDocument GetModifiedWmlDocument()
    {
        ThrowIfDisposed();
        FlushPackage();
        return new WmlDocument(_document?.FileName, _docMemoryStream!);
    }

    public SmlDocument GetModifiedSmlDocument()
    {
        ThrowIfDisposed();
        FlushPackage();
        return new SmlDocument(_document?.FileName, _docMemoryStream!);
    }

    public PmlDocument GetModifiedPmlDocument()
    {
        ThrowIfDisposed();
        FlushPackage();
        return new PmlDocument(_document?.FileName, _docMemoryStream!);
    }

    private void FlushPackage()
    {
        _docPackage?.Flush();
        _docMemoryStream!.Position = 0;
    }

    public void Close() => Dispose();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~OpenXmlMemoryStreamDocument() => Dispose(false);

    private void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Note: In .NET 10, there may be ZipArchive issues when closing
            // Package after modifications. Consider if this needs special handling.
            try
            {
                _docPackage?.Close();
            }
            catch (EndOfStreamException)
            {
                // Known issue in .NET 10 with ZipArchive
            }

            _docMemoryStream?.Dispose();
        }

        _docPackage = null;
        _docMemoryStream = null;
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private static Package OpenPackageSafe(MemoryStream stream)
    {
        try
        {
            return Package.Open(stream, FileMode.Open);
        }
        catch (Exception e)
        {
            throw new PowerToolsDocumentException("Failed to open document package.", e);
        }
    }
}

/*
public class OpenXmlPowerToolsDocument : IDisposable
{
    public string? FileName { get; set; }
    
    // Make this private - force callers to use Memory/Span
    private byte[]? DocumentByteArray { get; set; }
    
    // New: Expose as Memory<byte> for zero-copy access
    public Memory<byte> DocumentMemory => DocumentByteArray?.AsMemory(0, DocumentByteArray.Length) ?? Memory<byte>.Empty;

    // Threshold for LOH avoidance
    private const int PooledThreshold = 85_000;
    private bool _isPooled;

    // Factory method for loop processing - eliminates double-copy
    public static OpenXmlPowerToolsDocument FromFileForProcessing(string fileName)
    {
        var fileInfo = new FileInfo(fileName);
        int fileSize = (int)fileInfo.Length;
        
        byte[] buffer;
        bool isPooled = false;
        
        // Use pooling for large files to avoid LOH
        if (fileSize >= PooledThreshold)
        {
            buffer = ArrayPool<byte>.Shared.Rent(fileSize);
            isPooled = true;
        }
        else
        {
            buffer = new byte[fileSize];
        }
        
        // Read directly into buffer (single copy only)
        using (var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            fs.ReadExactly(buffer, 0, fileSize);
        }
        
        // Create instance and assign buffer directly
        var doc = new OpenXmlPowerToolsDocument(buffer, isPooled)
        {
            FileName = fileName
        };
        
        // Validate document type
        if (doc.GetDocumentType() == null)
        {
            if (isPooled) ArrayPool<byte>.Shared.Return(buffer);
            throw new PowerToolsDocumentException("Not an Open XML document.");
        }
        
        return doc;
    }

    // Private constructor that accepts pooled arrays
    private OpenXmlPowerToolsDocument(byte[] buffer, bool isPooled)
    {
        DocumentByteArray = buffer;
        _isPooled = isPooled;
    }

    // Existing constructors - keep for backward compatibility
    public OpenXmlPowerToolsDocument(OpenXmlPowerToolsDocument? original)
    {
        ArgumentNullException.ThrowIfNull(original);
        DocumentByteArray = original.DocumentByteArray?.ToArray();
        FileName = original.FileName;
    }

    public OpenXmlPowerToolsDocument(byte[] byteArray)
    {
        // For small arrays: copy is acceptable
        // For large arrays: caller should use FromFileForProcessing
        DocumentByteArray = byteArray.Length < PooledThreshold 
            ? byteArray.ToArray() 
            : RentAndCopy(byteArray);
    }

    private byte[] RentAndCopy(byte[] source)
    {
        var pooled = ArrayPool<byte>.Shared.Rent(source.Length);
        Array.Copy(source, pooled, source.Length);
        _isPooled = true;
        return pooled;
    }

    // CRITICAL: Add disposal to return pooled arrays
    public void Dispose()
    {
        if (_isPooled && DocumentByteArray != null)
        {
            ArrayPool<byte>.Shared.Return(DocumentByteArray);
            DocumentByteArray = null;
        }
    }

    // Keep all other methods unchanged...
    public void SaveAs(string fileName)
    {
        File.WriteAllBytes(fileName, DocumentByteArray);
    }

    // Modify OpenXmlMemoryStreamDocument to use MemoryStream wrapper
    public OpenXmlMemoryStreamDocument CreateStreamDocument()
    {
        // Wrap existing buffer in MemoryStream without copy
        var stream = new MemoryStream(DocumentByteArray, 0, DocumentByteArray.Length, true, true);
        return new OpenXmlMemoryStreamDocument(stream);
    }
}
 */
