using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace ChefShow.Tests
{
    // Запуск существующих тестов в уже открытом Editor без второго Unity-процесса.
    [InitializeOnLoad]
    public static class LayoutVerificationRunner
    {
        private const string ResultModeKey = "ChefShow.LayoutCheckMode";
        private static readonly TestRunnerApi Api;
        static LayoutVerificationRunner()
        {
            Api = ScriptableObject.CreateInstance<TestRunnerApi>();
            Api.RegisterCallbacks(new ResultWriter());
        }

        [MenuItem("Tools/Chef Show/Checks/Run EditMode Checks")]
        public static void RunEditMode() => Run(TestMode.EditMode, "editmode");
        [MenuItem("Tools/Chef Show/Checks/Run PlayMode Checks")]
        public static void RunPlayMode() => Run(TestMode.PlayMode, "playmode");

        private static void Run(TestMode mode, string name)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new System.InvalidOperationException("Дождитесь остановки Play Mode и компиляции.");
            SessionState.SetString(ResultModeKey, name);
            Api.Execute(new ExecutionSettings(new Filter {
                testMode = mode, assemblyNames = new[] { "ChefShow.Tests." + (mode == TestMode.EditMode ? "EditMode" : "PlayMode") }
            }));
        }

        private sealed class ResultWriter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string mode = SessionState.GetString(ResultModeKey, "");
                if (string.IsNullOrEmpty(mode)) return;
                Directory.CreateDirectory("TestResults");
                TestRunnerApi.SaveResultToFile(result, "TestResults/layout-" + mode + ".xml");
                SessionState.SetString(ResultModeKey, "");
                Debug.Log("Chef Show layout " + mode + ": " + result.ResultState);
            }
        }
    }
}
