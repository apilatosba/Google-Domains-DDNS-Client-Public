using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google_Domains_DDNS_Client {
   internal class LogEntry {
      LogEntryType entryType;
      string message;

      public LogEntry(LogEntryType entryType, string message) {
         this.entryType = entryType;
         this.message = message;
      }

      public override string ToString() {
         const int entryTypePartMaxLength = 11; // LogEntryType should not be longer than (entryTypePartMaxLength - 2) characters.
         int entryTypeLength = entryType.ToString().Length;
         int startOffset = (entryTypePartMaxLength - 2 - entryTypeLength) / 2; // -2 for the brackets.
         int endOffset = entryTypePartMaxLength - 2 - entryTypeLength - startOffset;
         string entryTypePart = $"[{entryType.ToString().ToUpper().PadLeft(startOffset + entryTypeLength).PadRight(startOffset + entryTypeLength + endOffset)}]";
         return $"{entryTypePart,-(entryTypePartMaxLength + 3)}{DateTime.Now,-27}{message}";
      }
   }
}
