using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using ParticleAutoSorting.Editor.Analysis;
using ParticleAutoSorting.Editor.Data;

namespace ParticleAutoSorting.Editor.Report
{
    public static class CsvReportExporter
    {
        public static bool Export(IEnumerable<PrefabData> prefabs, string filePath)
        {
            if (prefabs == null) return false;
            if (string.IsNullOrEmpty(filePath)) return false;

            var list = new List<PrefabData>();
            foreach (var p in prefabs)
            {
                if (p == null || p.Prefab == null) continue;
                list.Add(p);
            }

            if (list.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "Particle Auto Sorting",
                    "내보낼 프리팹이 없습니다.",
                    "확인");
                return false;
            }

            int totalBefore = 0;
            int totalAfter = 0;
            foreach (var p in list)
            {
                totalBefore += p.BatchBefore;
                totalAfter += p.BatchAfter;
            }

            var sb = new StringBuilder();
            AppendRow(sb,
                "프리팹명",
                "오브젝트명",
                "Above/Below",
                "Material",
                "OiL before",
                "OiL after",
                "Fudge before",
                "Fudge after",
                "프리팹 내 누적 배치 수 before",
                "프리팹 내 누적 배치 수 after",
                "전체 배치 수 before",
                "전체 배치 수 after");

            foreach (var p in list)
            {
                string prefabName = p.Prefab != null ? p.Prefab.name : string.Empty;

                if (p.Renderers == null || p.Renderers.Count == 0)
                {
                    AppendRow(sb,
                        prefabName,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        totalBefore.ToString(CultureInfo.InvariantCulture),
                        totalAfter.ToString(CultureInfo.InvariantCulture));
                    continue;
                }

                var (beforeCum, afterCum) = BatchCounter.CumulativeBatchesPerRenderer(p);

                for (int i = 0; i < p.Renderers.Count; i++)
                {
                    var r = p.Renderers[i];
                    if (r == null) continue;
                    int oilAfter = r.OilOverride ?? r.OilAfterAI;
                    float fudgeAfter = r.FudgeOverride ?? r.FudgeAfterAI;
                    string material = r.SharedMaterial != null ? r.SharedMaterial.name : string.Empty;

                    AppendRow(sb,
                        prefabName,
                        r.ObjectName ?? string.Empty,
                        r.GroupTag ?? string.Empty,
                        material,
                        r.OilBefore.ToString(CultureInfo.InvariantCulture),
                        oilAfter.ToString(CultureInfo.InvariantCulture),
                        r.FudgeBefore.ToString("0.###", CultureInfo.InvariantCulture),
                        fudgeAfter.ToString("0.###", CultureInfo.InvariantCulture),
                        beforeCum[i].ToString(CultureInfo.InvariantCulture),
                        afterCum[i].ToString(CultureInfo.InvariantCulture),
                        totalBefore.ToString(CultureInfo.InvariantCulture),
                        totalAfter.ToString(CultureInfo.InvariantCulture));
                }
            }

            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var encoding = new UTF8Encoding(true);
                File.WriteAllText(filePath, sb.ToString(), encoding);
            }
            catch (IOException e)
            {
                EditorUtility.DisplayDialog(
                    "Particle Auto Sorting",
                    $"CSV 저장 실패:\n{e.Message}",
                    "확인");
                return false;
            }

            return true;
        }

        public static string ShowSavePanel(string defaultName = "particle_auto_sorting_report.csv")
        {
            return EditorUtility.SaveFilePanel(
                "CSV 보고서 저장",
                string.Empty,
                defaultName,
                "csv");
        }

        private static void AppendRow(StringBuilder sb, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(Escape(fields[i]));
            }
            sb.Append("\r\n");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool needsQuote = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!needsQuote) return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
