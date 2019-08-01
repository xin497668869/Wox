using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Wox.Infrastructure.Logger;
using Wox.Plugin.Everything.Everything.Exceptions;

namespace Wox.Plugin.Everything.Everything
{
    public sealed class EverythingAPI
    {
        #region DllImport

        [DllImport(Main.DLL, CharSet = CharSet.Unicode)]
        private static extern int Everything_SetSearchW(string lpSearchString);

        [DllImport(Main.DLL)]
        private static extern void Everything_SetMatchPath(bool bEnable);

        [DllImport(Main.DLL)]
        private static extern void Everything_SetMatchCase(bool bEnable);

        [DllImport(Main.DLL)]
        private static extern void Everything_SetMatchWholeWord(bool bEnable);

        [DllImport(Main.DLL)]
        private static extern void Everything_SetRegex(bool bEnable);

        [DllImport(Main.DLL)]
        private static extern void Everything_SetMax(int dwMax);

        [DllImport(Main.DLL)]
        private static extern void Everything_SetOffset(int dwOffset);

        [DllImport(Main.DLL)]
        private static extern bool Everything_GetMatchPath();

        [DllImport(Main.DLL)]
        private static extern bool Everything_GetMatchCase();

        [DllImport(Main.DLL)]
        private static extern bool Everything_GetMatchWholeWord();

        [DllImport(Main.DLL)]
        private static extern bool Everything_GetRegex();

        [DllImport(Main.DLL)]
        private static extern UInt32 Everything_GetMax();

        [DllImport(Main.DLL)]
        private static extern UInt32 Everything_GetOffset();

        [DllImport(Main.DLL, CharSet = CharSet.Unicode)]
        private static extern string Everything_GetSearchW();

        [DllImport(Main.DLL)]
        private static extern StateCode Everything_GetLastError();

        [DllImport(Main.DLL, CharSet = CharSet.Unicode)]
        private static extern bool Everything_QueryW(bool bWait);

        [DllImport(Main.DLL)]
        private static extern void Everything_SortResultsByPath();

        [DllImport(Main.DLL)]
        private static extern int Everything_GetNumFileResults();

        [DllImport(Main.DLL)]
        private static extern int Everything_GetNumFolderResults();

        [DllImport(Main.DLL)]
        private static extern int Everything_GetNumResults();

        [DllImport(Main.DLL)]
        private static extern int Everything_GetTotFileResults();

        [DllImport(Main.DLL)]
        private static extern int Everything_GetTotFolderResults();

        [DllImport(Main.DLL)]
        private static extern int Everything_GetTotResults();

        [DllImport(Main.DLL)]
        private static extern bool Everything_IsVolumeResult(int nIndex);

        [DllImport(Main.DLL)]
        private static extern bool Everything_IsFolderResult(int nIndex);

        [DllImport(Main.DLL)]
        private static extern bool Everything_IsFileResult(int nIndex);

        [DllImport(Main.DLL, CharSet = CharSet.Unicode)]
        private static extern void Everything_GetResultFullPathNameW(int nIndex, StringBuilder lpString, int nMaxCount);

        [DllImport(Main.DLL)]
        private static extern void Everything_Reset();

        [DllImport(Main.DLL)]
        private static extern void Everything_SetSort(int sort);

        [DllImport(Main.DLL)]
        private static extern void Everything_SetRequestFlags(int sort);

        [DllImport(Main.DLL)]
        public static extern bool Everything_GetResultSize(int nIndex, out long lpFileSize);

        [DllImport(Main.DLL)]
        public static extern bool Everything_GetResultDateCreated(int nIndex, out long lpFileTime);

        [DllImport(Main.DLL)]
        public static extern bool Everything_GetResultDateModified(int nIndex, out long lpFileTime);

        #endregion

