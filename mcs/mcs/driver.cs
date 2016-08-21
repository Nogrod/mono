//
// driver.cs: The compiler command line driver.
//
// Authors:
//   Miguel de Icaza (miguel@gnu.org)
//   Marek Safar (marek.safar@gmail.com)
//
// Dual licensed under the terms of the MIT X11 or GNU GPL
//
// Copyright 2001, 2002, 2003 Ximian, Inc (http://www.ximian.com)
// Copyright 2004, 2005, 2006, 2007, 2008 Novell, Inc
// Copyright 2011 Xamarin Inc
//

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;
using System.Diagnostics;
#if STATIC
using System.Threading;
using ObjectStream;
using ObjectStream.Data;
using System.Text.RegularExpressions;
#endif

namespace Mono.CSharp
{
	/// <summary>
	///    The compiler driver.
	/// </summary>
	class Driver
	{
		readonly CompilerContext ctx;
#if STATIC
		static readonly Regex FileErrorRegex = new Regex(@"([\w\.]+)\(\d+,\d+\): error|error \w+: Source file `[\\\./]*([\w\.]+)", RegexOptions.Compiled);
        static MemoryStream assemblyStream;
        static Dictionary<string, byte[]> _sourceFiles;
		static Dictionary<string, byte[]> _referenceFiles;
		static StringBuilder _consoleOut;
		static bool _shouldRun = true;
		static bool _ready;
		static bool _exitReceived;
		static string _logPath = "";
		static TextWriter _logWriter;
		const string Version = "1.0.0";
#endif
		public Driver (CompilerContext ctx)
		{
			this.ctx = ctx;
		}

		Report Report {
			get {
				return ctx.Report;
			}
		}

		void tokenize_file (SourceFile sourceFile, ModuleContainer module, ParserSession session)
		{
			Stream input;
#if STATIC
			if (_sourceFiles != null)
			{
				if (_sourceFiles.ContainsKey(sourceFile.Name))
					input = new MemoryStream(_sourceFiles[sourceFile.Name]);
				else
				{
					Report.Error(2001, "Source file `" + sourceFile.Name + "' could not be found");
					return;
				}
			}
			else
#endif
			try {
				input = File.OpenRead (sourceFile.Name);
			} catch {
				Report.Error (2001, "Source file `" + sourceFile.Name + "' could not be found");
				return;
			}

			using (input){
				SeekableStreamReader reader = new SeekableStreamReader (input, ctx.Settings.Encoding);
				var file = new CompilationSourceFile (module, sourceFile);

				Tokenizer lexer = new Tokenizer (reader, file, session, ctx.Report);
				int token, tokens = 0, errors = 0;

				while ((token = lexer.token ()) != Token.EOF){
					tokens++;
					if (token == Token.ERROR)
						errors++;
				}
				Console.WriteLine ("Tokenized: " + tokens + " found " + errors + " errors");
			}
			
			return;
		}

		void Parse (ModuleContainer module)
		{
			bool tokenize_only = module.Compiler.Settings.TokenizeOnly;
			var sources = module.Compiler.SourceFiles;

			Location.Initialize (sources);

			var session = new ParserSession {
				UseJayGlobalArrays = true,
				LocatedTokens = new LocatedToken[15000]
			};

			for (int i = 0; i < sources.Count; ++i) {
				if (tokenize_only) {
					tokenize_file (sources[i], module, session);
				} else {
					Parse (sources[i], module, session, Report);
				}
			}
		}

#if false
		void ParseParallel (ModuleContainer module)
		{
			var sources = module.Compiler.SourceFiles;

			Location.Initialize (sources);

			var pcount = Environment.ProcessorCount;
			var threads = new Thread[System.Math.Max (2, pcount - 1)];

			for (int i = 0; i < threads.Length; ++i) {
				var t = new Thread (l => {
					var session = new ParserSession () {
						//UseJayGlobalArrays = true,
					};

					var report = new Report (ctx, Report.Printer); // TODO: Implement flush at once printer

					for (int ii = (int) l; ii < sources.Count; ii += threads.Length) {
						Parse (sources[ii], module, session, report);
					}

					// TODO: Merge warning regions
				});

				t.Start (i);
				threads[i] = t;
			}

			for (int t = 0; t < threads.Length; ++t) {
				threads[t].Join ();
			}
		}
#endif

