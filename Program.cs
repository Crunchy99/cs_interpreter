using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

using DDW; //csparser

namespace aaa
{
	
	public partial class MainClass
	{
		//TODO: <>, switch statements, dynamic/var keyword, full initializers support, constructors, ..
		//TODO: function overloading parameter type checking. (Dynamic runtime casting)
		
		public static readonly string fn = @"C:\Users\gavin\Documents\dev\cs_interpreter rid of dynamic kw 63ms\cs_interpreter\DynamicProgram.cs";
		public static readonly bool DEBUG_PRINT_ALWAYS = false;
		
		public static Dictionary<string, Func<BL_Args, dynamic>> baselib = new Dictionary<string, Func<BL_Args, dynamic>>
		{
			{"Console.WriteLine", (args) => { Console.WriteLine(args[0]); return null; }},
			{"Math.Sqrt", (args) => Math.Sqrt(args[0]) },
			{"Math.Floor", (args) => Math.Floor(args[0]) },
			{"Math.Pow", (args) => Math.Pow(args[0], args[1]) },
			{"Math.Log", (args) => Math.Log(args[0], args[1]) },
			{"File.ReadAllLines", (args) => File.ReadAllLines(args[0]) },
			{"File.ReadAllBytes", (args) => File.ReadAllBytes(args[0]) },
			{"File.ReadAllText", (args) => File.ReadAllText(args[0]) },
			{"File.WriteAllLines", (args) => { File.WriteAllLines(args[0], args[1]); return null; }},
			{"File.WriteAllBytes", (args) => { File.WriteAllBytes(args[0], args[1]); return null; }},
			{"File.WriteAllText", (args) => { File.WriteAllText(args[0], args[1]); return null; }},
		};
		
		//normally the executing function has arguments collapsed (evaluated) and passed through, rather than being evaluated from written internal caller code.
		//baselib functions cannot currently have multiple function overloads.
		public struct BL_Args { InvocationExpression v; public dynamic this[int index] { get{ return handle_dyn(v.ArgumentList[index].Expression); } } public static implicit operator BL_Args(InvocationExpression v){ return new BL_Args(){ v=v }; } }
		
		
		
		
		static object handle_ArrayCreationExpression(object vv)
		{
			ArrayCreationExpression v = vv as ArrayCreationExpression;
			List<dynamic> items = new List<dynamic>();
			foreach(var expr in v.Initializer.Expressions){ items.Add(handle_dyn(expr)); }
			var ret = items.ToArray();
			
			if(v.Initializer.Expressions.Last is IntegralPrimitive)
			{
				int[] array = new int[v.Initializer.Expressions.Count];
				for(int i=0; i<array.Length; i++) array[i] = (int)items[i];
				return array;
			}
			if(v.Initializer.Expressions.Last is BooleanPrimitive)
			{
				bool[] array = new bool[v.Initializer.Expressions.Count];
				for(int i=0; i<array.Length; i++) array[i] = (bool)items[i];
				return array;
			}
			if(v.Initializer.Expressions.Last is RealPrimitive)
			{
				if(((RealPrimitive)(v.Initializer.Expressions.Last)).IsFloat)
				{
					float[] array = new float[v.Initializer.Expressions.Count];
					for(int i=0; i<array.Length; i++) array[i] = (float)items[i];
					return array;
				}
				else
				{
					double[] array = new double[v.Initializer.Expressions.Count];
					for(int i=0; i<array.Length; i++) array[i] = (double)items[i];
					return array;
				}
			}
			if(v.Initializer.Expressions.Last is DecimalPrimitive)
			{
				float[] array = new float[v.Initializer.Expressions.Count];
				for(int i=0; i<array.Length; i++) array[i] = (float)items[i];
				return array;
			}
			if(v.Initializer.Expressions.Last is StringPrimitive)
			{
				string[] array = new string[v.Initializer.Expressions.Count];
				for(int i=0; i<array.Length; i++) array[i] = (string)items[i];
				return array;
			}
			if(v.Initializer.Expressions.Last is CharPrimitive)
			{
				char[] array = new char[v.Initializer.Expressions.Count];
				for(int i=0; i<array.Length; i++) array[i] = (char)items[i];
				return array;
			}
			//"int","bool","float","double","string","string[]","long","char"
			
