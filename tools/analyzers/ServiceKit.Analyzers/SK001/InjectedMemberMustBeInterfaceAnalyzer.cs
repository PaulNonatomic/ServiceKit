using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ServiceKit.Analyzers
{
	[DiagnosticAnalyzer(LanguageNames.CSharp)]
	public sealed class InjectedMemberMustBeInterfaceAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "SK001";

		private static readonly DiagnosticDescriptor Rule = new(
			id: DiagnosticId,
			title: "Injected member should be an interface",
			messageFormat: "Member '{0}' with [InjectService] should be an interface type",
			category: "ServiceKit.Usage",
			defaultSeverity: DiagnosticSeverity.Warning,
			isEnabledByDefault: true,
			description: "ServiceKit recommends injecting interfaces to keep services loosely coupled and AOT-friendly."
		);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

		public override void Initialize(AnalysisContext context)
		{
			context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
			context.EnableConcurrentExecution();
			context.RegisterSymbolAction(AnalyzeField, SymbolKind.Field);
			context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
		}

		private static void AnalyzeField(SymbolAnalysisContext ctx)
		{
			var field = (IFieldSymbol)ctx.Symbol;
			if (!HasInjectService(field)) return;

			if (field.Type?.TypeKind != TypeKind.Interface)
			{
				ctx.ReportDiagnostic(Diagnostic.Create(Rule, field.Locations[0], field.Name));
			}
		}

		private static void AnalyzeProperty(SymbolAnalysisContext ctx)
		{
			var prop = (IPropertySymbol)ctx.Symbol;
			if (!HasInjectService(prop)) return;

			if (prop.Type?.TypeKind != TypeKind.Interface)
			{
				ctx.ReportDiagnostic(Diagnostic.Create(Rule, prop.Locations[0], prop.Name));
			}
		}

		private static bool HasInjectService(ISymbol symbol) =>
			symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectServiceAttribute");
	}
}
