using System;
using System.Linq;
using System.Collections.Generic;

using DDW; //csparser

namespace aaa
{
	public partial class MainClass
	{
		
		//constants are handled nicely by csparser
		public static dictionary<string, Class> classes;
		public static dictionary<string, Class> genericsTypes;
		public static ObjectInstance executingClass;
		public static Function executingFunction;
		public static string localVarDeclarationName;
		
		
		
		static void setup(CompilationUnitNode cu)
		{
			foreach(var ns in cu.Namespaces)
			{
				foreach(var cs in ns.Classes)
				{
					classes[cs.Name.Identifier] = new Class(){ classnode = cs };
				}
				
				foreach(var cs in ns.Classes)
				{
					var cls = classes[cs.Name.Identifier];
					
					//"register" the class function symbols
					foreach(var method in cs.Methods)
					{
						var f = new Function(){method = method, ownerClass=cls};
						f.Params = new Parameters();
						
						//!!! we assume csparser has given us the args in the right order (we establish both the string name key AND integer index access methods)
						foreach(var param in method.Params)
						{
							string typename = ((dynamic)param).Type.Identifier.GenericIdentifier;
							f.Params[param.Name] = new ObjectInstance(typename, param.Name);
						}
						
						f.Params.isInit=false;
						cls.functions[f.name] = f;
						cls._functionOverloads.Add(f);
					}
				}
				
				foreach(var cs in ns.Classes)
				{
					var cls = classes[cs.Name.Identifier];
					
					cls.setupCtors(null);
					
					cls.setupGenericClasses();
					
					cls.initFields(staticInit: true);
					if(cls.staticConstructor != null)
						cls.staticConstructor.call(cls.staticNullInstance);
				}
				
				
			}
			
		}
		
		
		
		//no structs
		static List<string> nonclass_types = new string[]{ "int","bool","float","double","string","string[]","long","char", }.ToList();
		
		
		
		public class Function
		{
			public string name { get { return method != null ? method.GenericIdentifier.Split(' ')[1] : fnptrName; } }
			public Class ownerClass;
			public MethodNode method;
			public Parameters Params;
			public Func<List<dynamic>, ObjectInstance, int> fnptr = null;
			public string fnptrName;
			public dictionary<string, ObjectInstance> Localvars;
			public dynamic returnValue;
			
			
			//This is all very delicate form.
			public dynamic _call(List<dynamic> args, ObjectInstance fnScopeExecutingClassInstance = null)
			{
				var callerFn = executingFunction;
				var callerInst = executingClass;
				
				executingFunction = this; //push to the function stack
				executingClass = fnScopeExecutingClassInstance ?? executingClass;
				
				
				if(fnptr != null)
				{
					fnptr(args, fnScopeExecutingClassInstance);
				}
				else
				{
					if(method.Params.Count != args.Count) throw new Exception("Wrong number of arguments");
					
					//linkup the arguments
					for(int i=0; i<args.Count; i++) 
						Params[i].value = args[i]; 
					
					
					//collapse a series of statements down as trees of evaluation
					foreach(var sta in method.StatementBlock.Statements)
					{
						if(returnValue != null) break;
						
						handle_dyn(sta); //do statement. Early return might be done here
					}
				}
				
				executingFunction = callerFn; //pop the function stack
				executingClass = callerInst; //pop class member entry stack
				
				
				return returnValue;
			}
			
			
			
			
			