			return ret;
		}
		
		
		static object handle_ElementAccessExpression(object vv)
		{
			ElementAccessExpression v = vv as ElementAccessExpression;
			int index = (int)handle_dyn(v.Expressions[0]); //1-dimensional arrays only
			
			
			dynamic array = null;
			
			if(!(v is PrimaryExpression))
			{
				string Identifier = ((dynamic)v.LeftSide).Identifier;
				
				if(executingFunction.Localvars.has(Identifier)) array = executingFunction.Localvars[Identifier].value;
				else if(executingFunction.Params.has(Identifier)) array = executingFunction.Params[Identifier].value;
				else if(executingClass.fields.has(Identifier)) array = executingClass.fields[Identifier].value;
			}
			else //evaluate something like creating a new array right there.
				array = handle_dyn(v.LeftSide);
			
			var ret = array[index];
			return ret;
		}
		
		
		static object handle_ObjectCreationExpression(object vv)
		{
			ObjectCreationExpression v = vv as ObjectCreationExpression;
			string typename = ((dynamic)v.Type).GenericIdentifier;
			var obj2 = new ObjectInstance(typename, localVarDeclarationName);
			//TODO: argument list needs to be applied to a constructor => v.ArgumentsList
			if(obj2.Class.constructors.Count > 0)
			{
				//FIXME: match arguments to constructor forms
				obj2.Class.constructors[0].call(obj2);
			}
			if(localVarDeclarationName!=null) executingFunction.Localvars[localVarDeclarationName] = obj2; //-> e.g. new Class() will not be a tracked local
			localVarDeclarationName = null;
			
			var ret = obj2;
			return ret;
		}
		
		
		static object handle_LocalDeclarationStatement(object vv)
		{
			LocalDeclarationStatement v = vv as LocalDeclarationStatement;
			
			string typename = ((dynamic)v.Type).GenericIdentifier;
			//TODO: handle more complex local variable delcaration than this. Like templates or something, or "var t = new T(param1, param2)"
			executingFunction.Localvars[v.Declarators[0].Identifier.Identifier] = new ObjectInstance(typename, v.Declarators[0].Identifier.Identifier);
			
			//is this accounting for "Object o = new Object(..."? If so, this is where the identifier is.
			if(v.Declarators[0].Initializer != null)
				executingFunction.Localvars[v.Declarators[0].Identifier.Identifier].value = handle_dyn(v.Declarators[0].Initializer);
			
			//If the ".Initializer" is null, it has a null value, and is waiting to be written to.
			
			//we store the name for the object instance creation call ("new T(...)") - see use of "ObjectCreationExpression".
			localVarDeclarationName = v.Declarators[0].Identifier.Identifier;
			
			return null;
		}
		
		static object handle_IdentifierExpression(object vv)
		{
			IdentifierExpression v = vv as IdentifierExpression;
			//function's local variable
			if(executingFunction != null && executingFunction.Localvars.has(v.Identifier)) return executingFunction.Localvars[v.Identifier].value;
			
			//class function call. This Exception shouldn't be possible to hit anymore
			if(((Class)executingClass).functions.has(v.Identifier)) return new Exception("PrimaryExpression should not be handled as a raw function name but with arguments");
			
			//function parameter
			if(executingFunction != null && executingFunction.Params.has(v.Identifier)) return executingFunction.Params[v.Identifier].value;
			
			//class field
			if(executingClass.fields.has(v.Identifier)) return executingClass.fields[v.Identifier].value;
			
			return new NotImplementedException();
		}
		
		
		
		
		
		
		
		
		static object handle_IntegralPrimitive(object vv)
		{
			var v = (vv as IntegralPrimitive);
			switch(v.IntegralType)
			{
				case IntegralType.Byte: return (byte)v.Value;
				case IntegralType.Char: return (char)v.Value;
				case IntegralType.Int: return (int)v.Value;
				case IntegralType.Long: return (long)v.Value;
			}
			return new NotImplementedException();
		}
		
