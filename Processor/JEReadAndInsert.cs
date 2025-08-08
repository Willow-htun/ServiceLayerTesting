using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Configuration;
using ServiceLayerTesting.Model;
using ServiceLayerTesting.Core;

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

                var je = new JE();
                je.JournalEntryLines = new List<JELine>();
                JELine currentLine = null;

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    if (string.IsNullOrEmpty(line))
                    {
                        if (currentLine != null)
                        {
                            je.JournalEntryLines.Add(currentLine);
                            currentLine = null;
                        }
                        continue;
                    }

                    if (line.StartsWith("Date ="))
                    {
                        string dateStr = line.Split('=')[1].Trim();
                        DateTime date = DateTime.ParseExact(dateStr, "MM/dd/yyyy", CultureInfo.InvariantCulture);
                        je.ReferenceDate = date.ToString("yyyy-MM-dd");
                    }
                    else if (line.StartsWith("Memo ="))
                    {
                        je.Memo = line.Split('=')[1].Trim();
                    }
                    else if (line.StartsWith("AccountCode ="))
                    {
                        if (currentLine != null)
                        {
                            je.JournalEntryLines.Add(currentLine);
                        }
                        currentLine = new JELine();
                        currentLine.AccountCode = line.Split('=')[1].Trim();
                    }
                    else if (line.StartsWith("Debit ="))
                    {
                        if (currentLine != null)
                            currentLine.Debit = double.Parse(line.Split('=')[1].Trim(), CultureInfo.InvariantCulture);
                    }
                    else if (line.StartsWith("Credit ="))
                    {
                        if (currentLine != null)
                            currentLine.Credit = double.Parse(line.Split('=')[1].Trim(), CultureInfo.InvariantCulture);
                    }
                    else if (line.StartsWith("LineMemo ="))
                    {
                        if (currentLine != null)
                            currentLine.LineMemo = line.Split('=')[1].Trim();
                    }
                }

                // Don't forget the last line if file doesn't end with an empty line
                if (currentLine != null)
                {
                    je.JournalEntryLines.Add(currentLine);
                }

                if (je.JournalEntryLines.Count == 0)
                {
                    Logger.WriteError("No journal entry lines were parsed from the file.");
                    return;
                }

                bool isSuccess = JEPoster.PostJE(sessionId, je);
                if (isSuccess)
                {
                    Logger.WriteLog("Journal Entry from file inserted successfully.");
                }
                else
                {
                    Logger.WriteError("Journal Entry was not inserted due to posting error.");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteError("Failed to read and insert JE from file: " + ex.Message);
            }
        }
    }
}
