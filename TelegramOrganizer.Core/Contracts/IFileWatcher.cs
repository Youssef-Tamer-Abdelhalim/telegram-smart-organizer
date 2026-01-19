using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TelegramOrganizer.Core.Contracts
{
    // بنستخدم EventArgs عشان نبعت بيانات الملف مع الحدث
    public class FileEventArgs : EventArgs
    {
        public string FileName { get; set; }
        public string FullPath { get; set; }
        public string OldFileName { get; set; } // مهم جداً في حالة الـ Rename
    }

    public interface IFileWatcher
    {
        // دالة عشان نبدأ مراقبة فولدر معين
        void Start(string path);

        // دالة عشان نوقف مراقبة
        void Stop();

        // الأحداث اللي الـ Engine هيشترك فيها
        event EventHandler<FileEventArgs> FileCreated;
        event EventHandler<FileEventArgs> FileRenamed;
    }
}
