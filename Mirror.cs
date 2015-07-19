using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using DokanNet;
using SharpCompress;
using SharpCompress.Reader;
using SharpCompress.Common;
using FileAccess = DokanNet.FileAccess;

namespace DokanNetMirror
{
    internal class Mirror : IDokanOperations
    {
        private readonly string _path;
        private List<String> rarPaths;

        private const FileAccess DataAccess = FileAccess.ReadData |
                                              FileAccess.WriteData |
                                              FileAccess.AppendData |
                                              FileAccess.Execute |
                                              FileAccess.GenericExecute |
                                              FileAccess.GenericWrite |
                                              FileAccess.GenericRead;

        private const FileAccess DataWriteAccess = FileAccess.WriteData |
                                                   FileAccess.AppendData |
                                                   FileAccess.Delete |
                                                   FileAccess.GenericWrite;


        public Mirror(string path)
        {
            if (!Directory.Exists(path))
                throw new ArgumentException("path");
            _path = path;
        }

        private string GetPath(string fileName)
        {
            return _path + fileName;
        }

        private string GetSPath(string fileName)
        {
            return "S:" + fileName;
        }

        #region Implementation of IDokanOperations

        public DokanError CreateFile(string fileName, FileAccess access, FileShare share, FileMode mode,
                                     FileOptions options, FileAttributes attributes, DokanFileInfo info)
        {
            //Clennup debug code
            if (fileName.Contains("auto") || fileName.Contains("desktop") || fileName.Contains("Auto"))
            { 
                return DokanError.ErrorFileNotFound;
            }

            var path = GetSPath( fileName );

            bool pathExists = true;
            bool pathIsDirectory = false;

            bool readWriteAttributes = (access & DataAccess) == 0;

            bool readAccess = (access & DataWriteAccess) == 0;


            try
            {
                pathIsDirectory = !Path.HasExtension( path );
            }
            catch (IOException)
            {
                pathExists = false;
            }

            switch (mode)
            {
                case FileMode.Open:

                    if (pathExists)
                    {
                        if (readWriteAttributes || pathIsDirectory)
                            //check if only wants to read attributes,security info or open directory
                        {
                            info.IsDirectory = pathIsDirectory;
                            info.Context = new object();
                            // Must set it to someting if you return DokanError.ErrorSuccess

                            return DokanError.ErrorSuccess;
                        }
                    }
                    else
                    {
                        Console.WriteLine("This dose not exist and need to return error or we die! :"+ fileName);
                        if (!Path.HasExtension(fileName))
                        {
                            Console.WriteLine("Allow for virtual folder:"+fileName);
                            info.IsDirectory = true;
                            info.Context = new object();
                            return DokanError.ErrorSuccess;
                        }

                       
                        return DokanError.ErrorFileNotFound;
                    }
                    break;
                case FileMode.CreateNew:
                    if (pathExists)
                        return DokanError.ErrorAlreadyExists;
                    break;
                case FileMode.Truncate:
                    if (!pathExists)
                        return DokanError.ErrorFileNotFound;

                    break;
                default:
                    break;
            }

            info.Context = new object();
            Console.WriteLine("Path:" + pathExists);
            Console.WriteLine("Mode:" + mode);
            Console.WriteLine("Createing file:"+fileName);

            return DokanError.ErrorSuccess;
        }

        public DokanError OpenDirectory(string fileName, DokanFileInfo info)
        {
            return DokanError.ErrorSuccess;
        }

        public DokanError CreateDirectory(string fileName, DokanFileInfo info)
        {
                return DokanError.ErrorAccessDenied;
        }

        public DokanError Cleanup(string fileName, DokanFileInfo info)
        {
            if (info.Context != null && info.Context is FileStream)
            {
                (info.Context as FileStream).Dispose();
            }
            info.Context = null;

            if (info.DeleteOnClose)
            {
                if (info.IsDirectory)
                {
                    Directory.Delete(GetPath(fileName));
                }
                else
                {
                    File.Delete(GetPath(fileName));
                }
            }
            return DokanError.ErrorSuccess;
        }