		static object handle_InvocationExpression(object vv)
		{
			InvocationExpression v = vv as InvocationExpression;
			//evaluate a qualified class instance's function, or a static class function
			if(v.LeftSide is MemberAccessExpression) 
			{
				dynamic dot = (v.LeftSide as MemberAccessExpression);
				while(dot.Left is MemberAccessExpression) dot = dot.Left; //aha
				
				if(((dynamic)dot).Left is ObjectCreationExpression) //-> e.g. "new Class2().addf()"
				{
					//collapse the function arguments down into runtime values
					List<dynamic> args2 = new List<dynamic>();
					foreach(var arg in v.ArgumentList) args2.Add(handle_dyn(arg.Expression));
					
					var firstValue = (ObjectInstance)handle_dyn(((dynamic)dot).Left as ObjectCreationExpression);
					var second2 = (((dynamic)dot).Right).Identifier;
					return firstValue.Class.functions[second2].call(args2, firstValue);
				}
				
				string first = ((dynamic)dot).Left.Identifier;
				string second = ((dynamic)dot).Right.Identifier;
				
				//if we have a language base library match.. (special case: the "args" are evaluated via the internal base lib as it executes. Strange pattern)
				if(baselib.ContainsKey(first+"."+second))
				{
					return baselib[first+"."+second](v);
				}
				
				
				//collapse the function arguments down into runtime values
				List<dynamic> args = new List<dynamic>();
				foreach(var arg in v.ArgumentList) args.Add(handle_dyn(arg.Expression));
				
				
				//if we have a "localvar.function()" match.. [always check local var first. symbol hiding rule]
				if(executingFunction != null && executingFunction.Localvars.has(first)) return executingFunction.Localvars[first].Class.functions[second].call(args, executingFunction.Localvars[first]);
				
				//if we have a "class.function()" match.. (static function call: special case; it will not need to access instance fields.
				if(classes.has(first) && classes[first].functions.has(second)) return classes[first].functions[second].call(args); //here we pass no class inst
				
				//if we have a "field.function()" match..
				if(executingClass.fields.has(first)) return executingClass.fields[first].Class.functions[second].call(args, executingClass.fields[first]);
				
				throw new NotImplementedException();
			}
			
			//call a function within this class
			if(v.LeftSide is IdentifierExpression) 
			{
				//collapse the function arguments down into runtime values
				List<dynamic> args = new List<dynamic>();
				foreach(var arg in v.ArgumentList) args.Add(handle_dyn(arg.Expression));
				
				Function fn = ((Class)executingClass).functions[((dynamic)v.LeftSide).Identifier];
				return fn.call(args);
			}
			
			throw new NotImplementedException();
		}
		
		
		
		static bool _member_access_ptr;
		static object handle_MemberAccessExpression(object _dot)
		{
			MemberAccessExpression dot = _dot as MemberAccessExpression;
			//TODO: getters/setters/properties
			
			dynamic left = dot.Left;
			dynamic right = dot.Right;
			
			if(left is MemberAccessExpression)
			{
				while(left is MemberAccessExpression) left = handle_dyn(dot.Left); //aha!
				if(_member_access_ptr) return left.fields[right.Identifier];
				else return left.fields[right.Identifier].value;
			}
			
			
			string first = left.Identifier;
			string second = right.Identifier;
			
			//see "handle(BinaryExpression v)"
			if(_member_access_ptr)
			{
				//if we have a "localvar.field" match.. [always check local var first. symbol hiding rule]
				if(executingFunction.Localvars.has(first)) return executingFunction.Localvars[first].fields[second];
				
				//if we have a "class.field" match.. (static field access)
				if(classes.has(first) && classes[first].staticfields.has(second)) return classes[first].staticfields[second];
				
				//if we have a "field.field" match..
				if(executingClass.fields.has(first)) return executingClass.fields[first].fields[second];
			}
			else
			{
				//if we have a "localvar.field" match.. [always check local var first. symbol hiding rule]
				if(executingFunction.Localvars.has(first)) return executingFunction.Localvars[first].fields[second].value;
				
				//if we have a "class.field" match.. (static field access)
				if(classes.has(first) && classes[first].staticfields.has(second)) return classes[first].staticfields[second].value;
				
				//if we have a "field.field" match..
				if(executingClass.fields.has(first)) return executingClass.fields[first].fields[second].value;
			}
			
			
			
			
			
