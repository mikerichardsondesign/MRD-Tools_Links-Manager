using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Windows.Threading;

namespace MRDTools
{
    /// <summary>
    /// Looks for the files that links point at on background threads, so a slow or offline network share doesn't hold up the
    /// window. Each share and folder is looked for once in a round of checks, so links in an offline one wait for one timeout
    /// between them rather than one each. The results are handed to the links on the window's thread.
    /// </summary>
    internal sealed class FileChecker
    {
        readonly Dispatcher dispatcher;

        // Whether each share and folder exists, for the round of checks under way. It's cleared when the round is over,
        // so a folder that appears later is found the next time.
        readonly ConcurrentDictionary<string, Lazy<bool>> folders = new ConcurrentDictionary<string, Lazy<bool>>(StringComparer.OrdinalIgnoreCase);

        int pending;

        public FileChecker(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
        }

        /// <summary>How many links are still being looked for.</summary>
        public int Pending => Volatile.Read(ref pending);

        /// <summary>Looks for the link's file, unless it has none to look for (see LinkRow.FileToCheck).</summary>
        public void Check(LinkRow row)
        {
            string file = row.BeginFileCheck();
            if (file == null) return;

            Interlocked.Increment(ref pending);
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool found = Exists(file);
                dispatcher.BeginInvoke(new Action(() =>
                {
                    row.EndFileCheck(file, found);
                    if (Interlocked.Decrement(ref pending) == 0) folders.Clear();
                }));
            });
        }

        bool Exists(string file)
        {
            try
            {
                return FolderExists(Path.GetPathRoot(file)) && FolderExists(Path.GetDirectoryName(file)) && File.Exists(file);
            }
            catch (Exception)
            {
                return false;
            }
        }

        bool FolderExists(string folder) =>
            string.IsNullOrEmpty(folder) || folders.GetOrAdd(folder, f => new Lazy<bool>(() => Directory.Exists(f))).Value;
    }
}
