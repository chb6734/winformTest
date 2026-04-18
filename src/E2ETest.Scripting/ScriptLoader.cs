using System;
using System.IO;
using System.Threading.Tasks;
using E2ETest.Core.Api;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace E2ETest.Scripting
{
    /// <summary>C# 스크립트 파일(.csx)을 Roslyn으로 동적 컴파일 후 실행. 스크립트는 'app' 글로벌 변수로 IApp 인스턴스를 받음.
    /// VB 스크립팅은 Microsoft가 최신 버전 NuGet을 제공하지 않아 미지원. VB로 테스트하려면 VB 테스트 어셈블리([E2ETest] 속성) 사용.</summary>
    public static class ScriptLoader
    {
        public sealed class ScriptGlobals
        {
            public IApp app { get; set; }
        }

        public static async Task RunCSharpAsync(string scriptPath, IApp app)
        {
            string code = File.ReadAllText(scriptPath);
            var options = ScriptOptions.Default
                .WithReferences(typeof(IApp).Assembly)
                .WithImports("System", "E2ETest.Core.Api", "E2ETest.Core.Model");
            await CSharpScript.RunAsync(code, options, new ScriptGlobals { app = app });
        }

        public static Task RunVbAsync(string scriptPath, IApp app)
        {
            throw new NotSupportedException(
                "VB 스크립트 직접 실행(.vbx)은 지원하지 않습니다. VB로 테스트하려면 VB 클래스 라이브러리를 만들어 [E2ETest] 속성이 붙은 메서드를 정의한 뒤 'e2e run My.Tests.dll' 로 실행하세요. C# 스크립트(.csx)는 지원됩니다.");
        }
    }
}