			throw new NotImplementedException();
		}
		
		
		
		
		
		static dynamic handle_BinaryExpression(object vv)
		{
			BinaryExpression v = vv as BinaryExpression;
			//At the comparison level, we always have to end up evaluating the expressions.
			//If we didn't use "_member_access_ptr" then we wouldn't have a handle to the ObjectInstance's dynamic "value"
			
			_member_access_ptr = true;
			
			dynamic lhs = handle_dyn(v.Left); //"left-hand-side"
			dynamic rhs = handle_dyn(v.Right);
			
			_member_access_ptr = false;
			
			if(v.Op == TokenID.Greater) return lhs > rhs;
			if(v.Op == TokenID.Less) return lhs < rhs;
			if(v.Op == TokenID.And) return lhs && rhs;
			if(v.Op == TokenID.Plus) return lhs + rhs;
			if(v.Op == TokenID.Minus) return lhs - rhs;
			if(v.Op == TokenID.Star) return lhs * rhs;
			if(v.Op == TokenID.Slash) return lhs / rhs;
			if(v.Op == TokenID.GreaterEqual) return lhs >= rhs;
			if(v.Op == TokenID.LessEqual) return lhs <= rhs;
			if(v.Op == TokenID.EqualEqual) return lhs == rhs;
			
			//At this point, it's a member access or a field/property
			
			var _lhs = v.Left is MemberAccessExpression ? lhs : findLhs(v.Left);
			
			if(v.Op == TokenID.Equal) { _lhs.value = rhs; return null; }
			if(v.Op == TokenID.PlusEqual) { _lhs.value += rhs; return null; }
			if(v.Op == TokenID.MinusEqual) { _lhs.value -= rhs; return null; }
			
			throw new NotImplementedException();
		}
		
		static object findLhs(dynamic d)
		{
			//Since it's not a member access expression in this case, it has no dot, and it has one token.
			if(executingFunction.Localvars.has(d.Identifier)) return executingFunction.Localvars[d.Identifier];
			if(executingFunction.Params.has(d.Identifier)) return executingFunction.Params[d.Identifier];
			if(executingClass.fields.has(d.Identifier)) return executingClass.fields[d.Identifier];
			throw new NotImplementedException();
		}
		
		
		static dynamic handle_UnaryExpression(object vv)
		{
			UnaryExpression v = vv as UnaryExpression;
			dynamic rhs = handle_dyn(v.Child);
			
			if(v.Op == TokenID.Not) return !rhs;
			if(v.Op == TokenID.Minus) return -rhs;
			
			if(v is UnaryCastExpression)
			{
				string typename = ((dynamic)(v as UnaryCastExpression)).Type.GenericIdentifier;
				switch(typename)
				{
				case "int": return (int)rhs;
				case "float": return (float)rhs;
				case "double": return (double)rhs;
				default: return new NotImplementedException();
				}
			}
			
			throw new NotImplementedException();
		}
		
		
		
		
		static object handle_IfStatement(object vv)
		{
			IfStatement v = vv as IfStatement;
			if(((dynamic)handle_dyn(v.Test)) == true)
				foreach(var sta in v.Statements.Statements) handle_dyn(sta);
			else
				foreach(var sta in v.ElseStatements.Statements) handle_dyn(sta);
			return null;
		}
		
		
		
		
		
		
		
		static object handle_dyn(object v)
		{
			if(v is ArrayCreationExpression) return handle_ArrayCreationExpression(v);
			if(v is ElementAccessExpression) return handle_ElementAccessExpression(v);
			if(v is ObjectCreationExpression) return handle_ObjectCreationExpression(v);
			if(v is ExpressionStatement) return handle_dyn((v as ExpressionStatement).Expression);
			
			if(v is LocalDeclarationStatement) return handle_LocalDeclarationStatement(v);
			if(v is IdentifierExpression) return handle_IdentifierExpression(v);
			
			if(v is RealPrimitive) return ((RealPrimitive)v).IsFloat ? (float)((RealPrimitive)v).Value : (double)((RealPrimitive)v).Value;
			if(v is BooleanPrimitive) return ((BooleanPrimitive)v).Value;
			if(v is StringPrimitive) return ((StringPrimitive)v).Value;
			if(v is IntegralPrimitive) return handle_IntegralPrimitive(v);
			if(v is InvocationExpression) return handle_InvocationExpression(v);
			
