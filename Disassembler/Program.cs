using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.IO;
using Disassembler.Model;

namespace Disassembler
{
    class Program
    {
        private Assembly toDebug;
        private List<string> excludedTypeNames;
        private List<string> onlyTypes;
        private List<string> onlyMethods;
        private List<object> arguments;
        private string match;
        private bool matching;
        private bool hideAnonymous;
        private bool declaredOnly;
        
        public Program(List<string> args)
        {
            excludedTypeNames = new List<string>();
            ShowBanner();
            if (args.Any(x => x == "--help" || x == "-h")) { ShowHelp(); Environment.Exit(0); }            
            if (args.Any(x => x.StartsWith("--exclude-types=")))
                excludedTypeNames = args.Where(x => x.StartsWith("--exclude-types=")).Single().Split(new char[] { '=', '+' }).Skip(1).ToList();
            if (args.Any(x => x.StartsWith("--only-types=")))
                onlyTypes = args.Where(x => x.StartsWith("--only-types=")).Single().Split(new char[] { '=', '+' }).Skip(1).ToList();
            if (args.Any(x => x.StartsWith("--only-methods=")))
                onlyMethods = args.Where(x => x.StartsWith("--only-methods=")).Single().Split(new char[] { '=', '+' }).Skip(1).ToList();
            hideAnonymous = args.Any(x => x == "--hide-anonymous");
            declaredOnly = args.Any(x => x == "--declared-only" || x == "-d");
            if (matching = args.Any(x => x == "--methods-matching"))
                match = Enumerable.Range(0, args.Count).Where(x => args[x] == "--methods-matching").Select(x => args[x + 1]).Single().ToLower();
            toDebug = Assembly.LoadFile(Path.GetFullPath(args.Last()));
            if (args.Any(x => x == "--disassemble-all" || x == "-a"))
                GetSelectedTypes().ToList().ForEach(x => ExamineType(x));
            else if (args.Any(x => x == "--list-types" || x == "-t"))
                GetSelectedTypes().ToList().ForEach(x => Console.WriteLine(x));
            else if (args.Any(x => x == "--show-structure" || x == "-s"))
                ShowStructure();
            else if (args.Any(x=>x.StartsWith("--execute-method=")))
            {
                arguments = args.Where(x => x.StartsWith("--execute-method=")).Single().Split(new char[] { '=', '+' }).Skip(1).Select(x => (object)x).ToList();
                var type = GetStaticNonPublicMethods(GetSelectedTypes().Single()).Single();
                type.Invoke(null, arguments.Skip(1).ToArray());
                Console.WriteLine("Method {0} has been executed :)", type.Name);
            }
            else
                ShowHelp();
            Console.WriteLine();
        }

        private void ShowHelp()
        {
            Console.WriteLine("-a / --disassemble-all\t\t\tShow the code for all the assembly");
            Console.WriteLine("-d / --declared-only\t\t\tShow only methods declared (not inherited)");
            Console.WriteLine("-t / --list-types\t\t\tShow all types in the assembly");
            Console.WriteLine("-h / --help\t\t\t\tShow this help");            
            Console.WriteLine("--exclude-types=<type>+<type>...\tExclude a list of types from analysis");            
            Console.WriteLine("--execute-method=<name>+<argument>+...\tExecute a method (only static methods)");
            Console.WriteLine("--methods-matching <text>...\t\tOnly look for methods that contains the text");
            Console.WriteLine("--only-types=<type>+<type>...\t\tOnly selected types will be analysed");
            Console.WriteLine("--only-methods=<mathod>+<method>...\tOnly selected methods will be analysed");
            Console.WriteLine("--show-structure\t\t\tShow all types and its methods");
            Console.WriteLine("--hide-anonymous\t\t\tDo not analyze anonymous types\n");            
        }

        private void ShowBanner()
        {
            Console.WriteLine();
            Console.WriteLine("DotNet disassembler v1.0 - Pablo Carballude\n");
        }

        private IEnumerable<Type> GetSelectedTypes()
        {
            return from type in toDebug.GetTypes()
                   where !excludedTypeNames.Any(x => x == type.ToString())
                   && (hideAnonymous ? !type.ToString().Contains("_AnonymousType") : true)
                   && (onlyTypes == null ? true : onlyTypes.Any(x => x == type.ToString()))
                   orderby type.Name ascending
                   select type;
        }

        private void ShowStructure()
        {
            foreach (var type in GetSelectedTypes())
            {
                Console.WriteLine(type);
                foreach (var constructor in GetConstructors(type))
                    Console.WriteLine("\t"+(constructor.Attributes.ToString().Contains("Public")?"Public":"NonPublic")+" "+constructor.Name);
                GetPublicMethods(type).ToList().ForEach(x => Console.WriteLine("\tPublic " + x));
                GetNonPublicMethods(type).ToList().ForEach(x => Console.WriteLine("\tNonPublic " + x));
                GetStaticPublicMethods(type).ToList().ForEach(x => Console.WriteLine("\tStatic Public " + x));
                GetStaticNonPublicMethods(type).ToList().ForEach(x => Console.WriteLine("\tStatic NonPublic " + x));
                Console.WriteLine();
            }
        }

        private IEnumerable<ConstructorInfo> GetConstructors(Type type)
        {
            return from constructor in type.GetConstructors()
                   where ((onlyMethods == null) ? true : onlyMethods.Any(x => x == constructor.Name))
                   && (matching ? constructor.Name.ToLower().Contains(match) : true)
                   orderby constructor.Name ascending
                   select constructor;
        }