        public DokanError CloseFile(string fileName, DokanFileInfo info)
        {
            if (info.Context != null && info.Context is FileStream)
            {
                (info.Context as FileStream).Dispose();
            }
            info.Context = null;
            return DokanError.ErrorSuccess; // could recreate cleanup code hear but this is not called sometimes
        }

        public DokanError ReadFile(string fileName, byte[] buffer, out int bytesRead, long offset, DokanFileInfo info)
        {
            string loadPath = "E:\\buu\\test.txt";
            Console.WriteLine("Reading file:"+fileName);

            /* This is a simple hack to mark files as RARFiles, we need to change this, when we know how! */
            String parentFolder = System.IO.Path.GetDirectoryName(fileName);
            if (File.Exists(GetPath(parentFolder) + ".rar"))
            {
                loadPath = "E:\\buu\\rar.txt";
                Console.WriteLine("We are reading a rar File!!!!");
            }

            /* End hack */

            using (var stream = new FileStream(loadPath, FileMode.Open, System.IO.FileAccess.Read))
                {
                    stream.Position = offset;
                    bytesRead = stream.Read(buffer, 0, buffer.Length);
                }
            
            return DokanError.ErrorSuccess;
        }

        public DokanError WriteFile(string fileName, byte[] buffer, out int bytesWritten, long offset,
                                    DokanFileInfo info)
        {
            if (info.Context == null)
            {
                using (var stream = new FileStream(GetPath(fileName), FileMode.Open, System.IO.FileAccess.Write))
                {
                    stream.Position = offset;
                    stream.Write(buffer, 0, buffer.Length);
                    bytesWritten = buffer.Length;
                }
            }
            else
            {
                var stream = info.Context as FileStream;
                stream.Write(buffer, 0, buffer.Length);
                bytesWritten = buffer.Length;
            }
            return DokanError.ErrorSuccess;
        }

        public DokanError FlushFileBuffers(string fileName, DokanFileInfo info)
        {
            try
            {
                ((FileStream)(info.Context)).Flush();
                return DokanError.ErrorSuccess;
            }
            catch (IOException)
            {
                return DokanError.ErrorDiskFull;
            }
        }

        public DokanError GetFileInformation(string fileName, out FileInformation fileInfo, DokanFileInfo info)
        {
            // may be called with info.Context=null , but usually it isn't
            string path = GetSPath(fileName);
            if (!Path.HasExtension(path))
            {
                fileInfo = createVirtualFile(FileAttributes.Directory, 0, fileName);

            }
            else
            {
                fileInfo = createVirtualFile(FileAttributes.Normal, 99000000, fileName);
            }
               
            

            return DokanError.ErrorSuccess;
        }

        public DokanError FindFiles(string fileName, out IList<FileInformation> finale_files, DokanFileInfo info)
        {

            IList<FileInformation> files = new List<FileInformation>();
            if (Directory.Exists(GetPath(fileName)))
            {
                //Getting the real file kontent
                files = new DirectoryInfo(GetPath(fileName)).GetFileSystemInfos().Select(finfo => new FileInformation
                {
                    Attributes =
                        finfo.
                        Attributes,
                    CreationTime =
                        finfo.
                        CreationTime,
                    LastAccessTime =
                        finfo.
                        LastAccessTime,
                    LastWriteTime =
                        finfo.
                        LastWriteTime,
                    Length =
                        (finfo is
                         FileInfo)
                            ? ((
                               FileInfo
                               )finfo
                              ).
                                  Length
                            : 0,
                    FileName =
                        finfo.Name,
                }).ToArray();

            }
            else
            {
                //This is a virtual file
                Console.WriteLine("###Opening a virtual folder:" + GetPath(fileName));
               
                if (File.Exists(GetPath(fileName) + ".rar"))
                {
                    files = GetRarFileContent(GetPath(fileName) + ".rar");
                }
                 


            }
            finale_files = new List<FileInformation>();
            foreach (var fileInfo in files)
            {
                String extension = Path.GetExtension(fileInfo.FileName);
                String filename = fileInfo.FileName;
                String parentFolder = System.IO.Path.GetDirectoryName(fileInfo.FileName);
                if (extension != ".rar")
                {
                    finale_files.Add(fileInfo);
                }
                else
                {
                    //Not sure yet if we want to hide the real file
                    finale_files.Add(fileInfo);
                   
                    string bareName = filename.Substring(0, filename.Length - extension.Length);
                    string virtFolder = parentFolder + "" + bareName;
                    if (!Directory.Exists(virtFolder))
                    {
                        FileInformation rarAsFolder = createVirtualFile(FileAttributes.Directory, 0, bareName);
                        finale_files.Add(rarAsFolder);
                    }
                     

                }
            }
          
            finale_files.Add(createVirtualFile(FileAttributes.Directory, 0, "temp_folder"));
            finale_files.Add(createVirtualFile(FileAttributes.Archive, 888, "test_viertual.txt"));



            return DokanError.ErrorSuccess;
        }

