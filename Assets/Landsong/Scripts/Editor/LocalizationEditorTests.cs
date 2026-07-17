using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.Localization;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Localization.Tests
{
    public static class LocalizationEditorTests
    {
        [MenuItem("Landsong/Localization/Run Automated Tests")]
        public static void RunAutomatedTests()
        {
            var temporaryRoot = Path.Combine(
                Path.GetTempPath(),
                "LandsongLocalizationTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryRoot);

            try
            {
                TestQuotedMultilineCsv(temporaryRoot);
                TestDuplicateKeyRejected(temporaryRoot);
                TestInvalidUtf8Rejected(temporaryRoot);
                TestUnicodeStableIds();
                Debug.Log("本地化自动化测试通过：4/4。 ");
            }
            finally
            {
                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, true);
                }
            }
        }

        private static void TestQuotedMultilineCsv(string root)
        {
            ResetPack(root, "test.multiline");
            File.WriteAllText(
                Path.Combine(root, "strings.csv"),
                "Table,Key,Text\nUI,ui.test.multiline,\" first line\nsecond, \"\"quoted\"\" line \"\n",
                new UTF8Encoding(false));

            var result = new ExternalLanguagePackRepository().Read(root);
            Require(result.Success, "多行 CSV 解析失败：" + JoinDiagnostics(result));
            Require(result.Entries.Count == 1, "多行 CSV 表项数量错误。 ");
            Require(
                result.Entries[0].Text == " first line\nsecond, \"quoted\" line ",
                "多行 CSV 文本的空白或引号未原样保留。 ");
        }

        private static void TestDuplicateKeyRejected(string root)
        {
            ResetPack(root, "test.duplicate");
            File.WriteAllText(
                Path.Combine(root, "strings.csv"),
                "Table,Key,Text\nUI,ui.test.duplicate,One\nUI,ui.test.duplicate,Two\n",
                new UTF8Encoding(false));

            var result = new ExternalLanguagePackRepository().Read(root);
            Require(!result.Success, "重复 Table/Key 未被拒绝。 ");
            Require(
                result.Info.Diagnostics.Any(item => item.Code == "pack.key_duplicate"),
                "重复 Table/Key 未产生预期诊断。 ");
        }

        private static void TestInvalidUtf8Rejected(string root)
        {
            ResetPack(root, "test.invalid_utf8");
            File.WriteAllBytes(
                Path.Combine(root, "strings.csv"),
                new byte[] { 0x54, 0x61, 0x62, 0x6c, 0x65, 0xff });

            var result = new ExternalLanguagePackRepository().Read(root);
            Require(!result.Success, "非法 UTF-8 未被拒绝。 ");
            Require(
                result.Info.Diagnostics.Any(item => item.Code == "pack.strings_read_failed"),
                "非法 UTF-8 未产生预期诊断。 ");
        }

        private static void TestUnicodeStableIds()
        {
            var wheat = Landsong.Localization.L10n.NormalizeKeyPart("小麦");
            var gold = Landsong.Localization.L10n.NormalizeKeyPart("金币");
            Require(wheat != gold, "不同 Unicode 标识生成了相同 Key。 ");
            Require(IsAsciiKeyPart(wheat) && IsAsciiKeyPart(gold), "Unicode 标识未转换为 ASCII Key。 ");
        }

        private static void ResetPack(string root, string packId)
        {
            var stringsPath = Path.Combine(root, "strings.csv");
            if (File.Exists(stringsPath))
            {
                File.Delete(stringsPath);
            }

            File.WriteAllText(
                Path.Combine(root, "manifest.json"),
                "{\"schemaVersion\":1,\"packId\":\"" + packId +
                "\",\"displayName\":\"Test\",\"localeCode\":\"fr-FR\",\"fallbackLocaleCode\":\"en\",\"targetKeysetVersion\":" +
                LocalizationKeyset.CurrentVersion + "}",
                new UTF8Encoding(false));
        }

        private static bool IsAsciiKeyPart(string value)
        {
            return !string.IsNullOrEmpty(value)
                   && value.All(character => character is >= 'a' and <= 'z'
                                             or >= '0' and <= '9'
                                             or '_');
        }

        private static string JoinDiagnostics(LanguagePackReadResult result)
        {
            return string.Join("; ", result.Info.Diagnostics.Select(item => $"{item.Code}: {item.Message}"));
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
