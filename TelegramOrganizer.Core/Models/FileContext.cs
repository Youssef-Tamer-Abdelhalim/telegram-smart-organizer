using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramOrganizer.Core.Models
{
    public class FileContext
    {
        public string OriginalTempName { get; set; } // اسم الملف المؤقت (e.g., "123.td")
        public string DetectedGroupName { get; set; } // اسم الجروب (e.g., "CS50")
        public DateTime CapturedAt { get; set; } = DateTime.Now; // وقت الرصد (عشان ممكن نعمل تنظيف للقديم)
    }
}
