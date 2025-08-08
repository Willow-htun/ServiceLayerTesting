using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json.Linq; // only to read error.message.value if present
using ServiceLayerTesting.Core;
using ServiceLayerTesting.Model;

namespace ServiceLayerTesting.Processor
{
    internal static class BatchJEPoster
    {
        /// <summary>
        /// Posts all JEs in a single $batch changeset (atomic).
        /// Returns true if ALL succeed; otherwise false and a list of failures.
        /// Does NOT fetch created JE details.
        /// </summary>
        public static bool PostAllOrNothing(
            string sessionId,
            IList<JE> journalEntries,
            out List<(int Index, string Memo, string Error)> failures,
            out HttpStatusCode httpStatus)
        {
            failures = new List<(int, string, string)>();
            httpStatus = 0;

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

            string baseUrl = (ConfigurationManager.AppSettings["ServiceLayerBaseUrl"] ?? "").TrimEnd('/');

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
                string jeJson = Newtonsoft.Json.JsonConvert.SerializeObject(je);

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
                    httpStatus = resp.StatusCode;
                    var respText = reader.ReadToEnd();

                    // Fast path: if the response has no "error" object at all, consider it success.
                    if (respText.IndexOf("\"error\"", StringComparison.OrdinalIgnoreCase) < 0)
                        return true;

                    // Otherwise extract error messages (in order) and map to JEs by order.
                    ExtractBatchErrors(respText, journalEntries, failures);
                    return false;
                }
            }
            catch (WebException ex)
            {
                string bodyText = SafeReadBody(ex.Response);
                httpStatus = HttpStatusCode.BadRequest; // most likely 4xx from Service Layer

                var list = new List<(int, string, string)>();
                ExtractBatchErrors(bodyText, journalEntries, list);
                if (list.Count == 0)
                {
                    // No structured errors — attach raw
                    var memo = journalEntries.Count > 0 ? (journalEntries[0].Memo ?? "") : "";
                    list.Add((0, memo, string.IsNullOrWhiteSpace(bodyText) ? ex.Message : bodyText));
                }
                failures = list;
                return false;
            }
            catch (Exception ex)
            {
                httpStatus = 0;
                var memo = journalEntries.Count > 0 ? (journalEntries[0].Memo ?? "") : "";
                failures = new List<(int, string, string)> { (0, memo, ex.Message) };
                return false;
            }
        }

        /// <summary>
        /// Extracts error messages from a $batch multipart response (very tolerant),
        /// and maps them to the JE index by order of appearance.
        /// </summary>
        private static void ExtractBatchErrors(
            string respText,
            IList<JE> journalEntries,
            List<(int Index, string Memo, string Error)> failures)
        {
            if (string.IsNullOrWhiteSpace(respText))
            {
                var memo = journalEntries.Count > 0 ? (journalEntries[0].Memo ?? "") : "";
                failures.Add((0, memo, "Empty batch response"));
                return;
            }

            // Find JSON blocks in the response; collect "error.message.value" if present
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
                    // ignore non-JSON or malformed JSON fragments
                }
            }

            // Map captured errors to the JEs by order
            for (int i = 0; i < errorMessages.Count && i < journalEntries.Count; i++)
            {
                failures.Add((i, journalEntries[i].Memo ?? "", errorMessages[i]));
            }

            // If we saw "error" word but couldn't parse structured messages, add generic failure
            if (failures.Count == 0 && respText.IndexOf("\"error\"", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var memo = journalEntries.Count > 0 ? (journalEntries[0].Memo ?? "") : "";
                failures.Add((0, memo, "Batch reported an error but no details were available"));
            }
        }

        private static string SafeReadBody(WebResponse resp)
        {
            if (resp == null) return "";
            try
            {
                using (var s = resp.GetResponseStream())
                using (var r = s != null ? new StreamReader(s) : null)
                    return r?.ReadToEnd() ?? "";
            }
            catch { return ""; }
        }
    }
}