        enum StateCode
        {
            OK,
            MemoryError,
            IPCError,
            RegisterClassExError,
            CreateWindowError,
            CreateThreadError,
            InvalidIndexError,
            InvalidCallError
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match path].
        /// </summary>
        /// <value><c>true</c> if [match path]; otherwise, <c>false</c>.</value>
        public Boolean MatchPath
        {
            get { return Everything_GetMatchPath(); }
            set { Everything_SetMatchPath(value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match case].
        /// </summary>
        /// <value><c>true</c> if [match case]; otherwise, <c>false</c>.</value>
        public Boolean MatchCase
        {
            get { return Everything_GetMatchCase(); }
            set { Everything_SetMatchCase(value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [match whole word].
        /// </summary>
        /// <value><c>true</c> if [match whole word]; otherwise, <c>false</c>.</value>
        public Boolean MatchWholeWord
        {
            get { return Everything_GetMatchWholeWord(); }
            set { Everything_SetMatchWholeWord(value); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether [enable regex].
        /// </summary>
        /// <value><c>true</c> if [enable regex]; otherwise, <c>false</c>.</value>
        public Boolean EnableRegex
        {
            get { return Everything_GetRegex(); }
            set { Everything_SetRegex(value); }
        }

        /// <summary>
        /// Resets this instance.
        /// </summary>
        public void Reset()
        {
            Everything_Reset();
        }

        private void no()
        {
            switch (Everything_GetLastError())
            {
                case StateCode.CreateThreadError:
                    throw new CreateThreadException();
                case StateCode.CreateWindowError:
                    throw new CreateWindowException();
                case StateCode.InvalidCallError:
                    throw new InvalidCallException();
                case StateCode.InvalidIndexError:
                    throw new InvalidIndexException();
                case StateCode.IPCError:
                    throw new IPCErrorException();
                case StateCode.MemoryError:
                    throw new MemoryErrorException();
                case StateCode.RegisterClassExError:
                    throw new RegisterClassExException();
            }
        }


        /// <summary>
        /// Searches the specified key word.
        /// </summary>
        /// <param name="keyWord">The key word.</param>
        /// <param name="offset">The offset.</param>
        /// <param name="maxCount">The max count.</param>
        /// <returns></returns>
        public IEnumerable<SearchResult> Search(string keyWord, int offset = 0, int maxCount = 100)
        {
            var sw = new Stopwatch();
            sw.Start();
            if (string.IsNullOrEmpty(keyWord))
                throw new ArgumentNullException("keyWord");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (maxCount < 0)
                throw new ArgumentOutOfRangeException("maxCount");

            if (keyWord.StartsWith("@"))
            {
                Everything_SetRegex(true);
                keyWord = keyWord.Substring(1);
            }

            Everything_SetSearchW(keyWord);
            Everything_SetOffset(offset);
            Everything_SetMax(maxCount);
            Everything_SetSort(Sort.EVERYTHING_SORT_DATE_MODIFIED_DESCENDING);
            Everything_SetRequestFlags(RequestFlag.EVERYTHING_REQUEST_FILE_NAME |
                                       RequestFlag.EVERYTHING_REQUEST_PATH |
                                       RequestFlag.EVERYTHING_REQUEST_SIZE |
                                       RequestFlag.EVERYTHING_REQUEST_DATE_CREATED |
                                       RequestFlag.EVERYTHING_REQUEST_DATE_MODIFIED);
            Log.Warn("22===22 "+sw.ElapsedMilliseconds);
            if (!Everything_QueryW(true))
            {
                switch (Everything_GetLastError())
                {
                    case StateCode.CreateThreadError:
                        throw new CreateThreadException();
                    case StateCode.CreateWindowError:
                        throw new CreateWindowException();
                    case StateCode.InvalidCallError:
                        throw new InvalidCallException();
                    case StateCode.InvalidIndexError:
                        throw new InvalidIndexException();
                    case StateCode.IPCError:
                        throw new IPCErrorException();
                    case StateCode.MemoryError:
                        throw new MemoryErrorException();
                    case StateCode.RegisterClassExError:
                        throw new RegisterClassExException();
                }

                yield break;
            }

            const int bufferSize = 4096;
            StringBuilder buffer = new StringBuilder(bufferSize);
            for (int idx = 0; idx < Everything_GetNumResults(); ++idx)
            {
                Everything_GetResultFullPathNameW(idx, buffer, bufferSize);

                var result = new SearchResult {FullPath = buffer.ToString()};

                long size, createDate, modifiedDate;
                Everything_GetResultSize(idx, out size);
                result.Size = size;

                Everything_GetResultDateCreated(idx, out createDate);
                if (createDate <= 2001406367576800950L)
                {
                    result.DateCreated = createDate;
                }

                Everything_GetResultDateModified(idx, out modifiedDate);
                if (modifiedDate <= 2001406367576800950L)
                {
                    result.DateModified = modifiedDate;
                }


                if (Everything_IsFolderResult(idx))
                    result.Type = ResultType.Folder;
                else if (Everything_IsFileResult(idx))
                    result.Type = ResultType.File;

                yield return result;
            }
            Log.Warn("22=33==22 "+sw.ElapsedMilliseconds);
        }
    }

    public class RequestFlag
    {
        public static int EVERYTHING_REQUEST_FILE_NAME = 0x00000001;
        public static int EVERYTHING_REQUEST_PATH = 0x00000002;
        public static int EVERYTHING_REQUEST_FULL_PATH_AND_FILE_NAME = 0x00000004;
        public static int EVERYTHING_REQUEST_EXTENSION = 0x00000008;
        public static int EVERYTHING_REQUEST_SIZE = 0x00000010;
        public static int EVERYTHING_REQUEST_DATE_CREATED = 0x00000020;
        public static int EVERYTHING_REQUEST_DATE_MODIFIED = 0x00000040;
        public static int EVERYTHING_REQUEST_DATE_ACCESSED = 0x00000080;
        public static int EVERYTHING_REQUEST_ATTRIBUTES = 0x00000100;
        public static int EVERYTHING_REQUEST_FILE_LIST_FILE_NAME = 0x00000200;
        public static int EVERYTHING_REQUEST_RUN_COUNT = 0x00000400;
        public static int EVERYTHING_REQUEST_DATE_RUN = 0x00000800;
        public static int EVERYTHING_REQUEST_DATE_RECENTLY_CHANGED = 0x00001000;
        public static int EVERYTHING_REQUEST_HIGHLIGHTED_FILE_NAME = 0x00002000;
        public static int EVERYTHING_REQUEST_HIGHLIGHTED_PATH = 0x00004000;
        public static int EVERYTHING_REQUEST_HIGHLIGHTED_FULL_PATH_AND_FILE_NAME = 0x00008000;
    }

    public class Sort
    {
        public static int EVERYTHING_SORT_NAME_ASCENDING = 1;
        public static int EVERYTHING_SORT_NAME_DESCENDING = 2;
        public static int EVERYTHING_SORT_PATH_ASCENDING = 3;
        public static int EVERYTHING_SORT_PATH_DESCENDING = 4;
        public static int EVERYTHING_SORT_SIZE_ASCENDING = 5;
        public static int EVERYTHING_SORT_SIZE_DESCENDING = 6;
        public static int EVERYTHING_SORT_EXTENSION_ASCENDING = 7;
        public static int EVERYTHING_SORT_EXTENSION_DESCENDING = 8;
        public static int EVERYTHING_SORT_TYPE_NAME_ASCENDING = 9;
        public static int EVERYTHING_SORT_TYPE_NAME_DESCENDING = 10;
        public static int EVERYTHING_SORT_DATE_CREATED_ASCENDING = 11;
        public static int EVERYTHING_SORT_DATE_CREATED_DESCENDING = 12;
        public static int EVERYTHING_SORT_DATE_MODIFIED_ASCENDING = 13;
        public static int EVERYTHING_SORT_DATE_MODIFIED_DESCENDING = 14;
        public static int EVERYTHING_SORT_ATTRIBUTES_ASCENDING = 15;
        public static int EVERYTHING_SORT_ATTRIBUTES_DESCENDING = 16;
        public static int EVERYTHING_SORT_FILE_LIST_FILENAME_ASCENDING = 17;
        public static int EVERYTHING_SORT_FILE_LIST_FILENAME_DESCENDING = 18;
        public static int EVERYTHING_SORT_RUN_COUNT_ASCENDING = 19;
        public static int EVERYTHING_SORT_RUN_COUNT_DESCENDING = 20;
        public static int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_ASCENDING = 21;
        public static int EVERYTHING_SORT_DATE_RECENTLY_CHANGED_DESCENDING = 22;
        public static int EVERYTHING_SORT_DATE_ACCESSED_ASCENDING = 23;
        public static int EVERYTHING_SORT_DATE_ACCESSED_DESCENDING = 24;
        public static int EVERYTHING_SORT_DATE_RUN_ASCENDING = 25;
        public static int EVERYTHING_SORT_DATE_RUN_DESCENDING = 26;
    }
}