using System.Diagnostics;
using System.Collections.Immutable;

namespace Blokyk.Roslyn;

[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.FSharp, LanguageNames.VisualBasic)]
public class ValueNotUsedSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor _suppressIDE0058Descriptor
        = new(
            "BLK1001",
            "IDE0058",
            "This type has a fluent interface and thus the return value can be safely ignored"
        );

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } = ImmutableArray.Create(_suppressIDE0058Descriptor);

    private static readonly HashSet<string> _builtinExceptions = new() {
        "System.Text.StringBuilder"
    };

    private HashSet<string> _exceptionsMetadataNames = _builtinExceptions;
    private void SetNewExceptionList(string[] names) {
        _exceptionsMetadataNames = new();

        foreach (var name in names) {
            var trimmedName = name.Trim();

            if (!string.IsNullOrEmpty(trimmedName))
                _exceptionsMetadataNames.Add(name.Trim());
        }
    }

    private int _currNamesListHash = 0;

    private const string _supLogPath = "/home/blokyk/csharp/suppressor.log";

    [Conditional("LOG")]
    private static void Log(string s) => File.AppendAllText(_supLogPath, DateTime.Now + " > " + s + "\n");

    public override void ReportSuppressions(SuppressionAnalysisContext context) {
        Log("ReportSuppressions() called...");

        foreach (var diag in context.ReportedDiagnostics) {
            var tree = diag.Location.SourceTree!;

            var opts = context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree);

            if (opts.TryGetValue("dotnet_fluent_types", out var names)) {
                Log("dotnet_fluent_types = " + names);
                if (names.GetHashCode() != _currNamesListHash) {
                    _currNamesListHash = names.GetHashCode();
                    SetNewExceptionList(names.Split(','));
                }
            } else {
                _exceptionsMetadataNames = _builtinExceptions;
                Log("dotnet_fluent_types doesn't exist!");
                Log(string.Join(", ", opts.Keys));
            }

            Log("fluent_types = [" + string.Join(", ", _exceptionsMetadataNames) + "]");

            var node = tree.GetRoot().FindNode(diag.Location.SourceSpan, getInnermostNodeForTie: true);
            var model = context.GetSemanticModel(tree);

            var nodeSymbolInfo = model.GetSymbolInfo(node).Symbol;

            if (nodeSymbolInfo is null)
                continue;

            if (nodeSymbolInfo is not IMethodSymbol methodInfo)
                continue;

            var containingTypeName = GetFullMetadataName(methodInfo.ContainingType);

            Log("method's containing type is named '" + containingTypeName + "'");

            if (!_exceptionsMetadataNames.Contains(containingTypeName))
                continue;

            Log("type '" + containingTypeName + "' is fluent, suppressing diagnostic...");

            context.ReportSuppression(Suppression.Create(_suppressIDE0058Descriptor, diag));
        }
    }

    // from https://stackoverflow.com/questions/27105909/get-fully-qualified-metadata-name-in-roslyn
    public static string GetFullMetadataName(ISymbol s) {
        if (IsRootNamespace(s)) {
            return string.Empty;
        }

        var parts = new Stack<string>();

        parts.Push(s.MetadataName);

        var last = s;
        s = s.ContainingSymbol;

        while (!IsRootNamespace(s)) {
            if (s is ITypeSymbol && last is ITypeSymbol) {
                parts.Push("+");
            } else {
                parts.Push(".");
            }

            parts.Push(s.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            s = s.ContainingSymbol;
        }

        return string.Concat(parts);
    }

    private static bool IsRootNamespace(ISymbol symbol) => symbol is INamespaceSymbol { IsGlobalNamespace: true };
}