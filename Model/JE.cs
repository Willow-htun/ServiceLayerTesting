using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ServiceLayerTesting.Model
{
    public class JE
    {
        public string ReferenceDate { get; set; }
        public string Memo { get; set; }
        public List<JELine> JournalEntryLines { get; set; }
    }
}
