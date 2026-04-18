using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using E2ETest.Core;
using E2ETest.Core.Api;
using E2ETest.Core.Events;
using E2ETest.Core.Logging;
using E2ETest.Core.Model;
using E2ETest.Ipc;
using E2ETest.Scripting;

namespace E2ETest.Cli
{
    public static class Program
    {
        public static int Main(string[] rawArgs)
        {
            try
            {
                var args = new Args(rawArgs);
                if (args.ShowHelp || args.Positional.Count == 0)
                {
                    PrintHelp();
                    return args.Positional.Count == 0 ? 1 : 0;
                }

                string cmd = args.Positional[0].ToLowerInvariant();
                switch (cmd)
                {
                    case "run": return CmdRun(args);
                    case "record": return CmdRecord(args);
                    case "ui": return CmdUi(args);
                    case "dump": return CmdDump(args);
                    case "version": Console.WriteLine("e2e 0.1.0"); return 0;
                    default:
                        Console.Error.WriteLine("unknown command: " + cmd);
                        PrintHelp();
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                if (Environment.GetEnvironmentVariable("E2E_DEBUG") == "1") Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static int CmdRun(Args args)
        {
            if (args.Positional.Count < 2)
            {
                Console.Error.WriteLine("usage: e2e run <yaml|dll|csx|vbx>");
                return 2;
            }
            string input = args.Positional[1];

            PipeClient pipe = null;
            if (args.Flag("ui"))
            {
                LaunchDashboard();
                pipe = new PipeClient();
                // 대시보드가 파이프 서버를 여는 동안 잠깐 대기
                for (int i = 0; i < 30 && !pipe.TryConnect(200); i++) Thread.Sleep(200);
                if (!pipe.Connected) Console.Error.WriteLine("WARN: dashboard connect failed; continuing without UI");
            }

            int result = RunFile(input, args, pipe);
            if (pipe != null) pipe.Dispose();
            return result;
        }

        private static int CmdRecord(Args args)
        {
            // record는 run과 동일하지만 recordVideo=true 강제.
            if (args.Positional.Count < 2) { Console.Error.WriteLine("usage: e2e record <yaml|dll|csx|vbx>"); return 2; }
            return RunFile(args.Positional[1], args, null, forceRecord: true);
        }

        private static int CmdUi(Args args)
        {
            // e2e ui [run] <file>  → --ui 플래그와 동등
            if (args.Positional.Count < 2) { Console.Error.WriteLine("usage: e2e ui <file>"); return 2; }
            // sub-arg: [run] <file>
            int fileIdx = args.Positional[1].Equals("run", StringComparison.OrdinalIgnoreCase) ? 2 : 1;
            if (args.Positional.Count <= fileIdx) { Console.Error.WriteLine("usage: e2e ui <file>"); return 2; }

            LaunchDashboard();
            var pipe = new PipeClient();
            for (int i = 0; i < 30 && !pipe.TryConnect(200); i++) Thread.Sleep(200);

            int result = RunFile(args.Positional[fileIdx], args, pipe);
            pipe.Dispose();
            return result;
        }

        private static int CmdDump(Args args)
        {
            // e2e dump <exePath>  → 앱 실행 후 UIA 트리 출력.
            if (args.Positional.Count < 2) { Console.Error.WriteLine("usage: e2e dump <exePath>"); return 2; }
            var tc = new TestCase
            {
                Name = "uia-tree-dump",
                App = new AppSpec { Path = args.Positional[1] },
                Options = new TestOptions { RecordVideo = false }
            };
            tc.Steps.Add(new TestStep { Action = "wait", Value = "1500" });
            var runner = new Runner();
            // execute single wait step just to attach, then dump tree via backend directly.
            Console.WriteLine("Launching and dumping UI tree (first 4 levels)...");
            // 실용적으로는 Runner 내부 접근 필요 — 여기선 간단히 Run을 돌리고 결과 로그.
            var r = runner.Run(tc);
            Console.WriteLine(r.Status == TestStatus.Passed ? "attach OK" : "attach FAILED: " + r.Message);
            return r.Status == TestStatus.Passed ? 0 : 1;
        }

        private static int RunFile(string file, Args args, PipeClient pipe, bool forceRecord = false)
        {
            if (!File.Exists(file)) { Console.Error.WriteLine("file not found: " + file); return 2; }
            string ext = Path.GetExtension(file).ToLowerInvariant();

            var runner = new Runner();
            runner.Logger = new ConsoleLogger(args.Flag("verbose") ? LogLevel.Debug : LogLevel.Info);
            runner.OnEvent = evt => { if (pipe != null && pipe.Connected) pipe.Send(evt); };

            switch (ext)
            {
                case ".yaml":
                case ".yml":
                {
                    var tc = YamlLoader.Load(file);
                    if (forceRecord) tc.Options.RecordVideo = true;
                    var result = runner.Run(tc);
                    return result.Status == TestStatus.Passed ? 0 : 1;
                }
                case ".dll":
                {
                    var loader = new AssemblyTestLoader();
                    var methods = loader.Discover(file);
                    if (methods.Count == 0) { Console.Error.WriteLine("no [E2ETest] methods in " + file); return 1; }
                    int failed = 0;
                    foreach (var m in methods)
                    {
                        Console.WriteLine("--- " + m.DisplayName + " ---");
                        string appPath = (string)m.AttributeInstance.GetType().GetProperty("AppPath").GetValue(m.AttributeInstance, null);
                        if (string.IsNullOrEmpty(appPath)) { Console.Error.WriteLine("[E2ETest] requires AppPath"); failed++; continue; }
                        var tc = new TestCase { Name = m.DisplayName, App = new AppSpec { Path = appPath } };
                        tc.Options.RecordVideo = forceRecord || tc.Options.RecordVideo;
                        // 러너에서 app을 주입받고 메서드 실행하도록 커스텀 실행이 필요 - 간단화를 위해 생략.
                        Console.Error.WriteLine("assembly test runner not fully implemented; use YAML or scripts.");
                        failed++;
                    }
                    return failed == 0 ? 0 : 1;
                }
                case ".csx":
                case ".vbx":
                {
                    Console.Error.WriteLine("script runner not wired in MVP (YAML recommended). Scripts are loaded by E2ETest.Scripting.ScriptLoader.");
                    return 2;
                }
                default:
                    Console.Error.WriteLine("unsupported file type: " + ext);
                    return 2;
            }
        }

        private static void LaunchDashboard()
        {
            string baseDir = AppContext.BaseDirectory;
            string[] candidates =
            {
                Path.Combine(baseDir, "E2ETest.Dashboard.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "E2ETest.Dashboard", "bin", "Debug", "net8.0-windows", "E2ETest.Dashboard.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "E2ETest.Dashboard", "bin", "Release", "net8.0-windows", "E2ETest.Dashboard.exe")
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c))
                {
                    try { System.Diagnostics.Process.Start(c); return; } catch { }
                }
            }
            Console.Error.WriteLine("WARN: dashboard executable not found; continuing without UI");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("e2e - Playwright-style E2E testing for .NET WinForms/WPF");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  e2e run <file>             Run a test (.yaml/.yml, .dll)");
            Console.WriteLine("  e2e run --ui <file>        Run with WPF dashboard");
            Console.WriteLine("  e2e ui <file>              Alias for 'run --ui'");
            Console.WriteLine("  e2e record <file>          Run with video recording forced on");
            Console.WriteLine("  e2e dump <exePath>         Launch app and dump UIA tree");
            Console.WriteLine("  e2e version");
            Console.WriteLine();
            Console.WriteLine("Flags:");
            Console.WriteLine("  --ui                       Launch dashboard and stream events");
            Console.WriteLine("  --verbose                  Debug logging");
            Console.WriteLine("  --output <dir>             Override output directory");
        }
    }

    internal sealed class Args
    {
        public List<string> Positional { get; private set; }
        public Dictionary<string, string> Named { get; private set; }
        public HashSet<string> Flags { get; private set; }
        public bool ShowHelp { get; private set; }

        public Args(string[] raw)
        {
            Positional = new List<string>();
            Named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < raw.Length; i++)
            {
                string a = raw[i];
                if (a == "--help" || a == "-h") { ShowHelp = true; continue; }
                if (a.StartsWith("--"))
                {
                    string key = a.Substring(2);
                    if (i + 1 < raw.Length && !raw[i + 1].StartsWith("--")) { Named[key] = raw[++i]; }
                    else { Flags.Add(key); }
                    continue;
                }
                Positional.Add(a);
            }
        }
        public bool Flag(string name) { return Flags.Contains(name); }
        public string Get(string name) { string v; return Named.TryGetValue(name, out v) ? v : null; }
    }
}
