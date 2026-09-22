#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;

namespace Landsong.ECS.Editor.Tests
{
    /// <summary>
    /// NUnit ownership layer for the deterministic editor verification suites.
    /// The existing Run methods remain temporarily as compatibility entry points while
    /// their internal custom checks are migrated to focused NUnit fixtures over time.
    /// </summary>
    [TestFixture]
    [Category("Landsong")]
    public sealed class LandsongEditModeVerificationTests
    {
        public static IEnumerable SuiteCases
        {
            get
            {
                foreach (var suite in ProjectVerification.Suites)
                    yield return new TestCaseData(suite.Id)
                        .SetName(suite.DisplayName)
                        .SetCategory(suite.Category);
            }
        }

        [Test]
        [Order(0)]
        [Category("内容")]
        public void 当前内容基础校验()
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False, "请先退出 Play Mode。");
            Assert.DoesNotThrow(ContentValidation.Validate);
        }

        [TestCaseSource(nameof(SuiteCases))]
        [Order(1)]
        public void 领域逻辑验证(string suiteId)
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False, "请先退出 Play Mode。");

            string result = ProjectVerification.GetSuite(suiteId).Run();
            int assertions = ProjectVerification.AssertionCount(result);
            Assert.That(assertions, Is.GreaterThan(0), "验证套件必须执行至少一条断言。");
            TestContext.Progress.WriteLine($"{suiteId}: {assertions} assertions");
        }
    }
}
#endif