			if(v is MemberAccessExpression) return handle_MemberAccessExpression(v);
			if(v is BinaryExpression) return handle_BinaryExpression(v);
			if(v is UnaryExpression) return handle_UnaryExpression(v);
			if(v is IfStatement) return handle_IfStatement(v);
			
			//At the "return 0;" here, we return a non-null value as a special case, to end the Function.call().foreach() pattern.
			if(v is ReturnStatement) { executingFunction.returnValue = handle_dyn((v as ReturnStatement).ReturnValue as ExpressionNode); return 0; };
			
			if(v is NullPrimitive) return null; //ha ha
			
			
			if(v is ParenthesizedExpression) return handle_dyn((v as ParenthesizedExpression).Expression);
			
			throw new NotImplementedException();
		}
		
		
		
		
		
		public static void Main (string[] args)
		{
			//stopwatch_measure_native_function(() => Fibonacci(22));
			
			DateTime lastver = new DateTime();
			CompilationUnitNode cu = null;
			ObjectInstance mainClass = null;
			
			while(true)
			{
				while(true) //sleep while source code remains unchanged
				{
					bool refresh = lastver != File.GetLastWriteTime(fn);
					lastver = File.GetLastWriteTime(fn);
					
					if(refresh) break;
					else System.Threading.Thread.Sleep(90);
				}
				
				
				Console.Clear();
				
				var sw = new System.Diagnostics.Stopwatch();
				//sw.Start();
				
				try
				{
					Lexer l = new Lexer(new StreamReader(new FileStream(fn, FileMode.Open, FileAccess.Read), true));
					var toks = l.Lex();
					
					Parser p = new Parser(fn);
					cu = p.Parse(toks, l.StringLiterals);
					
					if(p.Errors.Count>0)
					{
						Console.WriteLine("***ERRORS***");
						continue;
					}
				}
				catch(Exception e)
				{
					Console.Clear();
					Console.WriteLine("***ERRORS***");
					continue;
				}
				
				//*************************
				
				//link schema and initialize some memory
				{
					setup(cu);
					
					sw.Start();
					
					//run interpreter from the main() function
					mainClass = new ObjectInstance("InterpretedProgram", "interpreter_runtime_main_object");
					((Class)mainClass).functions["DynamicMain"].call(new List<dynamic>(){null}, mainClass);
					
					//*************************
					
					sw.Stop();
				}
				
				
				Console.WriteLine("**time taken: " + sw.ElapsedMilliseconds + " ms");
			}
		}
		
		public static void stopwatch_measure_native_function(Action a)
		{
			var sw = new System.Diagnostics.Stopwatch();
			sw.Start();
			a();
			sw.Stop();
			Console.WriteLine("**time taken: " + sw.ElapsedMilliseconds + " ms");
		}
		
		
		
		//public static dynamic printret(dynamic d) { if(DEBUG_PRINT_ALWAYS) Console.WriteLine("**returning: " + d); return d; }
		
		//public static dynamic printPlus(dynamic d1, dynamic d2) { if(DEBUG_PRINT_ALWAYS) Console.WriteLine("**calculation: d1 ("+d1+") + d2 ("+d2+") = " + (d1+d2)); return (d1+d2); }
		//public static dynamic printMinus(dynamic d1, dynamic d2) { if(DEBUG_PRINT_ALWAYS) Console.WriteLine("**calculation: d1 ("+d1+") - d2 ("+d2+") = " + (d1-d2)); return (d1-d2); }
		
		//public static void print(string s) { if(DEBUG_PRINT_ALWAYS) Console.WriteLine(s); }
		
		
		
		
		public static int Fibonacci(int number)
		{
			if (number <= 1)
				return 1;
			else
				return Fibonacci(number - 2) + Fibonacci(number - 1);
		}
		
		
		
	}



	static class ext { public static bool in_(this string val, List<string> x) { return x.Contains(val); } public static bool isStatic(this FieldNode fn) { return ((((uint)fn.Modifiers) & (uint)Modifier.Static) != 0); }		}










}