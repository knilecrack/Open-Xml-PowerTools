# OpenXML PowerTools Agent Guide

This guide provides instructions for building, testing, and contributing to the OpenXML PowerTools repository.

## Project Structure

This is a .NET project written in C#. The main logic is in the `OpenXmlPowerTools` directory. Tests are located in the `OpenXmlPowerTools.Tests` directory.

## Build and Test

### Build

To build the solution, run the following command from the root directory:

```sh
dotnet build OpenXmlPowerTools.sln
```

This will build all projects in the solution.

### Test

Tests are written using XUnit. To run all tests, use the following command:

```sh
dotnet test OpenXmlPowerTools.sln
```

To run a single test, you can use the `--filter` option. For example, to run a test named `MyTestName`, use:

```sh
dotnet test --filter "Name=MyTestName"
```

## Code Style

### Formatting

- **Indentation**: 4 spaces.
- **Braces**: Opening braces are placed on the same line for properties and on a new line for classes, methods, and control structures.
- **`using` directives**: `System` imports should be placed before other imports.

### Naming Conventions

- **Classes and Methods**: Use `PascalCase`.
- **Variables**: Use `camelCase`.
- **Interfaces**: Use `IPascalCase`.
- **Private Fields**: Use `_camelCase`.
- **Constants**: Use `PascalCase`.

### Types

- Use explicit types (`string`, `int`) instead of `var` when the type is not obvious from the right-hand side of the assignment.
- Use LINQ for data manipulation where it improves readability.

### Error Handling

- Throw specific exceptions where possible (e.g., `ArgumentException`, `InvalidOperationException`).
- Avoid catching generic `Exception`.

### Imports

- Keep `using` statements at the top of the file.
- `System.*` namespaces should come first.
- Separate different sets of namespaces with a blank line. For example:

```csharp
using System;
using System.Collections.Generic;
using System.IO;

using DocumentFormat.OpenXml.Packaging;

using OpenXmlPowerTools;
```

### General Style

- Follow the existing code style.
- Use XML documentation comments for public members.
- The project uses `stylecop.json` and `.ruleset` files to enforce code style. Ensure your changes comply with these rules. It is recommended to use an IDE that supports these files, like Visual Studio with the StyleCop extension.

### Example

Here is an example of the preferred coding style:

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
        /// <param name="wDoc">The WordprocessingDocument.</param>
        /// <returns>A boolean indicating success.</returns>
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
                // Log exception
                throw new InvalidOperationException("An error occurred", e);
            }
        }
    }
}
```