		public void Parse (SourceFile file, ModuleContainer module, ParserSession session, Report report)
		{
			Stream input;
#if STATIC
			if (_sourceFiles != null)
			{
				if (_sourceFiles.ContainsKey(file.Name))
					input = new MemoryStream(_sourceFiles[file.Name]);
				else
				{
					report.Error(2001, "Source file `{0}' could not be found", file.Name);
					return;
				}
			}
			else
#endif
			try {
				input = File.OpenRead (file.Name);
			} catch {
				report.Error (2001, "Source file `{0}' could not be found", file.Name);
				return;
			}

			// Check 'MZ' header
			if (input.ReadByte () == 77 && input.ReadByte () == 90) {

				report.Error (2015, "Source file `{0}' is a binary file and not a text file", file.Name);
				input.Close ();
				return;
			}

			input.Position = 0;
			SeekableStreamReader reader = new SeekableStreamReader (input, ctx.Settings.Encoding, session.StreamReaderBuffer);

			Parse (reader, file, module, session, report);

			if (ctx.Settings.GenerateDebugInfo && report.Errors == 0 && !file.HasChecksum) {
				input.Position = 0;
				var checksum = session.GetChecksumAlgorithm ();
				file.SetChecksum (checksum.ComputeHash (input));
			}

			reader.Dispose ();
			input.Close ();
		}

		public static void Parse (SeekableStreamReader reader, SourceFile sourceFile, ModuleContainer module, ParserSession session, Report report)
		{
			var file = new CompilationSourceFile (module, sourceFile);
			module.AddTypeContainer (file);

			CSharpParser parser = new CSharpParser (reader, file, report, session);
			parser.parse ();
		}
#if STATIC
		private static string GetLogFileName()
		{
			return string.Format("compiler_{0:dd-MM-yyyy}.txt", DateTime.UtcNow);
		}

		public static void LogMessage(string message)
		{
			_logWriter.WriteLine("[SERVER v{0}] {1}", Version, message);
		}

		private static void GCCleanup()
		{
			System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
			GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
			//GC.Collect();
		}

		private static void Cleanup()
		{
			_consoleOut.Clear();
			Location.Reset();
			Linq.QueryBlock.TransparentParameter.Reset();
			TypeInfo.Reset();
		}