			//wrapper function to support function overloading.
			public dynamic call(List<dynamic> args, ObjectInstance fnScopeExecutingClassInstance = null)
			{
				if(ownerClass == null) return 0;
				
				//find and call the right overload
				var overloads = ownerClass._functionOverloads.Where(f=>f.name == name);
				if(overloads.Count() == 1){ return clone()._call(args, fnScopeExecutingClassInstance); }
				
				foreach(var ovl in overloads)
				{
					if(args.Count != ovl.Params.items.Count) continue;
					
					bool parametersFit=true;
					//for(int i=0; i<ovl.Params.items.Count; i++)
					//	if(!canDynamicCast(args[i], ovl.Params[i].GetType())) parametersFit=false;
					
					if(parametersFit) return ovl.clone()._call(args, fnScopeExecutingClassInstance);
				}
				
				if(fnptr != null)
				{
					_call(args, fnScopeExecutingClassInstance);
					return 0;
				}
				else
					throw new Exception("couldn't find the function overload");
			}
			
			
			
			
			//The function when called (again further up the stack!) shouldn't be messing with the same Localvars or Params pointers/memory.
			//We linkup the essential opaque data to the new instance. The rest is dynamically added.
			public Function clone(){ return new Function(){ method=method, ownerClass=ownerClass, Params=Params.clone() }; }
			
			
		}
		
		
		public class ctor
		{
			public List<string> parametersTypes = new List<string>();
			public ConstructorNode node;
			public ctor(ConstructorNode node, Class ownerClass){ this.node=node; this.myClass=ownerClass; }
			public Class myClass;
			public void call(ObjectInstance obj)
			{
				var cls = executingClass;
				executingClass = obj;
				foreach(var sta in node.StatementBlock.Statements)
					handle_dyn(sta);
				executingClass = cls;
			}
		}
		
		public class Class
		{
			public ClassNode classnode;
			public string genericsname;
			public string name{get{return classnode != null ? classnode.Name.Identifier : genericsname;}}
			public ObjectInstance staticNullInstance = null;
			public ctor staticConstructor = null;
			public List<ctor> constructors = new List<ctor>();
			public dictionary<string, Function> functions;
			public dictionary<string, ObjectInstance> staticfields;
			public dictionary<string, string> fieldNames;
			public List<Function> _functionOverloads = new List<Function>();
			
			
			public void setupCtors(ObjectInstance obj=null)
			{
				foreach(var _ctor in classnode.Constructors)
				{
					if(_ctor.IsStaticConstructor)
						staticConstructor = new ctor(_ctor, this);
					else if(!_ctor.IsStaticConstructor)
						constructors.Add(new ctor(_ctor, this));
				}
				
				staticNullInstance = new ObjectInstance(name, name+"_staticNullInstance", false);
			}
			
			public void initFields(ObjectInstance obj=null, bool staticInit=false)
			{
				if(classnode == null)
				{
					//we are handling a generic type
					if(genericsTypes.has(obj.typename))
					{
						var type = genericsTypes[obj.typename];
						//type.constructors
						if(type.name == "List<object>")
						{
							obj.value = new List<object>();
						}
						if(type.name == "Dictionary<string, object>")
						{
							obj.value = new Dictionary<string, object>();
						}
					}
					else
						throw new NotImplementedException();
				}
				else
				{
					//initialize class fields
					foreach(var field in classnode.Fields)
					{
						string typename = ((dynamic)field.Type).GenericIdentifier;
						
						
						var fieldname = field.Names[0].GenericIdentifier;
						
						if(staticInit && field.isStatic())
							this.staticfields[fieldname] = new ObjectInstance(typename, fieldname);
						if(!staticInit && typename != name)
							obj._fields[fieldname] = new ObjectInstance(typename, fieldname);
						if(!staticInit && typename == name)
							obj._fields[fieldname] = new ObjectInstance(typename, fieldname, allocate: false); //self-referential fields are allocated on demand
						
						fieldNames[fieldname] = fieldname;
					}
				}
				
			}
			
			public void setupGenericClass(string typename)
			{
				if(!genericsTypes.has(typename))
				{
					genericsTypes[typename] = new Class();
					//genericsTypes[typename].constructors
					genericsTypes[typename].genericsname = typename;
					genericsTypes[typename].functions["Add"] = new Function();
					genericsTypes[typename].functions["Add"].fnptr = (args, obj) => {
						(obj.value as List<object>).Add(args[0]); return 0;
					};
					genericsTypes[typename].functions["Add"].fnptrName = "Add";
					genericsTypes[typename].functions["Add"].ownerClass = genericsTypes[typename];
				}
			}
			