        private IEnumerable<MethodInfo> GetStaticPublicMethods(Type type)
        {
            MethodInfo[] selectedMethods;
            selectedMethods= type.GetMethods(BindingFlags.Static | BindingFlags.Public);
            return from method in selectedMethods
                   where (onlyMethods == null ? true : onlyMethods.Any(x => x == method.Name))
                   && (matching ? method.Name.ToLower().Contains(match) : true)
                   orderby method.Name ascending
                   select method;
        }

        private IEnumerable<MethodInfo> GetStaticNonPublicMethods(Type type)
        {
            MethodInfo[] selectedMethods;
            selectedMethods = type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic);
            return from method in selectedMethods
                   where (onlyMethods == null ? true : onlyMethods.Any(x => x == method.Name))
                   && (matching ? method.Name.ToLower().Contains(match) : true)
                   orderby method.Name ascending
                   select method;
        }

        private IEnumerable<MethodInfo> GetPublicMethods(Type type)
        {
            MethodInfo[] selectedMethods;
            if (declaredOnly)
                selectedMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            else
                selectedMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
            return from method in selectedMethods
                   where (onlyMethods == null ? true : onlyMethods.Any(x => x == method.Name))
                   && (matching ? method.Name.ToLower().Contains(match) : true)
                   orderby method.Name ascending
                   select method;
        }

        private IEnumerable<MethodInfo> GetNonPublicMethods(Type type)
        {
            MethodInfo[] selectedMethods;
            if (declaredOnly)
                selectedMethods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            else
                selectedMethods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance);
            return from method in selectedMethods
                   where (onlyMethods == null ? true : onlyMethods.Any(x => x == method.Name))
                   && (matching ? method.Name.ToLower().Contains(match) : true)
                   orderby method.Name ascending
                   select method;
        }

        private void ExamineType(Type type)
        {
            Console.WriteLine(type);
            var constructors = GetConstructors(type);
            constructors.ToList().ForEach(x => ExamineConstructorOrMethod(x));
            var publicMethods = GetPublicMethods(type);
            var nonPublicMethods = GetNonPublicMethods(type);
            GetPublicMethods(type).Concat(GetNonPublicMethods(type)).Concat(GetStaticPublicMethods(type)).Concat(GetStaticNonPublicMethods(type)).ToList().ForEach(x => ExamineConstructorOrMethod(x));                        
        }

        private void ExamineConstructorOrMethod(dynamic info)
        {
            if (onlyMethods != null && !onlyMethods.Any(x => x == info.Name)) return;
            Console.WriteLine("\n\t" + (info.IsPublic ? "Public " : "NonPublic ") + info);
            var body = info.GetMethodBody();
            if (body != null)
            {
                ExamineLocalVariables(body, null);
                ExamineInstructions(info);
            }
        }

        private void ExamineLocalVariables(MethodBody body, MethodInfo method = null)
        {
            Console.WriteLine("\n\t\t-- Local variables");
            body.LocalVariables.ToList().ForEach(x => Console.WriteLine("\t\t| " + x));
            Console.WriteLine("\t\t-- End of local variables\n");
        }

        private void ExamineInstructions(dynamic info)
        {
            var instructions = ILArrayToList(info.GetMethodBody().GetILAsByteArray());
            for (int i = 0; i < instructions.Count; i += instructions[i].Lenght + 1)
                Console.WriteLine("\t\tIL_" + i + ": " + PrintInstruction(instructions[i], info.Module));
        }

        private Func<MSILInstruction, Module, string> PrintInstruction = (x, y) => instructionIsInlineMethod(x) ? PrintInlineMethod(x, y) : x.OpCode.Name;
        private static Func<byte, bool> isInlineMethod = x => GetOpCode(x).OperandType == OperandType.InlineMethod;
        private static Func<MSILInstruction, bool> instructionIsInlineMethod = x => x.OpCode.OperandType == OperandType.InlineMethod;
        private Func<byte, int> ReadInstructionLenght = x => isInlineMethod(x) ? 4 : 0;

        private static string PrintInlineMethod(MSILInstruction instruction, Module module)
        {
            var methodBase = module.ResolveMethod((int)instruction.MetadataToken);
            return instruction.OpCode.Name + " " + methodBase.ReflectedType + "::" + methodBase.Name;
        }

        private List<MSILInstruction> ILArrayToList(byte[] array)
        {
            var instructions = new List<MSILInstruction>();
            for (int i = 0; i < array.Length; i++)
            {
                instructions.Add(new MSILInstruction()
                {
                    Lenght = ReadInstructionLenght(array[i]),
                    MetadataToken = ReadMetadataToken(array, i),
                    OpCode = ReadOpCode(array, i)
                });
            }
            return instructions;
        }        

        private void ReadInlineMethod(byte[] array, int i,  MSILInstruction instruction)
        {
            instruction.Lenght = 4;
            Int64 arg = 0;
            for (int j = 0; j < instruction.Lenght; ++j)
            {
                Int64 v = array[(i + 1) + j];
                arg += v << (j * 8);
            }
            instruction.MetadataToken = arg;
        }

        private OpCode ReadOpCode(byte[] array, int i)
        {
            return array[i] != 254 ? GetOpCode(array[i]) : GetOpCode((ushort)(array[++i] + 65024));
        }

        private static Int64 ReadMetadataToken(byte[] array, int i)
        {
            if (!isInlineMethod(array[i]))
                return 0;
            Int64 arg = 0;
            for (int j = 0; j < 4; ++j)
            {
                Int64 v = array[(i + 1) + j];
                arg += v << (j * 8);
            }
            return arg;
        }

        private static OpCode GetOpCode(ushort code)
        {
            return (from field in typeof(OpCodes).GetFields()
                    let opCode = (OpCode)field.GetValue(null)
                    where code == opCode.Value
                    select opCode).DefaultIfEmpty(OpCodes.Nop).SingleOrDefault();
        }

        static void Main(string[] args)
        {
            new Program(args.ToList());
        }
    }
}