        public static IList<FileInformation> GetRarFileContent(string path)
        {

            IList<FileInformation> finale_files = new List<FileInformation>();
            using (Stream stream = File.OpenRead(path))
            {
                var reader = ReaderFactory.Open(stream);
                string curFileName = null;
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Stream _redaer = new MemoryStream();
                        reader.WriteEntryTo(_redaer);
                        curFileName = reader.Entry.FilePath;
                        long Length = _redaer.Length;

                        FileInformation curFile = createVirtualFile(FileAttributes.ReadOnly, Length, curFileName);
                        finale_files.Add(curFile);


                        // reader.WriteEntryToDirectory(@"C:\temp", ExtractOptions.ExtractFullPath | ExtractOptions.Overwrite);
                    }
                    else
                    {

                    }
                }
            }

            return finale_files;

        }

        public static Stream GetRarFileStream(string rarUrl, string fileName)
        {

            using (Stream stream = File.OpenRead(rarUrl))
            {
                var reader = ReaderFactory.Open(stream);
                string curFileName = null;
                while (reader.MoveToNextEntry())
                {
                    if (!reader.Entry.IsDirectory)
                    {
                        Stream _redaer = new MemoryStream();
                        reader.WriteEntryTo(_redaer);
                        curFileName = reader.Entry.FilePath;
                        long Length = _redaer.Length;

                        if (curFileName == fileName)
                        {
                            Console.WriteLine("Sending back file stream");
                        }
                        else
                        {

                            Console.WriteLine("CurrFilename" + curFileName);
                            Console.WriteLine("Filename in parameter" + fileName);
                        }
                    }
                    else
                    {

                    }
                }
            }

            return new MemoryStream();

        }


        public static FileInformation createVirtualFile(FileAttributes _Type, long _Length, string _FileName)
        {
            return new FileInformation
            {
                Attributes = _Type,
                CreationTime = DateTime.Now,
                LastAccessTime = DateTime.Now,
                LastWriteTime = DateTime.Now,
                Length = _Length,
                FileName = _FileName,
            };
        }


        public DokanError SetFileAttributes(string fileName, FileAttributes attributes, DokanFileInfo info)
        {
            try
            {
                File.SetAttributes(GetPath(fileName), attributes);
                return DokanError.ErrorSuccess;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanError.ErrorAccessDenied;
            }
            catch (FileNotFoundException)
            {
                return DokanError.ErrorFileNotFound;
            }
            catch (DirectoryNotFoundException)
            {
                return DokanError.ErrorPathNotFound;
            }
        }

        public DokanError SetFileTime(string fileName, DateTime? creationTime, DateTime? lastAccessTime,
                                      DateTime? lastWriteTime, DokanFileInfo info)
        {
            try
            {
                string path = GetPath(fileName);
                if (creationTime.HasValue)
                {
                    File.SetCreationTime(path, creationTime.Value);
                }
                if (lastAccessTime.HasValue)
                {
                    File.SetCreationTime(path, lastAccessTime.Value);
                }
                if (lastWriteTime.HasValue)
                {
                    File.SetCreationTime(path, lastWriteTime.Value);
                }
                return DokanError.ErrorSuccess;
            }
            catch (UnauthorizedAccessException)
            {
                return DokanError.ErrorAccessDenied;
            }
            catch (FileNotFoundException)
            {
                return DokanError.ErrorFileNotFound;
            }
        }

        public DokanError DeleteFile(string fileName, DokanFileInfo info)
        {
            return File.Exists(GetPath(fileName)) ? DokanError.ErrorSuccess : DokanError.ErrorFileNotFound;
            // we just check here if we could delete file the true deletion is in Cleanup
        }

        public DokanError DeleteDirectory(string fileName, DokanFileInfo info)
        {
            return Directory.EnumerateFileSystemEntries(GetPath(fileName)).Any()
                       ? DokanError.ErrorDirNotEmpty
                       : DokanError.ErrorSuccess; // if dir is not empdy could not delete
        }

        public DokanError MoveFile(string oldName, string newName, bool replace, DokanFileInfo info)
        {
            string oldpath = GetPath(oldName);
            string newpath = GetPath(newName);
            if (!File.Exists(newpath))
            {
                info.Context = null;

                File.Move(oldpath, newpath);
                return DokanError.ErrorSuccess;
            }
            else if (replace)
            {
                info.Context = null;

                if (!info.IsDirectory)
                    File.Delete(newpath);
                File.Move(oldpath, newpath);
                return DokanError.ErrorSuccess;
            }
            return DokanError.ErrorFileExists;
        }

        public DokanError SetEndOfFile(string fileName, long length, DokanFileInfo info)
        {
            try
            {
                ((FileStream) (info.Context)).SetLength(length);
                return DokanError.ErrorSuccess;
            }
            catch (IOException)
            {
                return DokanError.ErrorDiskFull;
            }
        }

        public DokanError SetAllocationSize(string fileName, long length, DokanFileInfo info)
        {
            try
            {
                ((FileStream) (info.Context)).SetLength(length);
                return DokanError.ErrorSuccess;
            }
            catch (IOException)
            {
                return DokanError.ErrorDiskFull;
            }
        }

        public DokanError LockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            try
            {
                ((FileStream) (info.Context)).Lock(offset, length);
                return DokanError.ErrorSuccess;
            }
            catch (IOException)
            {
                return DokanError.ErrorAccessDenied;
            }
        }

        public DokanError UnlockFile(string fileName, long offset, long length, DokanFileInfo info)
        {
            try
            {
                ((FileStream) (info.Context)).Unlock(offset, length);
                return DokanError.ErrorSuccess;
            }
            catch (IOException)
            {
                return DokanError.ErrorAccessDenied;
            }
        }

        public DokanError GetDiskFreeSpace(out long free, out long total, out long used, DokanFileInfo info)
        {
            string driveLetter = Path.GetPathRoot(_path);
            DriveInfo dinfo = new DriveInfo(driveLetter);

            used = dinfo.AvailableFreeSpace;
            total = dinfo.TotalSize;
            free = dinfo.TotalFreeSpace;

            return DokanError.ErrorSuccess;
        }

        public DokanError GetVolumeInformation(out string volumeLabel, out FileSystemFeatures features,
                                               out string fileSystemName, DokanFileInfo info)
        {
            volumeLabel = "DOKAN";

            fileSystemName = "DOKAN";

            features = FileSystemFeatures.CasePreservedNames | FileSystemFeatures.CaseSensitiveSearch |
                       FileSystemFeatures.PersistentAcls | FileSystemFeatures.SupportsRemoteStorage |
                       FileSystemFeatures.UnicodeOnDisk;


            return DokanError.ErrorSuccess;
        }

        public DokanError GetFileSecurity(string fileName, out FileSystemSecurity security,
                                          AccessControlSections sections, DokanFileInfo info)
        {
            security = null;
            return DokanError.ErrorError;
        }

        public DokanError SetFileSecurity(string fileName, FileSystemSecurity security, AccessControlSections sections,
                                          DokanFileInfo info)
        {
            security = null;
            return DokanError.ErrorError;
        }

        public DokanError Unmount(DokanFileInfo info)
        {
            return DokanError.ErrorSuccess;
        }

        #endregion
    }
}