			public void setupGenericClasses()
			{
				setupGenericClass("List<object>");
				foreach(var innerTypeName in nonclass_types)
					setupGenericClass("List<" + innerTypeName + ">");
				foreach(var cls in classes.dict.Values)
					setupGenericClass("List<" + cls.name + ">");
				
				setupGenericClass("Dictionary<object>");
				foreach(var innerTypeName in nonclass_types)
					setupGenericClass("Dictionary<" + innerTypeName + ">");
				foreach(var cls in classes.dict.Values)
					setupGenericClass("Dictionary<" + cls.name + ">");
				
			}
			
		}
		
		
		//class field, local variable etc.
		public class ObjectInstance
		{
			//If you don't want the self-referential class fields feature, you can trash these 3 lines and just have "public dynamic value;" (Same for "fields" field).
			public dynamic _value;
			public dynamic value { get { if(!init && Class!=null && !typename.in_(nonclass_types)) { Class.initFields(this); init=true; } return _value; } set{ _value = value; } }
			public bool init;
			public static implicit operator Class(ObjectInstance o){ return o.Class; }
			public string typename;
			public string name;
			public bool isArray{ get{ return typename.Contains("[]"); } }
			public dictionary<string, ObjectInstance> _fields;
			public dictionary<string, ObjectInstance> fields { get { if(!init && Class!=null && !typename.in_(nonclass_types)) { Class.initFields(this); init=true; } return _fields; } set{ _fields = value; } }
			public Class Class;
			
			public ObjectInstance(string typename, string name, bool allocate=true)
			{
				this.typename=typename;
				this.name=name;
				
				if(typename.in_(nonclass_types))
				{
					//indicate type
					switch(typename)
					{
					case "int": { value=(int)0; break; }
					case "bool": { value=false; break; }
					case "float": { value=(float)0; break; }
					case "double": { value=(double)0; break; }
					case "string": { value=""; break; }
					case "string[]": { value=new string[]{""}; break; }
					case "long": { value=(long)0; break; }
					case "char": { value=(char)0; break; }
					default: throw new NotImplementedException();
					}
				}
				else //class or struct or generic. Structs are not allowed because it's bad programming
				{
					if(typename.Contains("List<") || typename.Contains("Dictionary<"))
						Class = genericsTypes[typename];
					else
					{
						Class = classes[typename];
						//foreach nonstatic field in the class, we attach an object instance field by that name. (class fields' initializer syntax is not supported)
						if(allocate) Class.initFields(this);
					}
				}
			}
			
			
		}
		
		
		
		
		
		public class Parameters
		{
			public List<ValuePair<string, ObjectInstance>> items = new List<ValuePair<string, ObjectInstance>>();
			
			public bool isInit=true;
			
			public ObjectInstance this[string byName]  {  get  {  return items.Where(x => x.k == byName).First().value;  }  set  {  if(isInit)  items.Add(new ValuePair<string, ObjectInstance>(){k=byName, value=value});  else { var x = items.Where(i=>i.k==byName).First(); x.value=value; } }  }
			public ObjectInstance this[int byIndex] { get { return items[byIndex].value; } }
			
			//function params list constitutes a unique name, to differentiate function overloads
			public override string ToString () { string label="("; foreach(var p in items) {label += p.k + ", ";} label=label.Substring(0, label.Length-2); label+=")"; return label; }
			
			public bool has(string key){ return items.Where(i=>i.k==key).Count()>0; }
			
			
			public Parameters clone() { var Params = new Parameters(); foreach(var param in items) { var v = param.value; Params[v.name] = new ObjectInstance(v.typename, v.name); } return Params; }
		}
		
		
		public struct ValuePair<K,V> { public K k; public V value; }
		
		
		//all this does is avoids repeating the syntax, "dictionary<K,V> dict = new dictionary<K,V>()", this way it's clearer and easier to read.
		public struct dictionary<K,V> { public bool has(K k){ if(dict==null) return false; return dict.ContainsKey(k);} public Dictionary<K,V> dict; public V this[K key] { get { if(dict==null) dict = new Dictionary<K,V>(); return dict[key]; } set { if(dict==null) dict = new Dictionary<K,V>(); dict[key] = value; } } }
		
		
		
		
		
		
	}
}