		private static void FullCleanup()
		{
			Cleanup();
			assemblyStream.Close();
			assemblyStream = new MemoryStream();
            _referenceFiles.Clear();
			_sourceFiles.Clear();
			RootContext.ToplevelTypes = null;
			var type = typeof(CSharpParser);
			type.GetField("global_yyVals", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
			type.GetField("global_yyStates", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
			GCCleanup();
		}

		private static void OnMessage(ObjectStreamConnection<CompilerMessage, CompilerMessage> connection, CompilerMessage message)
		{
			if (message == null)
			{
				LogMessage("Connection closed.");
				_exitReceived = true;
				//Environment.Exit(0);
				return;
			}
			LogMessage("Got Message: " + message.Type);
			switch (message.Type)
			{
				case CompilerMessageType.Compile:
					try
					{
						var compilerData = message.Data as CompilerData;
						if (compilerData == null || compilerData.ReferenceFiles == null || compilerData.SourceFiles == null)
						{
							connection.PushMessage(new CompilerMessage {Id = message.Id, Data = "Data does not contain valid CompilerData", Type = CompilerMessageType.Error});
							return;
						}
						var settings = new CompilerSettings
						{
							StdLib = compilerData.StdLib,
							LoadDefaultReferences = compilerData.LoadDefaultReferences,
							OutputFile = compilerData.OutputFile,
							Target = (Target)System.Enum.Parse(typeof(Target), System.Enum.GetName(typeof(CompilerTarget), compilerData.Target)),
							Platform = (Platform)System.Enum.Parse(typeof(Platform), System.Enum.GetName(typeof(CompilerPlatform), compilerData.Platform)),
							Version = (LanguageVersion)System.Enum.Parse(typeof(LanguageVersion), System.Enum.GetName(typeof(CompilerLanguageVersion), compilerData.Version)),
							SdkVersion = compilerData.SdkVersion,
                            GenerateDebugInfo = compilerData.GenerateDebugInfo,
							Optimize = true,
                            Unsafe = compilerData.Unsafe
						};
					    if (compilerData.Defines != null)
					    {
					        foreach (var define in compilerData.Defines)
					        {
					            if (!Tokenizer.IsValidIdentifier(define)) continue;
                                settings.AddConditionalSymbol(define);
                            }
					    }
						foreach (var referenceFile in compilerData.ReferenceFiles)
						{
							settings.AssemblyReferences.Add(referenceFile.Name);
							_referenceFiles[referenceFile.Name] = referenceFile.Data;
						}
						foreach (var sourceFile in compilerData.SourceFiles)
						{
							settings.SourceFiles.Add(new SourceFile(sourceFile.Name, sourceFile.Name, settings.SourceFiles.Count + 1));
							_sourceFiles[sourceFile.Name] = sourceFile.Data;
						}
						var outString = new StringBuilder();
						var tries = 0;
						var files = new HashSet<string>();
						Driver d;
						while (!(d = new Driver(new CompilerContext(settings, new StreamReportPrinter(Console.Out)))).Compile())
						{
							if (settings.FirstSourceFile == null || settings.SourceFiles.Count <= 1 || tries++ > 10)
								break;
							var matches = FileErrorRegex.Matches(_consoleOut.ToString());
							foreach (Match match in matches)
							{
								for (var i = 1; i < match.Groups.Count; i++)
								{
									if (string.IsNullOrWhiteSpace(match.Groups[i].Value)) continue;
									files.Add(match.Groups[i].Value);
								}
							}
							settings.SourceFiles.Clear();
							foreach (var sourceFile in compilerData.SourceFiles)
							{
								if (files.Contains(sourceFile.Name)) continue;
								settings.SourceFiles.Add(new SourceFile(sourceFile.Name, sourceFile.Name, settings.SourceFiles.Count + 1));
							}
							_consoleOut.AppendLine("Warning: restarting compilation");
							outString.Append(_consoleOut);
							Cleanup();
							LogMessage("Restarting: " + outString);
						}
						outString.Append(_consoleOut);
                        connection.PushMessage(new CompilerMessage { Id = message.Id, Data = d.Report.Errors == 0 ? assemblyStream.ToArray() : null, ExtraData = outString.ToString(), Type = CompilerMessageType.Assembly });
						LogMessage("Console: " + _consoleOut);
						FullCleanup();
					}
					catch (Exception e)
					{
						LogMessage("Error: " + e.Message + Environment.NewLine + e.StackTrace);
						connection.PushMessage(new CompilerMessage {Id = message.Id, Data = e.Message + Environment.NewLine + e.StackTrace, Type = CompilerMessageType.Error});
						FullCleanup();
					}
					break;
				case CompilerMessageType.Exit:
					LogMessage("Exit received.");
					_exitReceived = true;
					//Environment.Exit(0);
					break;
				case CompilerMessageType.Ready:
					_ready = true;
					break;
			}
		}

		private static void OnError(Exception exception)
		{
			LogMessage("Error: " + exception.Message + Environment.NewLine + exception.StackTrace);
		}
#endif		
		public static int Main (string[] args)
		{
#if BOOTSTRAP_BASIC
			if (Type.GetType ("Mono.Runtime") != null && Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX)
				for (var i = 0; i < args.Length; i++)
				{
					args[i] = Encoding.UTF8.GetString(Encoding.Unicode.GetBytes(args[i])).TrimEnd();
				}
#endif

			Location.InEmacs = Environment.GetEnvironmentVariable ("EMACS") == "t";

#if STATIC
			if (Array.IndexOf(args, "/service") != -1)
			{
				try
				{
					foreach (var arg in args)
					{
						if (string.IsNullOrWhiteSpace(arg) || !arg.StartsWith("/logPath:")) continue;
						var value = arg.Substring(arg.IndexOf(":") + 1);
						if (Directory.Exists(value)) _logPath = value;
						break;
					}
					var logStream = File.AppendText(Path.Combine(_logPath, GetLogFileName()));
					logStream.AutoFlush = true;
					_logWriter = TextWriter.Synchronized(logStream);
					LogMessage("Started as service");
					_consoleOut = new StringBuilder();
					Console.SetOut(new StringWriter(_consoleOut));
					Console.SetError(new StringWriter(_consoleOut));
					assemblyStream = new MemoryStream();
					_sourceFiles = new Dictionary<string, byte[]>();
					_referenceFiles = new Dictionary<string, byte[]>();
					/*_sourceFiles["__internal__.cs"] = Encoding.Default.GetBytes("class __internal__{}");
					new Driver(new CompilerContext(new CommandLineParser(Console.Out).ParseArguments(new[] { "__internal__.cs", "/noconfig", "/nostdlib", "/optimize", "/t:library" }), new StreamReportPrinter(Console.Out))).Compile();
					FullCleanup();*/
					var server = new ObjectStreamClient<CompilerMessage>(Console.OpenStandardInput(), Console.OpenStandardOutput());
					server.Message += OnMessage;
					server.Error += OnError;
					server.Start();
					LogMessage("Running as service");
					int tries = 0, nextGC = 0;
					while (!_exitReceived && (_shouldRun || _ready))
					{
						_shouldRun = !_ready && tries++ < 30; //wait 30 seconds for bootup
						if (!_ready) server.PushMessage(new CompilerMessage { Type = CompilerMessageType.Ready });
						Thread.Sleep(1000);
						if (nextGC++ < 30) continue;
						GCCleanup();
						nextGC = 0;
					}
					server.Stop();
					LogMessage("Shutdown");
				}
				catch (Exception e)
				{
					LogMessage("Error: " + e.Message + Environment.NewLine + e.StackTrace);
				}
				return 0;
			}
#endif
			CommandLineParser cmd = new CommandLineParser (Console.Out);
			var settings = cmd.ParseArguments (args);
			if (settings == null)
				return 1;

			if (cmd.HasBeenStopped)
				return 0;

			Driver d = new Driver (new CompilerContext (settings, new ConsoleReportPrinter ()));

			if (d.Compile () && d.Report.Errors == 0) {
				if (d.Report.Warnings > 0) {
					Console.WriteLine ("Compilation succeeded - {0} warning(s)", d.Report.Warnings);
				}
				Environment.Exit (0);
				return 0;
			}
			
			
			Console.WriteLine("Compilation failed: {0} error(s), {1} warnings",
				d.Report.Errors, d.Report.Warnings);
			Environment.Exit (1);
			return 1;
		}

		public static string GetPackageFlags (string packages, Report report)
		{
#if MONO_FEATURE_PROCESS_START
			ProcessStartInfo pi = new ProcessStartInfo ();
			pi.FileName = "pkg-config";
			pi.RedirectStandardOutput = true;
			pi.UseShellExecute = false;
			pi.Arguments = "--libs " + packages;
			Process p = null;
			try {
				p = Process.Start (pi);
			} catch (Exception e) {
				if (report == null)
					throw;

				report.Error (-27, "Couldn't run pkg-config: " + e.Message);
				return null;
			}
			
			if (p.StandardOutput == null) {
				if (report == null)
					throw new ApplicationException ("Specified package did not return any information");

				report.Warning (-27, 1, "Specified package did not return any information");
				p.Close ();
				return null;
			}

			string pkgout = p.StandardOutput.ReadToEnd ();
			p.WaitForExit ();
			if (p.ExitCode != 0) {
				if (report == null)
					throw new ApplicationException (pkgout);

				report.Error (-27, "Error running pkg-config. Check the above output.");
				p.Close ();
				return null;
			}

			p.Close ();
			return pkgout;
#else
			throw new NotSupportedException ("Process.Start is not supported on this platform.");
#endif // MONO_FEATURE_PROCESS_START
		}

		//
		// Main compilation method
		//
		public bool Compile ()
		{
			var settings = ctx.Settings;

			//
			// If we are an exe, require a source file for the entry point or
			// if there is nothing to put in the assembly, and we are not a library
			//
			if (settings.FirstSourceFile == null &&
				((settings.Target == Target.Exe || settings.Target == Target.WinExe || settings.Target == Target.Module) ||
				settings.Resources == null)) {
				Report.Error (2008, "No files to compile were specified");
				return false;
			}

			if (settings.Platform == Platform.AnyCPU32Preferred && (settings.Target == Target.Library || settings.Target == Target.Module)) {
				Report.Error (4023, "Platform option `anycpu32bitpreferred' is valid only for executables");
				return false;
			}

			TimeReporter tr = new TimeReporter (settings.Timestamps);
			ctx.TimeReporter = tr;
			tr.StartTotal ();

			var module = new ModuleContainer (ctx);
			RootContext.ToplevelTypes = module;

			tr.Start (TimeReporter.TimerType.ParseTotal);
			Parse (module);
			tr.Stop (TimeReporter.TimerType.ParseTotal);

			if (Report.Errors > 0)
				return false;

			if (settings.TokenizeOnly || settings.ParseOnly) {
				tr.StopTotal ();
				tr.ShowStats ();
				return true;
			}

			var output_file = settings.OutputFile;
			string output_file_name;
			if (output_file == null) {
				var source_file = settings.FirstSourceFile;

				if (source_file == null) {
					Report.Error (1562, "If no source files are specified you must specify the output file with -out:");
					return false;
				}

				output_file_name = source_file.Name;
				int pos = output_file_name.LastIndexOf ('.');

				if (pos > 0)
					output_file_name = output_file_name.Substring (0, pos);
				
				output_file_name += settings.TargetExt;
				output_file = output_file_name;
			} else {
				output_file_name = Path.GetFileName (output_file);

				if (string.IsNullOrEmpty (Path.GetFileNameWithoutExtension (output_file_name)) ||
					output_file_name.IndexOfAny (Path.GetInvalidFileNameChars ()) >= 0) {
					Report.Error (2021, "Output file name is not valid");
					return false;
				}
			}

#if STATIC
			var importer = new StaticImporter (module);
			var references_loader = new StaticLoader (importer, ctx, _referenceFiles);

			tr.Start (TimeReporter.TimerType.AssemblyBuilderSetup);
			var assembly = new AssemblyDefinitionStatic (module, references_loader, output_file_name, output_file);
			assembly.Create (references_loader.Domain);
			tr.Stop (TimeReporter.TimerType.AssemblyBuilderSetup);

			// Create compiler types first even before any referenced
			// assembly is loaded to allow forward referenced types from
			// loaded assembly into compiled builder to be resolved
			// correctly
			tr.Start (TimeReporter.TimerType.CreateTypeTotal);
			module.CreateContainer ();
			importer.AddCompiledAssembly (assembly);
			references_loader.CompiledAssembly = assembly;
			tr.Stop (TimeReporter.TimerType.CreateTypeTotal);

			references_loader.LoadReferences (module);

			tr.Start (TimeReporter.TimerType.PredefinedTypesInit);
			if (!ctx.BuiltinTypes.CheckDefinitions (module))
				return false;

			tr.Stop (TimeReporter.TimerType.PredefinedTypesInit);

			references_loader.LoadModules (assembly, module.GlobalRootNamespace);
#else
			var assembly = new AssemblyDefinitionDynamic (module, output_file_name, output_file);
			module.SetDeclaringAssembly (assembly);

			var importer = new ReflectionImporter (module, ctx.BuiltinTypes);
			assembly.Importer = importer;

			var loader = new DynamicLoader (importer, ctx);
			loader.LoadReferences (module);

			if (!ctx.BuiltinTypes.CheckDefinitions (module))
				return false;

			if (!assembly.Create (AppDomain.CurrentDomain, AssemblyBuilderAccess.Save))
				return false;

			module.CreateContainer ();

			loader.LoadModules (assembly, module.GlobalRootNamespace);
#endif
			module.InitializePredefinedTypes ();

			if (settings.GetResourceStrings != null)
				module.LoadGetResourceStrings (settings.GetResourceStrings);

			tr.Start (TimeReporter.TimerType.ModuleDefinitionTotal);
			module.Define ();
			tr.Stop (TimeReporter.TimerType.ModuleDefinitionTotal);

			if (Report.Errors > 0)
				return false;

			if (settings.DocumentationFile != null) {
				var doc = new DocumentationBuilder (module);
				doc.OutputDocComment (output_file, settings.DocumentationFile);
			}

			assembly.Resolve ();
			
			if (Report.Errors > 0)
				return false;


			tr.Start (TimeReporter.TimerType.EmitTotal);
			assembly.Emit ();
			tr.Stop (TimeReporter.TimerType.EmitTotal);

			if (Report.Errors > 0){
				return false;
			}

			tr.Start (TimeReporter.TimerType.CloseTypes);
			module.CloseContainer ();
			tr.Stop (TimeReporter.TimerType.CloseTypes);

			tr.Start (TimeReporter.TimerType.Resouces);
			if (!settings.WriteMetadataOnly)
				assembly.EmbedResources ();
			tr.Stop (TimeReporter.TimerType.Resouces);

			if (Report.Errors > 0)
				return false;
#if STATIC
			if (assemblyStream != null)
				assemblyStream.SetLength(0);
			assembly.Save(assemblyStream);
#else
			assembly.Save();
#endif

#if STATIC
			references_loader.Dispose ();
#endif
			tr.StopTotal ();
			tr.ShowStats ();

			return Report.Errors == 0;
		}
	}

	//
	// This is the only public entry point
	//
	public class CompilerCallableEntryPoint : MarshalByRefObject {
		public static bool InvokeCompiler (string [] args, TextWriter error)
		{
			try {
				CommandLineParser cmd = new CommandLineParser (error);
				var setting = cmd.ParseArguments (args);
				if (setting == null)
					return false;

				var d = new Driver (new CompilerContext (setting, new StreamReportPrinter (error)));
				return d.Compile ();
			} finally {
				Reset ();
			}
		}

		public static int[] AllWarningNumbers {
			get {
				return Report.AllWarnings;
			}
		}

		public static void Reset ()
		{
			Reset (true);
		}

		public static void PartialReset ()
		{
			Reset (false);
		}
		
		public static void Reset (bool full_flag)
		{
			Location.Reset ();
			
			if (!full_flag)
				return;

			Linq.QueryBlock.TransparentParameter.Reset ();
			TypeInfo.Reset ();
		}
	}
}
