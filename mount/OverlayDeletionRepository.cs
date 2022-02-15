using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Mount
{
    class OverlayDeletionRepository
    {
        private string _path;

        public OverlayDeletionRepository(string path)
        {
            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _path = path;

            if (File.Exists(_path))
            {
                foreach (var relativePath in File.ReadAllLines(_path))
                {
                    if (string.IsNullOrEmpty(relativePath) || !relativePath.StartsWith("\\"))
                        continue;

                    _files.Add(relativePath);
                }
            }
        }

        public void Save()
        {
            File.WriteAllLines(_path, _files.ToArray());
        }

        public void DeleteFile(string path)
        {
            path = path.Replace("/", "\\");

            if (!path.StartsWith("\\"))
                path = "\\" + path;

            if (_files.Any(f => f.Equals(path, StringComparison.InvariantCultureIgnoreCase)))
                return;

            foreach (var f in _files.Where(f => f.Equals(path, StringComparison.InvariantCultureIgnoreCase)).ToArray())
                _files.Remove(f);

            _files.Add(path);

            Save();
        }

        public void RestoreFile(string path)
        {
            path = path.Replace("/", "\\");

            if (!path.StartsWith("\\"))
                path = "\\" + path;

            if (!_files.Any(f => f.Equals(path, StringComparison.InvariantCultureIgnoreCase)))
                return;

            foreach (var f in _files.Where(f => f.Equals(path, StringComparison.InvariantCultureIgnoreCase)).ToArray())
                _files.Remove(f);

            Save();
        }

        public string[] DeletedFiles { get { return _files.ToArray(); } }

        private List<string> _files = new List<string>();
    }
}