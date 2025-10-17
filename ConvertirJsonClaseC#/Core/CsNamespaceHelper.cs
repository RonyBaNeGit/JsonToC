using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ConvertirJsonClaseC_.Core
{
    public static class CsNamespaceHelper
    {
        public static string? ExtractNamespace(string csharpSource)
        {
            if (string.IsNullOrWhiteSpace(csharpSource)) return null;

            var tree = CSharpSyntaxTree.ParseText(csharpSource);
            var root = tree.GetCompilationUnitRoot();

            var ns = root.Members.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.NamespaceDeclarationSyntax>()
                                 .FirstOrDefault();
            if (ns != null)
                return ns.Name.ToString().Trim();

            // file-scoped namespace: `namespace X.Y;`
            var fns = root.Members.OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FileScopedNamespaceDeclarationSyntax>()
                                  .FirstOrDefault();
            if (fns != null)
                return fns.Name.ToString().Trim();

            return null;
        }
    }
}
