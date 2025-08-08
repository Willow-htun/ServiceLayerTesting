using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using ServiceLayerTesting.Core;
using ServiceLayerTesting.HelperMethod;
using ServiceLayerTesting.Model;

namespace ServiceLayerTesting.Processor
{
    internal class JEReadAndInsert_Batch
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

                // Post all-or-nothing via $batch changeset
                var ok = BatchJEPoster.PostAllOrNothing(sessionId, allJEs,
                    out var failures, out var httpStatus);

                if (ok)
                {
                    Logger.WriteLog($"Created {allJEs.Count} JEs successfully.");
                }
                else
                {
                    Logger.WriteError("Batch errors:");
                    foreach (var f in failures)
                    {
                        Logger.WriteError($"  JE #{f.Index + 1} (Memo='{f.Memo}') — {f.Error}");
                    }
                    Logger.WriteError("Batch failed — no JEs were created.");
                }

                // One summary email for the whole batch
                bool emailEnabled = string.Equals(
                    (ConfigurationManager.AppSettings["EmailSend"] ?? "Y").Trim(),
                    "Y",
                    StringComparison.OrdinalIgnoreCase
                );

                if (emailEnabled)
                {
                    var subject = ConfigurationManager.AppSettings["SmtpSubject"] ?? "Journal Entry Notification";
                    if (ok)
                    {
                        EmailSender.Send(subject, $"Created {allJEs.Count} JEs successfully.");
                    }
                    else
                    {
                        var bodyLines = new List<string>
                        {
                            //"Batch JE Result (Changeset/$batch)",
                            $"Total: {allJEs.Count}, Success: 0, Failed: {allJEs.Count}",
                            //$"HTTP: {httpStatus}",
                            "Failures:"
                        };
                        bodyLines.AddRange(failures.Select(f =>
                            $"JE #{f.Index + 1} | Memo='{f.Memo}' | Error={f.Error}"
                        ));
                        EmailSender.Send(subject, string.Join(Environment.NewLine, bodyLines));
                    }
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

        // Same parser you already use
        private static List<JE> ParseMultipleJEs(string[] lines)
        {
            var allJEs = new List<JE>();
            JE currentJE = null;
            JELine currentLine = null;

            foreach (var raw in lines)
            {
                var line = raw.Trim();

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

                if (string.IsNullOrEmpty(line))
                {
                    if (currentLine != null)
                    {
                        currentJE?.JournalEntryLines?.Add(currentLine);
                        currentLine = null;
                    }
                    continue;
                }

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
