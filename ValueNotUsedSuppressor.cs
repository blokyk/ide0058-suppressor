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

    private int _currNamesListHash = 0;

    public override void ReportSuppressions(SuppressionAnalysisContext context) {
        foreach (var diag in context.ReportedDiagnostics) {
            var tree = diag.Location.SourceTree!;
            var model = context.GetSemanticModel(tree);

            // computing the list of exceptions is mildly expensive, but thankfully most
            // of nodes will probably share the same editorconfig so we can just cache it,
            // but we still need to check (and update if necessary) just to be correct
            UpdateExceptionListFrom(context.Options.AnalyzerConfigOptionsProvider.GetOptions(tree));

            if (ShouldSuppress(diag, tree, model))
                context.ReportSuppression(Suppression.Create(_suppressIDE0058Descriptor, diag));
        }
    }

    bool ShouldSuppress(Diagnostic diag, SyntaxTree tree, SemanticModel model) {
        // we want to suppress symbols that, are fluent interfaces, i.e. that are:
        //      1. are methods
        //      2. comes from a fluent type
        //      3. returns a fluent type

        var node = tree.GetRoot().FindNode(diag.Location.SourceSpan, getInnermostNodeForTie: true);
        var nodeSymbolInfo = model.GetSymbolInfo(node).Symbol;

        // if the node isn't typed, just give up
        if (nodeSymbolInfo is null)
            return false;

        // if it's not a method we don't care
        if (nodeSymbolInfo is not IMethodSymbol methodInfo)
            return false;

        var containingTypeName = GetFullMetadataName(methodInfo.ContainingType);

        return _exceptionsMetadataNames.Contains(containingTypeName);

    }

    private HashSet<string> _exceptionsMetadataNames = _builtinExceptions;
    private void SetNewExceptionList(string[] names) {
        _exceptionsMetadataNames = [];

        foreach (var name in names) {
            var trimmedName = name.Trim();

            if (!string.IsNullOrEmpty(trimmedName))
                _exceptionsMetadataNames.Add(name.Trim());
        }
    }

    void UpdateExceptionListFrom(AnalyzerConfigOptions opts) {
        if (opts.TryGetValue("dotnet_fluent_types", out var names)) {
            if (names.GetHashCode() != _currNamesListHash) {
                _currNamesListHash = names.GetHashCode();
                SetNewExceptionList(names.Split(','));
            }
        } else {
            _exceptionsMetadataNames = _builtinExceptions;
        }
    }

    // from https://stackoverflow.com/questions/27105909/get-fully-qualified-metadata-name-in-roslyn
    internal static string GetFullMetadataName(ISymbol s) {
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