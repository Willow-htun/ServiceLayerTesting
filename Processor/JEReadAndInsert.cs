using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Configuration;
using System.Linq;
using ServiceLayerTesting.Model;
using ServiceLayerTesting.Core;
using ServiceLayerTesting.HelperMethod;

namespace ServiceLayerTesting.Processor
{
    internal class JEReadAndInsert
    {
        private static readonly string DefaultJEFilePath = ConfigurationManager.AppSettings["JEFilePath"];

        public static void ReadJEAndInsert(string sessionId)
        {
            ReadJEAndInsert(sessionId, DefaultJEFilePath);
        }

        public static void ReadJEAndInsert(string sessionId, string filePath)
        {
            try
            {
                string[] lines = File.ReadAllLines(filePath);
                var allJEs = ParseMultipleJEs(lines);

                if (allJEs.Count == 0)
                {
                    Logger.WriteError("No journal entry lines were parsed from the file.");
                    return;
                }

                Logger.WriteLog($"Parsed {allJEs.Count} Journal Entries from file.");

                var results = new List<(int Index, string Memo, string RefDate, bool Success, string Message)>();

                for (int i = 0; i < allJEs.Count; i++)
                {
                    var je = allJEs[i];
                    var idx = i + 1;
                    Logger.WriteLog($"[JE #{idx}] Posting... Memo='{je.Memo}', Date='{je.ReferenceDate}', Lines={je.JournalEntryLines?.Count ?? 0}");

                    bool ok = JEPoster.PostJE(sessionId, je);
                    if (ok)
                    {
                        Logger.WriteLog($"[JE #{idx}] Posted successfully.");
                        results.Add((idx, je.Memo, je.ReferenceDate, true, "Created"));
                    }
                    else
                    {
                        Logger.WriteError($"[JE #{idx}] Failed to post.");
                        results.Add((idx, je.Memo, je.ReferenceDate, false, "Failed"));
                    }
                }

                // Single summary email for the whole batch
                bool emailEnabled = string.Equals(
                    (ConfigurationManager.AppSettings["EmailSend"] ?? "Y").Trim(),
                    "Y",
                    StringComparison.OrdinalIgnoreCase
                );

                if (emailEnabled)
                {
                    var total = results.Count;
                    var success = results.Count(r => r.Success);
                    var failed = total - success;

                    var subject = ConfigurationManager.AppSettings["SmtpSubject"] ?? "Journal Entry Notification";

                    // Build body
                    var bodyLines = new List<string>
                    {
                        $"Batch JE Result",
                        $"Total: {total}, Success: {success}, Failed: {failed}",
                        ""
                    };
                    bodyLines.AddRange(results.Select(r =>
                        $"JE #{r.Index} | Date={r.RefDate} | Memo='{r.Memo}' | Status={r.Message}"
                    ));

                    EmailSender.Send(subject, string.Join(Environment.NewLine, bodyLines));
                    Logger.WriteLog("Batch email sent.");
                }
                else
                {
                    Logger.WriteLog("EmailSend = N — skipping batch summary email.");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError("Failed to read and insert JEs from file: " + ex.Message);
            }
        }

        private static List<JE> ParseMultipleJEs(string[] lines)
        {
            var allJEs = new List<JE>();
            JE currentJE = null;
            JELine currentLine = null;

            foreach (var raw in lines)
            {
                var line = raw.Trim();

                // Separator: end current JE
                if (line == "=== JE ===")
                {
                    if (currentLine != null)
                    {
                        currentJE?.JournalEntryLines?.Add(currentLine);
                        currentLine = null;
                    }
                    if (currentJE != null && (currentJE.JournalEntryLines?.Count ?? 0) > 0)
                    {
                        allJEs.Add(currentJE);
                    }
                    currentJE = null;
                    continue;
                }

                // Blank line: end a line item
                if (string.IsNullOrEmpty(line))
                {
                    if (currentLine != null)
                    {
                        currentJE?.JournalEntryLines?.Add(currentLine);
                        currentLine = null;
                    }
                    continue;
                }

                // Lazy-create JE on first header/line
                if (currentJE == null)
                {
                    currentJE = new JE { JournalEntryLines = new List<JELine>() };
                }

                if (line.StartsWith("Date =", StringComparison.OrdinalIgnoreCase))
                {
                    string dateStr = line.Split('=')[1].Trim();
                    DateTime date = DateTime.ParseExact(dateStr, "MM/dd/yyyy", CultureInfo.InvariantCulture);
                    currentJE.ReferenceDate = date.ToString("yyyy-MM-dd");
                }
                else if (line.StartsWith("Memo =", StringComparison.OrdinalIgnoreCase))
                {
                    currentJE.Memo = line.Split('=')[1].Trim();
                }
                else if (line.StartsWith("AccountCode =", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentLine != null) currentJE.JournalEntryLines.Add(currentLine);
                    currentLine = new JELine { AccountCode = line.Split('=')[1].Trim() };
                }
                else if (line.StartsWith("Debit =", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentLine != null)
                        currentLine.Debit = double.Parse(line.Split('=')[1].Trim(), CultureInfo.InvariantCulture);
                }
                else if (line.StartsWith("Credit =", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentLine != null)
                        currentLine.Credit = double.Parse(line.Split('=')[1].Trim(), CultureInfo.InvariantCulture);
                }
                else if (line.StartsWith("LineMemo =", StringComparison.OrdinalIgnoreCase))
                {
                    if (currentLine != null)
                        currentLine.LineMemo = line.Split('=')[1].Trim();
                }
            }

            // finalize EOF
            if (currentLine != null)
            {
                currentJE?.JournalEntryLines?.Add(currentLine);
                currentLine = null;
            }
            if (currentJE != null && (currentJE.JournalEntryLines?.Count ?? 0) > 0)
            {
                allJEs.Add(currentJE);
            }

            return allJEs;
        }
    }
}