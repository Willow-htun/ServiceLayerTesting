using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq; // only to read error.message.value
using ServiceLayerTesting.Core;
using ServiceLayerTesting.Model;

namespace ServiceLayerTesting.Processor
{
    public static class JETranAPIinsert
    {
        /// <summary>
        /// Creates TWO hardcoded Journal Entries atomically using Service Layer $batch changeset.
        /// If one fails, none are created.
        /// SUCCESS: logs only "Created N JEs successfully."
        /// FAILURE: logs which JE(s) failed (index + Memo) and the error message.
        /// </summary>
        public static void CreateSampleJE(string sessionId)
        {
            string baseUrl = (ConfigurationManager.AppSettings["ServiceLayerBaseUrl"] ?? "").TrimEnd('/');

            // Hardcoded JEs for quick testing
            var je1 = new JE
            {
                ReferenceDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Memo = "T4",
                JournalEntryLines = new List<JELine>
                {
                    new JELine { AccountCode = "160000", Debit = 1.00, Credit = 0.00, LineMemo = "Test Debit 1" },
                    new JELine { AccountCode = "161000", Debit = 0.00, Credit = 1.00, LineMemo = "Test Credit 1" }
                }
            };

            var je2 = new JE
            {
                ReferenceDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Memo = "T4",
                JournalEntryLines = new List<JELine>
                {
                    new JELine { AccountCode = "160000", Debit = 2.00, Credit = 0.00, LineMemo = "Test Debit 2" },
                    new JELine { AccountCode = "161000", Debit = 0.00, Credit = 2.00, LineMemo = "Test Credit 2" }
                }
            };

            var all = new List<JE> { je1, je2 };
            var ok = PostAllOrNothingBatch(sessionId, baseUrl, all);

            if (ok)
                Logger.WriteLog($"Created {all.Count} JEs successfully.");
            else
                Logger.WriteError("Batch failed — no JEs were created.");
        }

        private static bool PostAllOrNothingBatch(string sessionId, string baseUrl, IList<JE> journalEntries)
        {
            if (journalEntries == null || journalEntries.Count == 0)
            {
                Logger.WriteError("No JEs to post.");
                return false;
            }

            if (journalEntries.Count > 10)
            {
                Logger.WriteError("Too many JEs for one transaction (limit ~10).");
                return false;
            }

            // Build multipart/mixed body with a single changeset (atomic unit)
            string batchBoundary = "batch_" + Guid.NewGuid().ToString("N");
            string changeBoundary = "changeset_" + Guid.NewGuid().ToString("N");
            var sb = new StringBuilder();

            sb.AppendLine($"--{batchBoundary}");
            sb.AppendLine($"Content-Type: multipart/mixed; boundary={changeBoundary}");
            sb.AppendLine();

            int cid = 1;
            foreach (var je in journalEntries)
            {
                string jeJson = JsonConvert.SerializeObject(je);

                sb.AppendLine($"--{changeBoundary}");
                sb.AppendLine("Content-Type: application/http");
                sb.AppendLine("Content-Transfer-Encoding: binary");
                sb.AppendLine($"Content-ID: {cid++}");
                sb.AppendLine();
                sb.AppendLine("POST /b1s/v1/JournalEntries HTTP/1.1");
                sb.AppendLine("Content-Type: application/json");
                sb.AppendLine();
                sb.AppendLine(jeJson);
                sb.AppendLine();
            }

            sb.AppendLine($"--{changeBoundary}--");
            sb.AppendLine($"--{batchBoundary}--");

            var body = Encoding.UTF8.GetBytes(sb.ToString());

            try
            {
                var req = (HttpWebRequest)WebRequest.Create($"{baseUrl}/$batch");
                req.Method = "POST";
                req.Accept = "application/json";
                req.Headers.Add("Cookie", $"B1SESSION={sessionId}");
                req.ContentType = $"multipart/mixed; boundary={batchBoundary}";
                req.ContentLength = body.Length;
                req.Timeout = 60000;

                using (var rs = req.GetRequestStream())
                    rs.Write(body, 0, body.Length);

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var reader = new StreamReader(resp.GetResponseStream()))
                {
                    var respText = reader.ReadToEnd();

                    // Decide success purely from inner parts; do NOT print created JE details.
                    var (allCreated, failures) = AnalyzeBatchResponse(respText, journalEntries);

                    // Accept any 2xx (incl. 202 Accepted) AND require all inner parts to be successful
                    if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300 && allCreated)
                        return true;

                    // Some failed — log concise failure lines (which JE + message)
                    if (failures.Count == 0)
                    {
                        Logger.WriteError($"Batch not fully successful. HTTP: {resp.StatusCode}");
                    }
                    else
                    {
                        Logger.WriteError("Batch errors:");
                        foreach (var f in failures)
                        {
                            int idx = f.Index + 1; // display as 1-based
                            Logger.WriteError($"  JE #{idx} (Memo='{f.Memo}') — {f.Error}");
                        }
                    }
                    return false;
                }
            }
            catch (WebException ex)
            {
                string err = "";
                try
                {
                    using (var rs = ex.Response?.GetResponseStream())
                    using (var r = rs != null ? new StreamReader(rs) : null)
                        err = r?.ReadToEnd() ?? ex.Message;
                }
                catch { }
                Logger.WriteError("Batch post failed (WebException): " + err);
                return false;
            }
            catch (Exception ex)
            {
                Logger.WriteError("Batch post unexpected error: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Tolerant analysis:
        /// - If the response contains no "error" JSON at all -> success.
        /// - Otherwise, extract each "error.message.value" we can find (in order),
        ///   map to JE index by order, and return failures.
        /// </summary>
        private static (bool allCreated, List<(int Index, string Memo, string Error)> failures)
            AnalyzeBatchResponse(string respText, IList<JE> journalEntries)
        {
            var failures = new List<(int, string, string)>();

            if (string.IsNullOrWhiteSpace(respText))
                return (false, new List<(int, string, string)> { (0, "(unknown)", "Empty batch response") });

            // Fast path: if there is no "error" at all, we consider the whole batch successful.
            if (respText.IndexOf("\"error\"", StringComparison.OrdinalIgnoreCase) < 0)
                return (true, failures);

            // Find JSON blocks and capture error.message.value in order of appearance
            var errorMessages = new List<string>();
            int pos = 0;
            while (true)
            {
                int start = respText.IndexOf('{', pos);
                if (start < 0) break;

                int depth = 0, end = -1;
                for (int i = start; i < respText.Length; i++)
                {
                    char c = respText[i];
                    if (c == '{') depth++;
                    else if (c == '}')
                    {
                        depth--;
                        if (depth == 0) { end = i; break; }
                    }
                }
                if (end < 0) break;

                string jsonText = respText.Substring(start, end - start + 1);
                pos = end + 1;

                try
                {
                    var token = JToken.Parse(jsonText);
                    var msg = token.SelectToken("error.message.value")?.ToString();
                    if (!string.IsNullOrWhiteSpace(msg))
                        errorMessages.Add(msg);
                }
                catch
                {
                    // ignore non-JSON blocks
                }
            }

            // Map captured errors to the JEs by order
            for (int i = 0; i < errorMessages.Count && i < journalEntries.Count; i++)
            {
                failures.Add((i, journalEntries[i].Memo ?? "", errorMessages[i]));
            }

            // If we saw "error" but couldn't extract structured messages, return generic failure
            if (failures.Count == 0)
                failures.Add((0, journalEntries.Count > 0 ? journalEntries[0].Memo ?? "" : "", "Batch reported an error but no details were available"));

            return (false, failures);
        }
    }
}
