using System;
using System.Collections.Generic;

namespace Landsong.Localization
{
    public static class LocalizationTables
    {
        public const string Ui = "UI";
        public const string Content = "Content";
        public const string Gameplay = "Gameplay";

        public static readonly string[] All =
        {
            Ui,
            Content,
            Gameplay
        };

        public static bool IsKnown(string tableName)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], tableName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    public enum LanguageOptionSource
    {
        BuiltIn = 0,
        ExternalDirectory = 1
    }

    public enum LocalizationDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    [Serializable]
    public sealed class LanguagePackManifest
    {
        public int schemaVersion = 1;
        public string packId = string.Empty;
        public string displayName = string.Empty;
        public string localeCode = string.Empty;
        public string fallbackLocaleCode = "en";
        public string author = string.Empty;
        public string description = string.Empty;
        public int targetKeysetVersion = 1;
    }

    [Serializable]
    public sealed class LocalizationDiagnostic
    {
        public LocalizationDiagnosticSeverity Severity;
        public string Code = string.Empty;
        public string Message = string.Empty;
        public int Line;

        public bool IsError => Severity == LocalizationDiagnosticSeverity.Error;

        public static LocalizationDiagnostic Error(string code, string message, int line = 0)
        {
            return Create(LocalizationDiagnosticSeverity.Error, code, message, line);
        }

        public static LocalizationDiagnostic Warning(string code, string message, int line = 0)
        {
            return Create(LocalizationDiagnosticSeverity.Warning, code, message, line);
        }

        public static LocalizationDiagnostic Info(string code, string message, int line = 0)
        {
            return Create(LocalizationDiagnosticSeverity.Info, code, message, line);
        }

        private static LocalizationDiagnostic Create(
            LocalizationDiagnosticSeverity severity,
            string code,
            string message,
            int line)
        {
            return new LocalizationDiagnostic
            {
                Severity = severity,
                Code = code ?? string.Empty,
                Message = message ?? string.Empty,
                Line = Math.Max(0, line)
            };
        }
    }

    [Serializable]
    public sealed class ExternalLanguageTextEntry
    {
        public string TableName = string.Empty;
        public string Key = string.Empty;
        public string Text = string.Empty;
        public int SourceLine;
    }

    [Serializable]
    public sealed class LanguagePackInfo
    {
        public string OptionId = string.Empty;
        public string PackId = string.Empty;
        public string LocaleCode = string.Empty;
        public string FallbackLocaleCode = string.Empty;
        public string DisplayName = string.Empty;
        public string RootPath = string.Empty;
        public string Author = string.Empty;
        public string Description = string.Empty;
        public int SchemaVersion;
        public int TargetKeysetVersion;
        public int TranslatedEntryCount;
        public int MissingKeyCount;
        public int UnknownKeyCount;
        public LanguageOptionSource Source;
        public bool IsValid = true;
        public List<LocalizationDiagnostic> Diagnostics = new List<LocalizationDiagnostic>();

        public bool HasErrors
        {
            get
            {
                if (Diagnostics == null)
                {
                    return false;
                }

                for (var i = 0; i < Diagnostics.Count; i++)
                {
                    if (Diagnostics[i] != null && Diagnostics[i].IsError)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public LanguagePackInfo Clone()
        {
            return new LanguagePackInfo
            {
                OptionId = OptionId ?? string.Empty,
                PackId = PackId ?? string.Empty,
                LocaleCode = LocaleCode ?? string.Empty,
                FallbackLocaleCode = FallbackLocaleCode ?? string.Empty,
                DisplayName = DisplayName ?? string.Empty,
                RootPath = RootPath ?? string.Empty,
                Author = Author ?? string.Empty,
                Description = Description ?? string.Empty,
                SchemaVersion = SchemaVersion,
                TargetKeysetVersion = TargetKeysetVersion,
                TranslatedEntryCount = TranslatedEntryCount,
                MissingKeyCount = MissingKeyCount,
                UnknownKeyCount = UnknownKeyCount,
                Source = Source,
                IsValid = IsValid,
                Diagnostics = Diagnostics == null
                    ? new List<LocalizationDiagnostic>()
                    : new List<LocalizationDiagnostic>(Diagnostics)
            };
        }

        public static LanguagePackInfo BuiltIn(string localeCode, string displayName)
        {
            var optionId = $"builtin.{localeCode}";
            return new LanguagePackInfo
            {
                OptionId = optionId,
                PackId = optionId,
                LocaleCode = localeCode,
                FallbackLocaleCode = string.Empty,
                DisplayName = displayName,
                Source = LanguageOptionSource.BuiltIn,
                SchemaVersion = 1,
                TargetKeysetVersion = LocalizationKeyset.CurrentVersion,
                IsValid = true
            };
        }
    }

    public sealed class LanguagePackReadResult
    {
        public LanguagePackInfo Info;
        public List<ExternalLanguageTextEntry> Entries = new List<ExternalLanguageTextEntry>();
        public bool Success => Info != null && Info.IsValid && !Info.HasErrors;
    }

    public static class LocalizationKeyset
    {
        public const int CurrentVersion = 1;
    }
}
