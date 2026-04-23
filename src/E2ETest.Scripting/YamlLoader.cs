using System;
using System.Collections.Generic;
using System.IO;
using E2ETest.Core.Model;
using YamlDotNet.RepresentationModel;

namespace E2ETest.Scripting
{
    /// <summary>YAML 테스트 파일을 TestCase로 로드.</summary>
    public static class YamlLoader
    {
        public static TestCase Load(string yamlFilePath)
        {
            string fullPath = Path.GetFullPath(yamlFilePath);
            string baseDir = Path.GetDirectoryName(fullPath);
            using (var sr = new StreamReader(fullPath))
            {
                var tc = LoadFromReader(sr, fullPath);
                tc.SourcePath = fullPath;
                // 상대 경로를 YAML 파일 기준으로 절대 경로화
                if (tc.App != null && !string.IsNullOrEmpty(tc.App.Path) && !Path.IsPathRooted(tc.App.Path))
                {
                    tc.App.Path = Path.GetFullPath(Path.Combine(baseDir, tc.App.Path));
                }
                if (tc.App != null && !string.IsNullOrEmpty(tc.App.WorkingDirectory) && !Path.IsPathRooted(tc.App.WorkingDirectory))
                {
                    tc.App.WorkingDirectory = Path.GetFullPath(Path.Combine(baseDir, tc.App.WorkingDirectory));
                }
                if (tc.Options != null && !string.IsNullOrEmpty(tc.Options.OutputDirectory) && !Path.IsPathRooted(tc.Options.OutputDirectory))
                {
                    tc.Options.OutputDirectory = Path.GetFullPath(Path.Combine(baseDir, tc.Options.OutputDirectory));
                }
                return tc;
            }
        }

        public static TestCase LoadFromString(string yamlContent, string sourceLabel = "<inline>")
        {
            using (var sr = new StringReader(yamlContent))
            {
                return LoadFromReader(sr, sourceLabel);
            }
        }

        private static TestCase LoadFromReader(TextReader reader, string source)
        {
            var stream = new YamlStream();
            stream.Load(reader);
            if (stream.Documents.Count == 0) throw new FormatException(source + ": empty yaml");
            var root = (YamlMappingNode)stream.Documents[0].RootNode;

            var tc = new TestCase();
            tc.Name = GetString(root, "name") ?? Path.GetFileNameWithoutExtension(source);

            var appNode = GetMapping(root, "app");
            if (appNode == null) throw new FormatException(source + ": 'app' section is required");
            tc.App = new AppSpec
            {
                Path = GetString(appNode, "path"),
                Args = GetString(appNode, "args"),
                WorkingDirectory = GetString(appNode, "workingDirectory"),
                MainWindowTitle = GetString(appNode, "mainWindowTitle")
            };
            var startup = GetString(appNode, "startupTimeoutMs");
            if (!string.IsNullOrEmpty(startup)) { int v; if (int.TryParse(startup, out v)) tc.App.StartupTimeoutMs = v; }

            var opts = GetMapping(root, "options");
            if (opts != null)
            {
                var dtm = GetString(opts, "defaultTimeoutMs");
                if (!string.IsNullOrEmpty(dtm)) { int v; if (int.TryParse(dtm, out v)) tc.Options.DefaultTimeoutMs = v; }
                var rv = GetString(opts, "recordVideo");
                if (!string.IsNullOrEmpty(rv)) tc.Options.RecordVideo = rv.Equals("true", StringComparison.OrdinalIgnoreCase);
                tc.Options.OutputDirectory = GetString(opts, "outputDirectory") ?? tc.Options.OutputDirectory;
            }

            var steps = GetSequence(root, "steps");
            if (steps == null) throw new FormatException(source + ": 'steps' section is required");
            foreach (var step in steps.Children)
            {
                var m = (YamlMappingNode)step;
                var s = new TestStep
                {
                    Action = GetString(m, "action"),
                    Target = GetString(m, "target"),
                    Value = GetString(m, "value"),
                    Name = GetString(m, "name"),
                    Description = GetString(m, "description")
                };
                var to = GetString(m, "timeoutMs");
                if (!string.IsNullOrEmpty(to))
                {
                    int t; if (int.TryParse(to, out t)) s.TimeoutMs = t;
                }
                tc.Steps.Add(s);
            }
            return tc;
        }

        private static YamlNode TryGet(YamlMappingNode node, string key)
        {
            var keyNode = new YamlScalarNode(key);
            foreach (var kv in node.Children)
            {
                var sk = kv.Key as YamlScalarNode;
                if (sk != null && sk.Value == key) return kv.Value;
            }
            return null;
        }

        private static string GetString(YamlMappingNode node, string key)
        {
            var v = TryGet(node, key) as YamlScalarNode;
            return v != null ? v.Value : null;
        }

        private static YamlMappingNode GetMapping(YamlMappingNode node, string key)
        {
            return TryGet(node, key) as YamlMappingNode;
        }

        private static YamlSequenceNode GetSequence(YamlMappingNode node, string key)
        {
            return TryGet(node, key) as YamlSequenceNode;
        }
    }
}
