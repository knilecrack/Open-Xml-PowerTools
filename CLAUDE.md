# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Open-XML-PowerTools is a C# library built on top of the [Microsoft Open XML SDK](https://learn.microsoft.com/en-us/office/open-xml/open-xml-sdk). While the Open XML SDK provides low-level access to Office document structure (DOCX, XLSX, PPTX), this library adds high-level operations and utilities that would otherwise require significant custom code:

- Document merging and splitting
- Document comparison with revision tracking
- High-fidelity HTML ↔ DOCX conversion
- Template-based document assembly
- Content search and replacement
- Metrics extraction and analysis

**Key relationship**: This library wraps and extends the Open XML SDK (DocumentFormat.OpenXml package) to provide developer-friendly APIs for common document manipulation scenarios. Understanding the [Open XML SDK fundamentals](https://learn.microsoft.com/en-us/office/open-xml/open-xml-sdk) is helpful but not required for using most PowerTools features.

## Build and Test Commands

### Build
```bash
dotnet build OpenXmlPowerTools.sln
```

### Run All Tests
```bash
dotnet test OpenXmlPowerTools.sln
```

### Run Specific Test
Use the `--filter` option to run a single test:
```bash
dotnet test --filter "Name=TestMethodName"
```

### Build Configuration
- **Release builds** treat warnings as errors (configured in `Directory.Build.props`)
- StyleCop analyzers are enabled via `Directory.Build.targets` and configured through `stylecop.json` and `rules.ruleset`
- Target framework: .NET 10.0 (net10.0)
- Suppressed warnings: CA1416, CA2022, CA2200
- **Nullable reference types**: Enabled project-wide (`<Nullable>enable</Nullable>`)
  - All reference types must be explicitly marked as nullable (`?`) if they can be null
  - Use `#nullable disable` at file-level for legacy code with many nullable warnings (e.g., WmlComparer.cs has 900+ warnings)
  - For new code, follow nullable best practices: mark nullable parameters/fields with `?`, use null checks, avoid null-forgiving operator (`!`) unless certain

## Code Architecture

### Core Document Abstraction Layer

The library uses an in-memory document abstraction pattern centered around three base classes:
- `OpenXmlPowerToolsDocument` - Base class for all document types
- `WmlDocument` - Word documents (extends OpenXmlPowerToolsDocument)
- `SmlDocument` - Excel documents (extends OpenXmlPowerToolsDocument)
- `PmlDocument` - PowerPoint documents (extends OpenXmlPowerToolsDocument)

These classes store documents as byte arrays (`DocumentByteArray`) and provide a consistent interface for manipulation without requiring file I/O.

### Document Manipulation Pattern

The codebase follows a consistent pattern that wraps the Open XML SDK's package-based model:

1. **Modifying a document**: Load byte array → open with SDK's `WordprocessingDocument`/`SpreadsheetDocument`/`PresentationDocument` → manipulate using SDK + PowerTools utilities → return modified byte array
2. **Read-only operations**: Load byte array → open with SDK → read data using SDK/PowerTools → return results
3. **Creating new documents**: Create in-memory using SDK → populate content with PowerTools helpers → return byte array

**Important**: PowerTools provides the in-memory byte array abstraction layer and high-level operations, while internally using the SDK's `*Document` classes (from `DocumentFormat.OpenXml.Packaging`) for actual XML manipulation.

See examples in the comments at the top of `PtOpenXmlDocument.cs` (lines 5-46).

### Major Functional Modules

Each major module is implemented as a static class with public methods:

- **DocumentBuilder** (`DocumentBuilder.cs`) - Merges multiple DOCX files, splits documents on sections. Uses `Source` class to define source documents with options for section handling and page ranges.

- **WmlComparer** (`WmlComparer.cs`) - Compares two DOCX files and produces revision-tracked output. Core algorithm uses content atom representation and correlation algorithms. Configurable via `WmlComparerSettings`.

- **WmlToHtmlConverter** (`WmlToHtmlConverter.cs`) - High-fidelity DOCX to HTML conversion with CSS styling.

- **HtmlToWmlConverter** (`HtmlToWmlConverter.cs`, `HtmlToWmlConverterCore.cs`, `HtmlToWmlCssParser.cs`, `HtmlToWmlCssApplier.cs`) - Reverse conversion from HTML/CSS to DOCX. Split across multiple files for parsing, CSS application, and core conversion logic.

- **PresentationBuilder** (`PresentationBuilder.cs`) - Merges and manipulates PowerPoint presentations, analogous to DocumentBuilder for PPTX.

- **DocumentAssembler** (`DocumentAssembler.cs`) - Template-based document generation using XML data sources.

- **SpreadsheetWriter** (`SpreadsheetWriter.cs`) - Simplified API for generating XLSX files, including streaming support for large datasets.

- **MetricsGetter** (`MetricsGetter.cs`) - Extracts metadata from DOCX files (styles hierarchy, languages, fonts, revision tracking status).

- **RevisionProcessor** / **RevisionAccepter** - Handles tracked revisions in Word documents.

- **TextReplacer** (`TextReplacer.cs`) - Regex-based search and replace in documents.

- **FormattingAssembler** (`FormattingAssembler.cs`) - Processes and applies formatting across documents.

### Utility Infrastructure

- **PtOpenXmlUtil** (`PtOpenXmlUtil.cs`) - Core utilities for Open XML manipulation (323KB file, extensive helper methods)
- **PtUtil** (`PtUtil.cs`) - General utility functions including file handling
- **OxPtHelpers** (`OxPtHelpers.cs`) - Helper extension methods for Open XML objects
- **ListItemRetriever** (`ListItemRetriever.cs`) - Handles numbered/bulleted list processing with localization support (see `GetListItemText_*.cs` files for various languages)

### Test Organization

Tests are organized by module in `OpenXmlPowerTools.Tests/`:
- Each major module has a corresponding `*Tests.cs` file
- Tests use XUnit framework
- `TestsBase.cs` provides common test infrastructure
- Test files located in `TestFiles/` directory

## Code Style Guidelines

### Formatting (from AGENTS.md and stylecop.json)

- **Indentation**: 4 spaces (no tabs)
- **Using directives**:
  - Place outside namespace
  - System namespaces first, then blank line, then other namespaces
  - Blank lines required between using groups
- **Naming**:
  - Classes/Methods: `PascalCase`
  - Private fields: `_camelCase`
  - Variables: `camelCase`
  - Interfaces: `IPascalCase`
- **File endings**: Newline required at end of file

### Example from AGENTS.md (lines 87-128)

```csharp
using System;
using System.Collections.Generic;

using DocumentFormat.OpenXml.Packaging;

namespace OpenXmlPowerTools
{
    public class ExampleClass
    {
        private readonly string _exampleField;

        public ExampleClass(string exampleField)
        {
            _exampleField = exampleField;
        }

        /// <summary>
        /// An example method.
        /// </summary>
        public bool DoSomething(WordprocessingDocument wDoc)
        {
            if (wDoc == null)
            {
                throw new ArgumentNullException(nameof(wDoc));
            }

            try
            {
                // Method implementation
                return true;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("An error occurred", e);
            }
        }
    }
}
```

### Error Handling

- Throw specific exceptions (`ArgumentException`, `ArgumentNullException`, `InvalidOperationException`)
- Avoid catching generic `Exception`
- Custom exceptions: `PowerToolsDocumentException`, `PowerToolsInvalidDataException`

### Type Usage

- Prefer explicit types over `var` when type is not obvious
- Use LINQ for data manipulation where it improves readability

## Key Dependencies

- **DocumentFormat.OpenXml** (3.4.1) - The Microsoft Open XML SDK. This is the foundational library that PowerTools is built upon. All document manipulation ultimately uses SDK classes like `WordprocessingDocument`, `SpreadsheetDocument`, `PresentationDocument`, and the various element types.
- **System.IO.Packaging** (10.0.2) - Low-level package manipulation (used by Open XML SDK)
- **System.IO.Hashing** (10.0.2) - Hashing algorithms for content comparison
- **System.Drawing.Common** (10.0.0) - Graphics/color operations (Windows-specific APIs)
- **xunit** (2.9.2) - Testing framework

Note: Some APIs (System.Drawing.Common) are Windows-specific, which is why CA1416 warnings are suppressed.

**Package Info**: Custom build published as `FHPowerTools` package (version 1.0.2) - a fork of the original OpenXmlDev/Open-Xml-PowerTools.

## Important Implementation Details

### Conditional Compilation
Several modules use preprocessor directives:
- `DocumentBuilder.cs` defines `TestForUnsupportedDocuments` and `MergeStylesWithSameNames` (lines 4-5)

### Debugging Support
`WmlComparer.cs` includes debugging infrastructure:
- `s_SaveIntermediateFilesForDebugging` flag
- `WmlComparerSettings.DebugTempFileDi` for saving intermediate files during comparison

### Localization
List item text retrieval supports multiple languages via separate `GetListItemText_*.cs` files:
- Default, French (fr_FR), Russian (ru_RU), Swedish (sv_SE), Turkish (tr_TR), Chinese (zh_CN)

## Working with Nullable Reference Types

The project has nullable reference types enabled. When fixing nullable warnings:

1. **For files with many warnings** (like `WmlComparer.cs` with 900+ warnings): Add `#nullable disable` at the top of the file as a pragmatic solution for legacy code.

2. **For new code or targeted fixes**:
   - Change return types to nullable when methods can return null: `object` → `object?`
   - Make parameters nullable if they accept null: `string param` → `string? param`
   - Make fields/properties nullable: `XElement field` → `XElement? field`
   - Add null checks before dereferencing: `element.Value` → `element?.Value` or add explicit `if (element != null)`
   - Use null-forgiving operator (`!`) sparingly, only when you're certain the value is non-null but the compiler can't infer it

3. **Common patterns**:
   - `CS8603` (Possible null reference return): Make return type nullable
   - `CS8600` (Converting null to non-nullable): Make target variable/parameter nullable
   - `CS8625` (Cannot convert null literal): Make the parameter/field type nullable
   - `CS8602` (Dereference of possibly null): Add null check or use `?.` operator
   - `CS8618` (Non-nullable field not initialized): Add `= null!;` or make nullable or initialize in constructor
