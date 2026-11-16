# Dev notes

## Testing

Setting up tests for diagnostic suppressors is... not easy :|

It seems you're _meant_ to use similar techniques to normal analyzers (after all,
`DiagnosticSuppressor` is a child of `DiagnosticAnalyzer`), i.e. using
`Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest` and the like, but the
[one](https://github.com/AndanteTribe/UnityNamespaceSuppressor/tree/main/src/UnityNamespaceSuppressor.Tests)
example of suppressor tests I found seemingly reimplements most of the machinery
itself, so I don't know what you're actually supposed to do.

I am tired, so I just put my tiny little try at it in the `unit-tests` branch.

## Testing (badly, this time)

If you just want to do a quick and dirty test (which, importantly, will _not_
allow you to run with a debugger), you can use `dotnet new console` in a
temporary dir, and then add the following stuff to the `.csproj`:

```xml
<PropertyGroup>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>

<ItemGroup>
  <ProjectReference
    OutputItemType="Analyzer"
    ReferenceOutputAssembly="false"
    Include="/home/blokyk/dev/lab/ide0058-suppressor/ValueNotUsedSuppressor.csproj"
  />
</ItemGroup>
```

And then add the following `.editorconfig`:

```
root = true

# C# files
[*.cs]

dotnet_fluent_types = System.Text.StringBuilder, C

csharp_style_unused_value_assignment_preference = discard_variable:warning
csharp_style_unused_value_expression_statement_preference = discard_variable:warning
dotnet_diagnostics.IDE0058.severity = warning
```
