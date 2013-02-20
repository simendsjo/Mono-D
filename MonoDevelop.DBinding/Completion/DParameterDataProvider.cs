using System.Collections.Generic;
using System.Text;
using D_Parser.Completion;
using D_Parser.Dom;
using D_Parser.Dom.Expressions;
using D_Parser.Resolver;
using ICSharpCode.NRefactory.Completion;
using MonoDevelop.D.Resolver;
using MonoDevelop.Ide.CodeCompletion;
using MonoDevelop.Ide.Gui;

namespace MonoDevelop.D.Completion
{
	public class DParameterDataProvider : ParameterDataProvider
	{
		Document doc;
		ArgumentsResolutionResult args;
		int selIndex = 0;

		public AbstractType CurrentResult { get {
			return args.ResolvedTypesOrMethods[selIndex];
		} }
		
		public IEnumerable<ISyntaxRegion> GetParameters()
		{
			if (CurrentResult is DSymbol)
			{
				var tir = (DSymbol)CurrentResult;

				if (tir.Definition is DClassLike && ((DClassLike)tir.Definition).TemplateParameters!=null)
					return ((DClassLike)tir.Definition).TemplateParameters;

				var dm = tir.Definition as DMethod;
				if (dm != null)
				{
					if (args.IsTemplateInstanceArguments)
						return dm.TemplateParameters;
					return dm.Parameters;
				}
			}
			else if (CurrentResult is DelegateType)
			{
				var dr = (DelegateType)CurrentResult;

				if (dr.IsFunctionLiteral)
					return ((FunctionLiteral)dr.DeclarationOrExpressionBase).AnonymousMethod.Parameters;
				else
					return ((DelegateDeclaration)dr.DeclarationOrExpressionBase).Parameters;
			}

			return null;
		}

		/// <summary>
		/// Might be either an INode or a ITemplateParameter.
		/// </summary>
		public ISyntaxRegion GetParameterObj(int paramIndex)
		{
			if (paramIndex < 0)
				return null;

			var parameters = GetParameters();
			
			if(parameters == null)
				return null;
			
			if(parameters is ITemplateParameter[])
				return (parameters as ITemplateParameter[])[paramIndex];
			else if(parameters is List<INode>)
				return (parameters as List<INode>)[paramIndex];
			return null;
		}

		public static DParameterDataProvider Create (Document doc, IAbstractSyntaxTree SyntaxTree, CodeCompletionContext ctx)
		{
			var caretLocation = new CodeLocation (ctx.TriggerLineOffset, ctx.TriggerLine);

			try {
				var edData = DResolverWrapper.GetEditorData(doc);

				edData.CaretLocation=caretLocation;
				edData.CaretOffset=ctx.TriggerOffset;

				var argsResult = ParameterInsightResolution.ResolveArgumentContext (edData);
				
				if (argsResult == null || argsResult.ResolvedTypesOrMethods == null || argsResult.ResolvedTypesOrMethods.Length < 1)
					return null;

				return new DParameterDataProvider(doc, argsResult, ctx.TriggerOffset);
			} catch {
				return null;
			}
		}
		
		private DParameterDataProvider (Document doc, ArgumentsResolutionResult argsResult, int startOffset) : base(startOffset)
		{
			this.doc = doc;
			args = argsResult;
			selIndex = args.CurrentlyCalledMethod;
		}
		
		public static string GetNodeParamString (D_Parser.Dom.INode node)
		{	
			string result = "";
			string sep = "";
			if (node is DMethod) {
				
				foreach (D_Parser.Dom.INode param in (node as DMethod).Parameters) {
					if (param.Type != null)
						result = result + sep + param.Type.ToString ();	
					sep = ", ";
				}
				if (result.Length != 0)
					result = "(" + result + ")"; 
			}
			return result;
		}			
	



		#region IParameterDataProvider implementation
		public int GetCurrentParameterIndex(CodeLocation where)
		{
			/*
			if(args.ParsedExpression is PostfixExpression_MethodCall)
			{
				var mc = args.ParsedExpression as PostfixExpression_MethodCall;
				
				if(mc.ArgumentCount == 0)
					return 0;
				for(int i = 0; i < mc.ArgumentCount; i++)
				{
					if(where <= mc.Arguments[i].EndLocation)
						return i+1;
				}
			}*/
			return args.CurrentlyTypedArgumentIndex;
		}
		
		public int GetCurrentParameterIndex (ICompletionWidget widget, CodeCompletionContext ctx)
		{
			/*
			int cursor = widget.CurrentCodeCompletionContext.TriggerOffset;
			var loc=new CodeLocation(ctx.TriggerLineOffset,ctx.TriggerLine);

			if (args.IsTemplateInstanceArguments)
			{

			}
			else
			{
				var firstArgLocation = CodeLocation.Empty;

				if (args.ParsedExpression is PostfixExpression_MethodCall)
					firstArgLocation = (args.ParsedExpression as PostfixExpression_MethodCall).Arguments[0].Location;
				else if (args.ParsedExpression is NewExpression)
					firstArgLocation = (args.ParsedExpression as NewExpression).Arguments[0].Location;
				else
					return -1;

				if (loc < firstArgLocation)
					loc = firstArgLocation;

				var code = doc.Editor.Document.GetTextBetween(firstArgLocation.Line,firstArgLocation.Column, scopeMethod.EndLocation.Line, scopeMethod.EndLocation.Column);

				var tr = new StringReader(code);
				var parser = new DParser(new Lexer(tr));
				parser.Lexer.SetInitialLocation(firstArgLocation);
				parser.Step();

				var updatedArguments = parser.ArgumentList();
				tr.Close();

				var lastArg = updatedArguments[updatedArguments.Count - 1];

				for (int i = 0; i < updatedArguments.Count; i++)
					if ((loc >= updatedArguments[i].Location && loc <= updatedArguments[i].EndLocation) ||
						(i==updatedArguments.Count-1 && loc <= updatedArguments[i].EndLocation))
						return i + 1;
			}
			*/
			return args.CurrentlyTypedArgumentIndex;
		}

