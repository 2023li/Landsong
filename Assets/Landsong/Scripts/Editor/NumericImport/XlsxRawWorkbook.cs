using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Landsong.EditorTools.NumericImport
{
    internal sealed class XlsxRawWorkbook
    {
        private static readonly XNamespace SpreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        private readonly Dictionary<string, XlsxRawSheet> sheets =
            new Dictionary<string, XlsxRawSheet>(StringComparer.Ordinal);

        public static XlsxRawWorkbook Load(string path)
        {
            using var file = File.OpenRead(path);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read, false);
            var sharedStrings = ReadSharedStrings(archive);
            var workbookDocument = LoadXml(archive, "xl/workbook.xml");
            var relationsDocument = LoadXml(archive, "xl/_rels/workbook.xml.rels");
            var targets = relationsDocument.Root?
                .Elements(PackageRelationshipNamespace + "Relationship")
                .Where(element => element.Attribute("Id") != null
                                  && element.Attribute("Target") != null)
                .ToDictionary(
                    element => element.Attribute("Id")?.Value ?? string.Empty,
                    element => NormalizeEntryPath(
                        "xl/",
                        element.Attribute("Target")?.Value ?? string.Empty),
                    StringComparer.Ordinal)
                ?? new Dictionary<string, string>(StringComparer.Ordinal);

            var result = new XlsxRawWorkbook();
            foreach (var element in workbookDocument.Descendants(
                         SpreadsheetNamespace + "sheet"))
            {
                var name = element.Attribute("name")?.Value ?? string.Empty;
                var relationId =
                    element.Attribute(RelationshipNamespace + "id")?.Value
                    ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)
                    || !targets.TryGetValue(relationId, out var target)
                    || archive.GetEntry(target) == null)
                {
                    continue;
                }

                result.sheets[name] = ReadSheet(
                    archive,
                    target,
                    sharedStrings);
            }

            return result;
        }

        public bool TryGetSheet(string name, out XlsxRawSheet sheet) =>
            sheets.TryGetValue(name, out sheet);

        public string GetCell(string sheet, int row, int column) =>
            sheets.TryGetValue(sheet, out var value)
                ? value.GetCell(row, column)
                : string.Empty;

        private static XlsxRawSheet ReadSheet(
            ZipArchive archive,
            string entryName,
            IReadOnlyList<string> sharedStrings)
        {
            var document = LoadXml(archive, entryName);
            var sheet = new XlsxRawSheet();
            foreach (var rowElement in document.Descendants(
                         SpreadsheetNamespace + "row"))
            {
                var rowNumber = ParseInt(rowElement.Attribute("r")?.Value);
                if (rowNumber <= 0)
                {
                    continue;
                }

                foreach (var cell in rowElement.Elements(
                             SpreadsheetNamespace + "c"))
                {
                    var reference = cell.Attribute("r")?.Value ?? string.Empty;
                    var column = ParseColumn(reference);
                    if (column <= 0)
                    {
                        continue;
                    }

                    var type = cell.Attribute("t")?.Value ?? string.Empty;
                    string value;
                    if (string.Equals(
                            type,
                            "inlineStr",
                            StringComparison.Ordinal))
                    {
                        value = string.Concat(
                            cell.Descendants(SpreadsheetNamespace + "t")
                                .Select(text => text.Value));
                    }
                    else
                    {
                        value =
                            cell.Element(SpreadsheetNamespace + "v")?.Value
                            ?? string.Empty;
                        if (string.Equals(type, "s", StringComparison.Ordinal)
                            && int.TryParse(
                                value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture,
                                out var stringIndex)
                            && stringIndex >= 0
                            && stringIndex < sharedStrings.Count)
                        {
                            value = sharedStrings[stringIndex];
                        }
                        else if (string.Equals(
                                     type,
                                     "b",
                                     StringComparison.Ordinal))
                        {
                            value = value == "1" ? "TRUE" : "FALSE";
                        }
                    }

                    sheet.SetCell(rowNumber, column, value);
                }
            }

            return sheet;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            return document.Descendants(SpreadsheetNamespace + "si")
                .Select(item => string.Concat(
                    item.Descendants(SpreadsheetNamespace + "t")
                        .Select(text => text.Value)))
                .ToList();
        }

        private static XDocument LoadXml(
            ZipArchive archive,
            string entryName)
        {
            var entry = archive.GetEntry(entryName)
                        ?? throw new InvalidDataException(
                            $"XLSX 缺少条目：{entryName}");
            using var stream = entry.Open();
            return XDocument.Load(stream);
        }

        private static string NormalizeEntryPath(
            string basePath,
            string target)
        {
            var segments = (target.StartsWith("/", StringComparison.Ordinal)
                    ? target.TrimStart('/')
                    : basePath + target)
                .Replace('\\', '/')
                .Split('/');
            var stack = new List<string>();
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    continue;
                }

                stack.Add(segment);
            }

            return string.Join("/", stack);
        }

        private static int ParseColumn(string reference)
        {
            var result = 0;
            for (var i = 0;
                 i < reference.Length && char.IsLetter(reference[i]);
                 i++)
            {
                result = result * 26
                         + char.ToUpperInvariant(reference[i])
                         - 'A'
                         + 1;
            }

            return result;
        }

        private static int ParseInt(string value) =>
            int.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var result)
                ? result
                : 0;
    }

    internal sealed class XlsxRawSheet
    {
        private readonly Dictionary<long, string> cells =
            new Dictionary<long, string>();

        public int MaxRow { get; private set; }

        public void SetCell(int row, int column, string value)
        {
            cells[Key(row, column)] = value ?? string.Empty;
            MaxRow = Math.Max(MaxRow, row);
        }

        public string GetCell(int row, int column) =>
            cells.TryGetValue(Key(row, column), out var value)
                ? value ?? string.Empty
                : string.Empty;

        public Dictionary<string, int> GetHeaders(int row)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var column = 1; column <= 256; column++)
            {
                var value = GetCell(row, column).Trim();
                if (!string.IsNullOrWhiteSpace(value)
                    && !result.ContainsKey(value))
                {
                    result.Add(value, column);
                }
            }

            return result;
        }

        private static long Key(int row, int column) =>
            ((long)row << 32) | (uint)column;
    }
}
