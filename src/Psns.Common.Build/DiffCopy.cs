using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Security.Cryptography;
using System.IO;

using System.Collections.Concurrent;
using System.Threading;
using ThreadLib = System.Threading.Tasks;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    public static class FileExtensions
    {
        /// <summary>
        /// Determines if the file are equal by comparing the MD5 hashes.
        /// </summary>
        public static bool Equals(string leftFilePath, string rightFilePath)
        {
            var sourceHash = GetMd5Hash(leftFilePath);
            var destHash = GetMd5Hash(rightFilePath);

            return Equals(sourceHash, destHash);
        }

        public static bool Equals(byte[] leftBytes, byte[] rightBytes)
        {
            for(int i = 0; i < leftBytes.Length; i++)
            {
                if(leftBytes[i] != rightBytes[i])
                    return false;
            }

            return true;
        }

        public static byte[] GetMd5Hash(string filePath)
        {
            byte[] hash;

            using(var stream = File.OpenRead(filePath))
            {
                using(var md5 = MD5.Create())
                {
                    hash = md5.ComputeHash(stream);
                }
            }

            return hash;
        }

        public static bool IsDirectory(string filePath)
        {
            return (File.GetAttributes(filePath) & FileAttributes.Directory) == FileAttributes.Directory;
        }

        public static FileAttributes RemoveAttribute(string path, FileAttributes attribute)
        {
            var attributes = File.GetAttributes(path);
            var resulting = attributes & ~attribute;

            if((attributes & attribute) == attribute)
                File.SetAttributes(path, resulting);

            return resulting;
        }

        public static string GetLastSegment(string path)
        {
            return path.Split(new[] { Path.DirectorySeparatorChar }, 
                StringSplitOptions.RemoveEmptyEntries).Last();
        }

        public static void ComprehensiveRecursiveDelete(string directoryPath)
        {
            foreach(var path in Directory.EnumerateFileSystemEntries(directoryPath))
            {
                if(IsDirectory(path))
                    ComprehensiveRecursiveDelete(path);
                else
                    RemoveAttribute(path, FileAttributes.ReadOnly);
            }

            Directory.Delete(directoryPath, true);
        }
    }

    public class DiffCopy : Task
    {
        ConcurrentBag<TaskItem> _copied; 
        ConcurrentBag<string> _deleted;
        ConcurrentDictionary<int, int> _threadCountMap;
        Func<int, int, int> _updateThreadCount;

        ThreadLib.Task<bool> _parentTask;

        public DiffCopy()
        {
            _copied = new ConcurrentBag<TaskItem>();
            _deleted = new ConcurrentBag<string>();
            _threadCountMap = new ConcurrentDictionary<int, int>();

            _updateThreadCount = (int id, int count) =>
            {
                return count++;
            };
        }

        public ITaskItem[] Files { get; set; }

        [Output]
        public ITaskItem[] FilesCopied { get; private set; }

        [Required]
        public ITaskItem Source { get; set; }

        [Required]
        public ITaskItem Destination { get; set; }

        public ITaskItem[] Excluded { get; set; }

        public override bool Execute()
        {
            UpdateThreadStats();

            var status = true;
            FilesCopied = new ITaskItem[0];

            if(!Directory.Exists(Source.ItemSpec))
            {
                Log.LogError(string.Format("{0} does not exist", Source.ItemSpec));
                return false;
            }

            if(!Directory.Exists(Destination.ItemSpec))
            {
                Log.LogError(string.Format("{0} does not exist", Destination.ItemSpec));
                return false;
            }

            _parentTask = ThreadLib.Task.Factory.StartNew<bool>(() =>
            {
                UpdateThreadStats();

                var taskStatus = true;

                if(Files != null)
                {
                    foreach(var file in Files)
                        taskStatus = CopyWithDiff(file.ItemSpec);
                }
                else
                {
                    taskStatus = CopyWithDiff("");
                }

                return taskStatus;
            });

            try
            {
                _parentTask.Wait();
            }
            catch(AggregateException ae)
            {
                foreach(var exception in ae.InnerExceptions)
                    throw;
            }

            status = _parentTask.Result;

            FilesCopied = _copied.ToArray();

            Log.LogMessage(string.Format("{0} files copied, {1} deleted with {2} task threads.", 
                _copied.Count,
                _deleted.Count,
                _threadCountMap.Keys.Count));

            foreach(var value in _threadCountMap)
                Log.LogMessage(string.Format("Thread {0} was used {1} times.", value.Key, value.Value));

            return status;
        }

        private void UpdateThreadStats()
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            _threadCountMap.AddOrUpdate(threadId, 1, _updateThreadCount);
        }

        private bool CopyWithDiff(string path)
        {
            var childTask = ThreadLib.Task<bool>.Factory.StartNew(() =>
            {
                UpdateThreadStats();

                var sourcePath = Path.Combine(Source.ItemSpec, path);
                var destPath = Path.Combine(Destination.ItemSpec, path);

                if(IsExcluded(sourcePath))
                {
                    Log.LogMessage(string.Format("{0} was ignored", sourcePath));
                }
                else
                {
                    if(!FileExtensions.IsDirectory(sourcePath))
                        processFile(sourcePath, destPath);
                    else
                        processDirectory(path, sourcePath, destPath);
                }

                return true;
            }, ThreadLib.TaskCreationOptions.AttachedToParent);

            return childTask.Result;
        }

        private void processFile(string sourcePath, string destPath)
        {
            Action<bool, string> processFile = (bool exists, string logMessage) =>
            {
                FileAttributes? attributes = null;
                if(exists)
                {
                    attributes = File.GetAttributes(destPath);
                    FileExtensions.RemoveAttribute(destPath, FileAttributes.ReadOnly);
                }

                File.Copy(sourcePath, destPath, true);
                _copied.Add(new TaskItem(sourcePath));

                if(attributes.HasValue)
                    File.SetAttributes(destPath, attributes.Value);

                Log.LogMessage(logMessage);
            };

            if(!File.Exists(destPath))
                processFile.Invoke(false, string.Format("{0} copied to {1} - did not exist", sourcePath, destPath));
            else if(!FileExtensions.Equals(sourcePath, destPath))
                processFile.Invoke(true, string.Format("{0} copied to {1} - files are different", sourcePath, destPath));
            else
                Log.LogMessage(string.Format("{0} skipped", sourcePath));
        }

        private void processDirectory(string path, string sourcePath, string destPath)
        {
            bool checkDestination = false;
            List<string> filesToKeep = new List<string>();

            if(!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
                Log.LogMessage(string.Format("{0} created - did not exist", destPath));
            }
            else
                checkDestination = true;

            foreach(var subFile in Directory.EnumerateFileSystemEntries(sourcePath))
            {
                var lastSegment = FileExtensions.GetLastSegment(subFile);

                if(checkDestination)
                    filesToKeep.Add(lastSegment);

                CopyWithDiff(Path.Combine(path, lastSegment));
            }

            if(checkDestination)
            {
                var existing = Directory.EnumerateFileSystemEntries(destPath)
                    .Select(p =>
                    {
                        return FileExtensions.GetLastSegment(p);
                    });

                var deletes = existing.Except(filesToKeep).Where(e =>
                    {
                        if(IsExcluded(e))
                        {
                            Log.LogMessage(string.Format("{0} was ignored", Path.Combine(destPath, e)));
                            return false;
                        }
                        else
                            return true;
                    }).ToList();

                deletes.ForEach(fileToDelete =>
                {
                    var deletionPath = Path.Combine(destPath, fileToDelete);
                    FileExtensions.RemoveAttribute(deletionPath, FileAttributes.ReadOnly);

                    try
                    {
                        if(FileExtensions.IsDirectory(deletionPath))
                            FileExtensions.ComprehensiveRecursiveDelete(deletionPath);
                        else
                            File.Delete(deletionPath);

                        _deleted.Add(deletionPath);
                        Log.LogMessage(string.Format("{0} was deleted", deletionPath));
                    }
                    catch(Exception e)
                    {
                        Log.LogWarning(string.Format("Could not delete {0}. Message: {1}", deletionPath, e.Message));
                    }
                });
            }
        }

        private bool IsExcluded(string path)
        {
            return Excluded != null && Excluded
                .Where(e => path.EndsWith(e.ItemSpec, StringComparison.CurrentCultureIgnoreCase)).Count() > 0;
        }
    }
}