		public override string GetParameterName(int overload, int currentParameter)
		{
			selIndex = overload;

			var param = GetParameterObj(currentParameter);

			if (param is AbstractNode)
				return ((AbstractNode)param).Name;
			else if (param is ITemplateParameter)
				return (param as ITemplateParameter).Name;

			return null;
		}

		public string GetHeading(int overload, string[] parameterMarkup, int currentParameter)
		{
			selIndex = overload;

			if (CurrentResult is DSymbol)
			{
				var s = "";
				var tir = (DSymbol)CurrentResult;

				var dm = tir.Definition as DMethod;
				if (dm != null)
				{
					s = GetMethodMarkup(dm, parameterMarkup, currentParameter);
				}
				else if (tir.Definition is DClassLike)
				{
					s = tir.Definition.Name + "(" + string.Join(",", parameterMarkup) + ")";
				}

				// Optional: description
				if (tir.Definition != null && !string.IsNullOrWhiteSpace(tir.Definition.Description))
					s += "\n\n " + tir.Definition.Description;

				return s;
			}
			else if (CurrentResult is DelegateType)
			{
				var dr = (DelegateType)CurrentResult;

				if (dr.IsFunctionLiteral && dr.DeclarationOrExpressionBase is FunctionLiteral)
					return GetMethodMarkup(((FunctionLiteral)dr.DeclarationOrExpressionBase).AnonymousMethod, parameterMarkup, currentParameter);
				else if(dr.DeclarationOrExpressionBase is DelegateDeclaration)
				{
					var dg = (DelegateDeclaration)dr.DeclarationOrExpressionBase;
					return dg.ReturnType.ToString() + " " + (dg.IsFunction?"function":"delegate") + "(" + string.Join(",", parameterMarkup) + ")";
				}
			}

			return "";
		}

		string GetMethodMarkup(DMethod dm, string[] parameterMarkup, int currentParameter)
		{
			var sb = new StringBuilder();

			switch (dm.SpecialType)
			{
				case DMethod.MethodType.Constructor:
					sb.Append("(Constructor) ");
					break;
				case DMethod.MethodType.Destructor:
					sb.Append("(Destructor) ");
					break;
				case DMethod.MethodType.Allocator:
					sb.Append("(Allocator) ");
					break;
			}
			/*TODO: Show attributes?
			if (dm.Attributes != null && dm.Attributes.Count > 0)
				s = dm.AttributeString + ' ';
			*/
			sb.Append(dm.Name);

			// Template parameters
			if (dm.TemplateParameters != null && dm.TemplateParameters.Length > 0)
			{
				sb.Append("(");

				if (args.IsTemplateInstanceArguments)
					sb.Append(string.Join(",", parameterMarkup));
				else
					foreach (var p in dm.TemplateParameters)
						sb.Append(p.ToString()+",");

				if(sb[0] == ',')
					sb.Remove(0,1);
				sb.Append(")");
			}

			// Parameters
			sb.Append("(");
			
			if (!args.IsTemplateInstanceArguments)
				sb.Append(string.Join(",", parameterMarkup));
			else
				foreach (var p in dm.Parameters)
					sb.Append(p.ToString() + ",");

			if(sb[0] == ',')
					sb.Remove(0,1);
			sb.Append(")");
			return sb.ToString();
		}

		public string GetParameterDescription(int overload, int paramIndex)
		{
			selIndex = overload;

			var param = GetParameterObj(paramIndex);

			if (param is AbstractNode)
				return ((AbstractNode)param).ToString(false);
			else if (param is ITemplateParameter)
				return param.ToString();

			return null;
		}

		public override int GetParameterCount (int overload)
		{			
			selIndex = overload;

			var parameters = GetParameters();
			
			if(parameters is ITemplateParameter[])
				return (parameters as ITemplateParameter[]).Length;
			else if(parameters is List<INode>)
				return (parameters as List<INode>).Count;
			
			return 0;
		}

		/// <summary>
		/// Count of overloads
		/// </summary>
		public override int Count {
			get { return args.ResolvedTypesOrMethods.Length; }
		}
		#endregion

		public override bool AllowParameterList(int overload)
		{
			return true;
		}

		public string GetDescription(int overload, int currentParameter)
		{
			selIndex = overload;

			var param = GetParameterObj(currentParameter);

			if (param is INode)
				return ((INode)param).Description;

			return null;
		}
	}
}

